using System;
using System.Collections.Generic;
using System.Linq;

namespace PDT.Plugins.Marantz.Mock
{
    /// <summary>
    /// How the mock receiver misbehaves, for exercising the driver's error paths. The Denon/Marantz
    /// protocol has no error reply (a real unit ignores what it doesn't accept), so the only fault is silence.
    /// </summary>
    internal enum MockMarantzAvrFault
    {
        /// <summary>Answer every command normally</summary>
        None,
        /// <summary>Never answer, so the communication monitor goes offline</summary>
        Silent
    }

    /// <summary>
    /// Answers the Denon/Marantz ASCII control protocol the way a receiver does: queries are answered,
    /// and every state change is reported back as the new state (a real unit echoes changes to every
    /// open session, including changes made with its own remote). Holds the receiver state only and
    /// uses no Crestron types, so it can be linked into tests.
    /// </summary>
    /// <remarks>
    /// Volume on the wire: two digits are whole dB ("MV45" = 45 dB, 450 here), three digits are half
    /// dB and must end in 5 ("MV455" = 45.5 dB). Anything else is not a valid level and is ignored.
    /// </remarks>
    internal class MockMarantzAvrResponder
    {
        private const int VolumeMax = 980;
        private const int ChannelMin = 380;
        private const int ChannelMax = 620;
        private const int ChannelDefault = 500;
        private const int VolumeStep = 5;

        /// <summary>
        /// Input codes the receiver accepts on "SI", matching the driver's input list
        /// </summary>
        public static readonly HashSet<string> Inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DVD", "BD", "TV", "SAT/CBL", "MPLAY", "GAME", "GAME1", "GAME2",
            "AUX1", "AUX2", "AUX3", "AUX4", "AUX5", "AUX6", "AUX7",
            "8K", "CD", "PHONO", "TUNER", "HDRADIO", "NET", "BT"
        };

        /// <summary>
        /// Surround mode names the receiver accepts on "MS"
        /// </summary>
        public static readonly HashSet<string> SurroundModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "STEREO", "MCH STEREO", "MULTI CH IN", "DOLBY DIGITAL", "DTS SURROUND", "PURE DIRECT",
            "AURO2DSURR", "AURO3D", "AUTO", "MOVIE", "MUSIC", "GAME", "DIRECT", "VIRTUAL", "NEURAL"
        };

        private static readonly string[] Channels = { "FL", "FR", "C", "SW", "SL", "SR", "SBL", "SBR" };

        private readonly object stateLock = new object();
        private readonly Dictionary<string, int> channelLevels = new Dictionary<string, int>();
        private bool powerIsOn;
        private bool mainZoneIsOn;
        private int volume;
        private bool isMuted;
        private string input;
        private string surroundMode;
        private readonly int maxVolume;
        private bool zone2IsOn;
        private int zone2Volume;
        private bool zone2IsMuted;
        private string zone2Input;

        /// <summary>
        /// Creates a receiver with the given starting state
        /// </summary>
        public MockMarantzAvrResponder(MockMarantzAvrOptions options)
        {
            options = options ?? new MockMarantzAvrOptions();

            powerIsOn = options.PowerOn;
            mainZoneIsOn = options.PowerOn;
            volume = SnapVolume(options.Volume);
            input = Inputs.Contains(options.Input ?? string.Empty) ? options.Input.ToUpper() : "SAT/CBL";
            surroundMode = SurroundModes.Contains(options.SurroundMode ?? string.Empty) ? options.SurroundMode.ToUpper() : "STEREO";
            maxVolume = Math.Max(1, Math.Min(98, options.MaxVolume));
            zone2Volume = 400;
            zone2Input = "SOURCE";

            foreach (var channel in Channels)
            {
                channelLevels[channel] = ChannelDefault;
            }
        }

        /// <summary>
        /// Raised with the lines a real receiver would send unprompted after a change made at the unit
        /// </summary>
        public event Action<IList<string>> Unsolicited;

        /// <summary>
        /// Current fault mode
        /// </summary>
        public MockMarantzAvrFault Fault { get; set; }

        /// <summary>
        /// Answers one command line (without the \r delimiter)
        /// </summary>
        /// <returns>The reply lines, in order; empty when the receiver says nothing</returns>
        public IList<string> Respond(string line)
        {
            var command = (line ?? string.Empty).Trim().ToUpper();
            if (command.Length == 0)
            {
                return new string[0];
            }

            lock (stateLock)
            {
                if (Fault == MockMarantzAvrFault.Silent)
                {
                    return new string[0];
                }

                // Order matters: MVMAX before MV
                if (command.StartsWith("PW")) return Power(command.Substring(2));
                if (command.StartsWith("MVMAX")) return new string[0]; // a response, not a command
                if (command.StartsWith("MV")) return MasterVolume(command.Substring(2));
                if (command.StartsWith("MU")) return Mute(command.Substring(2));
                if (command.StartsWith("SI")) return Input(command.Substring(2));
                if (command.StartsWith("MS")) return Surround(command.Substring(2));
                if (command.StartsWith("ZM")) return MainZone(command.Substring(2));
                if (command.StartsWith("Z2")) return Zone2(command.Substring(2));
                if (command.StartsWith("CV")) return ChannelVolume(command.Substring(2));

                // Unknown commands get no reply, so gaps in the mock show up as unanswered commands
                return new string[0];
            }
        }

        /// <summary>
        /// Changes the main zone power as if someone used the receiver's own buttons or remote
        /// </summary>
        public void SetPower(bool on)
        {
            IList<string> lines;
            lock (stateLock) lines = MainZone(on ? "ON" : "OFF");
            RaiseUnsolicited(lines);
        }

        /// <summary>
        /// Changes the input as if someone used the receiver's own buttons or remote
        /// </summary>
        /// <returns>False if the receiver has no such input</returns>
        public bool SetInput(string inputCode)
        {
            IList<string> lines;
            lock (stateLock) lines = Input((inputCode ?? string.Empty).ToUpper());
            RaiseUnsolicited(lines);
            return lines.Count > 0;
        }

        /// <summary>
        /// Changes the master volume (0-980, 5 per 0.5 dB) as if someone used the receiver's own buttons or remote
        /// </summary>
        public void SetVolume(int level)
        {
            IList<string> lines;
            lock (stateLock)
            {
                volume = SnapVolume(level);
                lines = new[] { EncodeVolume("MV", volume) };
            }

            RaiseUnsolicited(lines);
        }

        /// <summary>
        /// Changes the mute state as if someone used the receiver's own buttons or remote
        /// </summary>
        public void SetMute(bool muted)
        {
            IList<string> lines;
            lock (stateLock) lines = Mute(muted ? "ON" : "OFF");
            RaiseUnsolicited(lines);
        }

        /// <summary>
        /// One-line description of the receiver state, for the console
        /// </summary>
        public string Describe()
        {
            lock (stateLock)
            {
                return string.Format("power={0} mainZone={1} volume={2} mute={3} input={4} surround={5} zone2={6}/{7}/{8} fault={9}",
                    powerIsOn ? "on" : "standby", mainZoneIsOn ? "on" : "off", EncodeVolume("", volume), isMuted,
                    input, surroundMode, zone2IsOn ? "on" : "off", zone2Input, EncodeVolume("", zone2Volume), Fault);
            }
        }

        private void RaiseUnsolicited(IList<string> lines)
        {
            var handler = Unsolicited;
            if (handler != null && lines.Count > 0 && Fault != MockMarantzAvrFault.Silent)
            {
                handler(lines);
            }
        }

        private IList<string> Power(string arg)
        {
            if (arg == "?") return new[] { PowerString() };

            if (arg == "ON")
            {
                powerIsOn = true;
                return new[] { PowerString() };
            }

            if (arg == "STANDBY" || arg == "OFF")
            {
                powerIsOn = false;
                mainZoneIsOn = false;
                zone2IsOn = false;
                return new[] { PowerString(), "ZMOFF", "Z2OFF" };
            }

            return new string[0];
        }

        private string PowerString()
        {
            return powerIsOn ? "PWON" : "PWSTANDBY";
        }

        private IList<string> MainZone(string arg)
        {
            if (arg == "?") return new[] { MainZoneString() };

            if (arg == "ON")
            {
                mainZoneIsOn = true;
                powerIsOn = true; // turning a zone on wakes the unit
                return new[] { "PWON", "ZMON" };
            }

            if (arg == "OFF")
            {
                mainZoneIsOn = false;
                return new[] { "ZMOFF" };
            }

            return new string[0];
        }

        private string MainZoneString()
        {
            return mainZoneIsOn && powerIsOn ? "ZMON" : "ZMOFF";
        }

        private IList<string> MasterVolume(string arg)
        {
            // Real units answer the level, then MVMAX
            if (arg == "?") return new[] { EncodeVolume("MV", volume), "MVMAX " + maxVolume };

            if (arg == "UP") volume = Math.Min(VolumeMax, volume + VolumeStep);
            else if (arg == "DOWN") volume = Math.Max(0, volume - VolumeStep);
            else
            {
                int level;
                if (!TryParseVolume(arg, out level)) return new string[0];
                volume = Math.Min(VolumeMax, level);
            }

            return new[] { EncodeVolume("MV", volume) };
        }

        private IList<string> Mute(string arg)
        {
            if (arg == "ON") isMuted = true;
            else if (arg == "OFF") isMuted = false;
            else if (arg != "?") return new string[0];

            return new[] { isMuted ? "MUON" : "MUOFF" };
        }

        private IList<string> Input(string arg)
        {
            arg = arg.Trim();
            if (arg == "?") return new[] { "SI" + input };

            // A real unit ignores unknown source names
            if (!Inputs.Contains(arg)) return new string[0];

            input = arg;
            return new[] { "SI" + input };
        }

        private IList<string> Surround(string arg)
        {
            arg = arg.Trim();
            if (arg == "?") return new[] { "MS" + surroundMode };
            if (!SurroundModes.Contains(arg)) return new string[0];

            surroundMode = arg;
            return new[] { "MS" + surroundMode };
        }

        private IList<string> ChannelVolume(string arg)
        {
            arg = arg.Trim();
            if (arg == "?") return ChannelDump();

            if (arg == "ZRL")
            {
                foreach (var channel in Channels)
                {
                    channelLevels[channel] = ChannelDefault;
                }

                return ChannelDump();
            }

            var parts = arg.Split(' ');
            int level;
            if (parts.Length != 2 || !channelLevels.ContainsKey(parts[0]) || !TryParseVolume(parts[1], out level))
            {
                return new string[0];
            }

            channelLevels[parts[0]] = Math.Max(ChannelMin, Math.Min(ChannelMax, level));
            return new[] { EncodeVolume("CV" + parts[0] + " ", channelLevels[parts[0]]), "CVEND" };
        }

        private IList<string> ChannelDump()
        {
            var lines = Channels.Select(channel => EncodeVolume("CV" + channel + " ", channelLevels[channel])).ToList();
            lines.Add("CVEND");
            return lines;
        }

        private IList<string> Zone2(string arg)
        {
            arg = arg.Trim();

            // Real units dump zone status: power, input, volume, mute
            if (arg == "?") return new[] { Zone2PowerString(), "Z2" + zone2Input, EncodeVolume("Z2", zone2Volume), Zone2MuteString() };

            switch (arg)
            {
                case "ON":
                    zone2IsOn = true;
                    powerIsOn = true;
                    return new[] { "Z2ON" };
                case "OFF":
                    zone2IsOn = false;
                    return new[] { "Z2OFF" };
                case "MUON":
                    zone2IsMuted = true;
                    return new[] { "Z2MUON" };
                case "MUOFF":
                    zone2IsMuted = false;
                    return new[] { "Z2MUOFF" };
                case "MU?":
                    return new[] { Zone2MuteString() };
                case "UP":
                    zone2Volume = Math.Min(VolumeMax, zone2Volume + VolumeStep);
                    return new[] { EncodeVolume("Z2", zone2Volume) };
                case "DOWN":
                    zone2Volume = Math.Max(0, zone2Volume - VolumeStep);
                    return new[] { EncodeVolume("Z2", zone2Volume) };
            }

            if (arg == "SOURCE" || Inputs.Contains(arg))
            {
                zone2Input = arg;
                return new[] { "Z2" + zone2Input };
            }

            int level;
            if (!TryParseVolume(arg, out level)) return new string[0];

            zone2Volume = Math.Min(VolumeMax, level);
            return new[] { EncodeVolume("Z2", zone2Volume) };
        }

        private string Zone2PowerString()
        {
            return zone2IsOn && powerIsOn ? "Z2ON" : "Z2OFF";
        }

        private string Zone2MuteString()
        {
            return zone2IsMuted ? "Z2MUON" : "Z2MUOFF";
        }

        /// <summary>
        /// Two digits are whole dB; three digits are half dB and must end in 5. Anything else is invalid.
        /// </summary>
        private static bool TryParseVolume(string digits, out int level)
        {
            level = 0;
            digits = (digits ?? string.Empty).Trim();
            if (digits.Length < 2 || digits.Length > 3 || !digits.All(char.IsDigit))
            {
                return false;
            }

            var value = int.Parse(digits);
            if (digits.Length == 2)
            {
                level = value * 10;
                return true;
            }

            if (value % 10 != 5)
            {
                return false;
            }

            level = value;
            return true;
        }

        private static string EncodeVolume(string prefix, int level)
        {
            return level % 10 == 0
                ? string.Format("{0}{1:D2}", prefix, level / 10)
                : string.Format("{0}{1:D3}", prefix, level);
        }

        private static int SnapVolume(int level)
        {
            level = Math.Max(0, Math.Min(VolumeMax, level));
            return level - (level % VolumeStep);
        }
    }
}
