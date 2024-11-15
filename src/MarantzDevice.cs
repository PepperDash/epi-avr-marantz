using Crestron.SimplSharp;
using Crestron.SimplSharpPro.CrestronThread;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.GeneralIO;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using PepperDash.Essentials.Core.Queues;
using System;
using System.Collections.Generic;
using System.Linq;
using Feedback = PepperDash.Essentials.Core.Feedback;


// TODO: Add IHasInputs and IInputs back into this repo for 3-series compatibility

#if SERIES4
#else
using PDT.Plugins.Marantz.Interfaces;
#endif
namespace PDT.Plugins.Marantz
{
    public class MarantzDevice : EssentialsBridgeableDevice,
        IOnline,
        IHasPowerControlWithFeedback,
        IBasicVolumeWithFeedbackAdvanced,
        IRouting,
        ICommunicationMonitor,
        IHasFeedback,
        IHasSurroundChannels,
        IHasInputs<string>,
        IRoutingSinkWithSwitching,
        IDeviceInfoProvider,
        IHasSurroundSoundModes<eSurroundModes, string>,
        IWarmingCooling
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private readonly Dictionary<SurroundChannel, MarantzChannelVolume> _surroundChannels;
        private readonly int _rampRepeatTimeMs;

        private MarantzZone2 _zone2;

        public ISelectableItems<eSurroundModes> SurroundSoundModes { get; private set; }

        public ISelectableItems<string> Inputs { get; private set; }


        private readonly GenericQueue _receiveQueue;

        private const string CommsDelimiter = "\r";

        private bool _powerIsOn;


        public bool PowerIsOn
        {
            get { return _powerIsOn; }
            private set
            {
                if (value == _powerIsOn)
                    return;
                _powerIsOn = value;
                PowerIsOnFeedback.FireUpdate();
                if (_powerIsOn)
                {
                    _isWarmingUp = false;
                    IsWarmingUpFeedback.FireUpdate();
                }
                else
                {
                    _isCoolingDown = false;
                    IsCoolingDownFeedback.FireUpdate();
                    VolumeLevel = 0;

                    // Clear out any FB that is invalid when the unit is off
                    CurrentSourceInfoKey = null;
                    CurrentSourceInfo = null;

                    MuteIsOn = false;

                    foreach (var item in Inputs.Items)
                    {
                        item.Value.IsSelected = false;
                    }

                    foreach (var item in SurroundSoundModes.Items)
                    {
                        item.Value.IsSelected = false;
                    }
                }
            }
        }

        private bool _muteIsOn;

        public bool MuteIsOn
        {
            get { return _muteIsOn; }
            private set
            {
                if (value == _muteIsOn)
                    return;
                _muteIsOn = value;
                MuteFeedback.FireUpdate();
            }
        }

        private int _volumeLevel;

        /// <summary>
        /// Volume level from 0 - 980
        /// </summary>
        public int VolumeLevel
        {
            get { return _volumeLevel; }
            private set
            {
                if (value == _volumeLevel)
                    return;
                _volumeLevel = value;
                Debug.Console(2, this, " Volume Level: {0}", _volumeLevel);
                VolumeLevelFeedback.FireUpdate();
            }
        }

        /// <summary>
        /// The raw decimal volume of the channel from -80dB (-800) to 18dB (180) in decibels
        /// </summary>
        public int RawVolumeLevel
        {
            get
            {
                return CrestronEnvironment.ScaleWithLimits(VolumeLevel, 980, 0, 180, -800);
            }
        }

        public eVolumeLevelUnits Units
        {
            get { return eVolumeLevelUnits.Decibels; }
        }

        private int _maxVolLevel;
        public int MaxVolLevel
        {
            get { return _maxVolLevel; }
            set
            {
                _maxVolLevel = value;
            }
        }

        private int _minVolLevel;
        public int MinVolLevel
        {
            get { return _minVolLevel; }
            set
            {
                _minVolLevel = value;
            }
        }

        public MarantzDevice(string key, string name, MarantzProps config, IBasicCommunication comms)
            : base(key, name)
        {
            _rampRepeatTimeMs = config.RampRepeatTimeMs == 0 ? 250 : config.RampRepeatTimeMs;
            try
            {
                _receiveQueue = new GenericQueue(Key + "-rxQueue", Thread.eThreadPriority.MediumPriority, 2048);

                DeviceInfo = new DeviceInfo();

                _surroundChannels = new Dictionary<SurroundChannel, MarantzChannelVolume>();

                _comms = comms;

                var socket = _comms as ISocketStatus;
                if (socket != null)
                {
                    socket.ConnectionChange += OnSocketConnectionChange;
                }

                var monitorConfig = config.Monitor ?? new CommunicationMonitorConfig()
                {
                    PollString = "PW?" + CommsDelimiter,
                    PollInterval = 30000,
                    TimeToWarning = 60000,
                    TimeToError = 120000
                };

                _commsMonitor = new GenericCommunicationMonitor(this, comms, monitorConfig);

                SetupInputs();

                SetupDefaultSurroundModes();

                //SetupSurroundChannels();

                SetupGather();

                SetupRoutingPorts();

                SetupFeedbacks();

                SetupPolling();

                SetupConsoleCommands();

                if (config.EnableZone2)
                {
                    _zone2 = new MarantzZone2(Key + "-z2", Name + " - Zone 2", this);
                    DeviceManager.AddDevice(_zone2);
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Error in the constructor: {0}", e);
            }
        }

        public override bool CustomActivate()
        {
            // Check for mobile control and add the custom messengers 
            var mc = DeviceManager.AllDevices.OfType<IMobileControl>().FirstOrDefault();

            if (mc == null)
            {
                return base.CustomActivate();
            }

            var surroundModeMessenger = new ISelectableItemsMessenger<eSurroundModes>
                (string.Format("{0}-surroundSoundModes-plugin", Key),
                string.Format("/device/{0}", Key),
                this.SurroundSoundModes, "surroundSoundModes");
            mc.AddDeviceMessenger(surroundModeMessenger);

            var surroundChannelMessenger = new ISurroundChannelsMessenger
                (string.Format("{0}-surroundChannels-plugin", Key),
                               string.Format("/device/{0}", Key),
                                              this);
            mc.AddDeviceMessenger(surroundChannelMessenger);

            // Inputs messenger should be automatically added by the MC plugin

            return base.CustomActivate();
        }

        /// <summary>
        /// Attempts to add a messenger for the surround channel
        /// </summary>
        /// <param name="channel"></param>
        private void AddSurroundChannelMessenger(SurroundChannel channel)
        {
            try
            {
                var mc = DeviceManager.AllDevices.OfType<IMobileControl>().FirstOrDefault();

                if (mc == null)
                {
                    return;
                }

                if (_surroundChannels.TryGetValue(channel, out var channelValue))
                {
                    var channelMessenger = new DeviceVolumeMessenger(
                        string.Format("{0}-{1}-channelVolume-plugin", Key, channel.ToString()),
                        string.Format("/device/{0}/{1}", Key, channel.ToString()),
                        channelValue);
                    mc.AddDeviceMessenger(channelMessenger);
                }
            }
            catch (Exception e)
            {
                Debug.Console(0, this, "Error adding channel volume messengers: {0}", e);
            }
        }

        private void SetupInputs()
        {
            Inputs = new MarantzInputs
            {
                Items = new Dictionary<string, ISelectableItem>
                {
                    {"DVD", new MarantzInput("DVD", "DVD", this, "DVD")},
                    {"BD", new MarantzInput("BD", "BD", this, "BD")},
                    {"TV", new MarantzInput("TV", "TV", this, "TV")},
                    {"SAT/CBL", new MarantzInput("SAT/CBL", "SAT/CBL", this, "SAT/CBL")},
                    {"MPLAY", new MarantzInput("MPLAY", "MPLAY", this, "MPLAY")},
                    {"GAME", new MarantzInput("GAME", "GAME", this, "GAME")},
                    {"8K", new MarantzInput("8K", "8K", this, "8K")},
                    {"AUX1", new MarantzInput("AUX1", "AUX1", this, "AUX1")},
                    {"AUX2", new MarantzInput("AUX2", "AUX2", this, "AUX2")},
                    //{"AUX3", new MarantzInput("AUX3", "AUX3", this, "AUX3")},
                    //{"AUX4", new MarantzInput("AUX4", "AUX4", this, "AUX4")},
                    //{"AUX5", new MarantzInput("AUX5", "AUX5", this, "AUX5")},
                    //{"AUX6", new MarantzInput("AUX6", "AUX6", this, "AUX6")},
                    //{"AUX7", new MarantzInput("AUX7", "AUX7", this, "AUX7")},
                    {"CD", new MarantzInput("CD", "CD", this, "CD")},
                    //{"PHONO", new MarantzInput("PHONO", "PHONO", this, "PHONO")},
                    //{"TUNER", new MarantzInput("TUNER", "TUNER", this, "TUNER")},
                    //{"HDRADIO", new MarantzInput("HDRADIO", "HDRADIO", this, "HDRADIO")},
                    {"NET", new MarantzInput("NET", "NET", this, "NET")},
                    {"BT", new MarantzInput("BT", "BT", this, "BT")},
                }
            };
        }

        public void SetDefaultChannelLevels()
        {
            SendText("CVZRL");

            // OR on older models
            //foreach (var channel in _surroundChannels)
            //{
            //    channel.Value.SetVolume(500);
            //}
        }

        private void SetupDefaultSurroundModes()
        {
            // Denon AVR-2311CI Surround Modes
            SurroundSoundModes = new MarantzSurroundModes
            {

                Items = new Dictionary<eSurroundModes, ISelectableItem>
                {
                    {eSurroundModes.Direct, new MarantzSurroundMode(eSurroundModes.Direct.ToString(), "Direct", this, "DIRECT")},
                    {eSurroundModes.DolbyDigital, new MarantzSurroundMode(eSurroundModes.DolbyDigital.ToString(), "Dolby Digital", this, "DOLBY DIGITAL", "DOLBY")},
                    {eSurroundModes.DTS, new MarantzSurroundMode(eSurroundModes.DTS.ToString(), "DTS", this, "DTS SURROUND", "DTS")},
                    //{eSurroundModes.JazzClub, new MarantzSurroundMode(eSurroundModes.JazzClub.ToString(), "Jazz Club", this, "JAZZ CLUB")},
                    {eSurroundModes.Matrix, new MarantzSurroundMode(eSurroundModes.Matrix.ToString(), "Matrix", this, "MATRIX")},
                    //{eSurroundModes.MonoMovie, new MarantzSurroundMode(eSurroundModes.MonoMovie.ToString(), "Mono Movie", this, "MONO MOVIE")},
                    {eSurroundModes.MultiChannelStereo, new MarantzSurroundMode(eSurroundModes.MultiChannelStereo.ToString(), "Multi Channel Stereo", this, "MCH STEREO")},
                    {eSurroundModes.PureDirect, new MarantzSurroundMode(eSurroundModes.PureDirect.ToString(), "Pure Direct", this, "PURE DIRECT")},
                    //{eSurroundModes.RockArena, new MarantzSurroundMode(eSurroundModes.RockArena.ToString(), "Rock Arena", this, "ROCK ARENA")},
                    {eSurroundModes.Standard, new MarantzSurroundMode(eSurroundModes.Standard.ToString(), "Standard", this, "STANDARD")},
                    {eSurroundModes.Stereo, new MarantzSurroundMode(eSurroundModes.Stereo.ToString(), "Stereo", this, "STEREO")},
                    {eSurroundModes.VideoGame, new MarantzSurroundMode(eSurroundModes.VideoGame.ToString(), "Video Game", this, "VIDEO GAME")},
                    {eSurroundModes.Virtual, new MarantzSurroundMode(eSurroundModes.Virtual.ToString(), "Virtual", this, "VIRTUAL")}
                }
            };

            // Marants SR8015 Surround Modes
            //SurroundSoundModes = new MarantzSurroundModes
            //{

            //    Items = new Dictionary<eSurroundModes, ISelectableItem>
            //    {
            //        {eSurroundModes.Auro2DSurround, new MarantzSurroundMode(eSurroundModes.Auro2DSurround.ToString(), "Auro 2D Surround", this, "AURO2DSURR")},
            //        {eSurroundModes.Auro3D, new MarantzSurroundMode(eSurroundModes.Auro3D.ToString(), "Auro 3D", this, "AURO3D")},
            //        {eSurroundModes.Auto, new MarantzSurroundMode(eSurroundModes.Auto.ToString(), "Auto", this, "AUTO")},
            //        {eSurroundModes.Direct, new MarantzSurroundMode(eSurroundModes.Direct.ToString(), "Direct", this, "DIRECT")},
            //        {eSurroundModes.DolbyDigital, new MarantzSurroundMode(eSurroundModes.DolbyDigital.ToString(), "Dolby Digital", this, "DOLBY DIGITAL", "DOLBY")},
            //        {eSurroundModes.DTS, new MarantzSurroundMode(eSurroundModes.DTS.ToString(), "DTS", this, "DTS SURROUND", "DTS")},
            //        {eSurroundModes.Game, new MarantzSurroundMode(eSurroundModes.Game.ToString(), "Game", this, "GAME")},
            //        {eSurroundModes.Left, new MarantzSurroundMode(eSurroundModes.Left.ToString(), "Left", this, "LEFT")},
            //        {eSurroundModes.Movie, new MarantzSurroundMode(eSurroundModes.Movie.ToString(), "Movie", this, "MOVIE")},
            //        {eSurroundModes.MultiChannelStereo, new MarantzSurroundMode(eSurroundModes.MultiChannelStereo.ToString(), "Multi Channel Stereo", this, "MCH STEREO")},
            //        {eSurroundModes.Music, new MarantzSurroundMode(eSurroundModes.Music.ToString(), "Music", this, "MUSIC")},
            //        {eSurroundModes.Neural, new MarantzSurroundMode(eSurroundModes.Neural.ToString(), "Neural", this, "NEURAL")},
            //        {eSurroundModes.PureDirect, new MarantzSurroundMode(eSurroundModes.PureDirect.ToString(), "Pure Direct", this, "PURE DIRECT")},
            //        {eSurroundModes.Right, new MarantzSurroundMode(eSurroundModes.Right.ToString(), "Right", this, "RIGHT")}
            //        {eSurroundModes.Standard, new MarantzSurroundMode(eSurroundModes.Standard.ToString(), "Standard", this, "STANDARD")},
            //        {eSurroundModes.Stereo, new MarantzSurroundMode(eSurroundModes.Stereo.ToString(), "Stereo", this, "STEREO")},
            //        {eSurroundModes.Virtual, new MarantzSurroundMode(eSurroundModes.Virtual.ToString(), "Virtual", this, "VIRTUAL")},
            //    }
            //};
        }

        private void SetupGather()
        {
            var gather = new CommunicationGather(_comms, CommsDelimiter);
            gather.LineReceived += OnGatherLineReceived;
        }

        private void SetupRoutingPorts()
        {
            InputPorts = new RoutingPortCollection<RoutingInputPort>
            {
                new RoutingInputPort("CD", eRoutingSignalType.Audio, eRoutingPortConnectionType.LineAudio, "CD", this),
                new RoutingInputPort("DVD", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "DVD", this),
                new RoutingInputPort("TV", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "TV", this),
                new RoutingInputPort("SAT/CBL", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi,
                    "SAT/CBL",
                    this),
                new RoutingInputPort("MPLAY", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "MPLAY",
                    this),
                new RoutingInputPort("GAME", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "GAME",
                    this),
                new RoutingInputPort("8K", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "8K", this),
                new RoutingInputPort("TUNER", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "TUNER",
                    this),
                new RoutingInputPort("HDRADIO", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi,
                    "HDRADIO", this),
                new RoutingInputPort("PHONO", eRoutingSignalType.Audio, eRoutingPortConnectionType.LineAudio, "PHONO",
                    this),
                new RoutingInputPort("AUX1", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX1",
                    this),
                new RoutingInputPort("AUX2", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX2",
                    this),
                new RoutingInputPort("AUX3", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX3",
                    this),
                new RoutingInputPort("AUX4", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX4",
                    this),
                new RoutingInputPort("AUX5", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX5",
                    this),
                new RoutingInputPort("AUX6", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX6",
                    this),
                new RoutingInputPort("AUX7", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX7",
                    this),
                new RoutingInputPort("NET", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Streaming, "NET",
                    this),
                new RoutingInputPort("BT", eRoutingSignalType.Audio, eRoutingPortConnectionType.Streaming, "BT", this),
            };

            OutputPorts = new RoutingPortCollection<RoutingOutputPort>
            {
                new RoutingOutputPort("ZONE1", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "Main", this),
                new RoutingOutputPort("ZONE2", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "Z2", this)
            };
        }

        private void SetupConsoleCommands()
        {
            CrestronConsole.AddNewConsoleCommand(s =>
            {
                var request = s.Trim().ToLower();
                switch (request)
                {
                    case "poll":
                        Poll(this);
                        break;
                    case "poweron":
                        PowerOn();
                        break;
                    case "poweroff":
                        PowerOff();
                        break;
                    case "input?":
                        CrestronConsole.ConsoleCommandResponse("{0}", Inputs.CurrentItem);
                        break;
                    case "power?":
                        CrestronConsole.ConsoleCommandResponse("{0}", PowerIsOn);
                        break;
                    default:
                        CrestronConsole.ConsoleCommandResponse("Unknown device request:{0}", s);
                        break;
                }
            }, Key, "Commands to trigger device actions", ConsoleAccessLevelEnum.AccessOperator);
        }

        private void SetupPolling()
        {
            var poll = new CTimer(Poll, this, Timeout.Infinite);

            _commsMonitor.StatusChange += (sender, args) =>
            {
                if (args.Status == MonitorStatus.IsOk)
                {
                    poll.Reset(25, 10000);
                }
                else
                {
                    poll.Stop();
                }
            };

            PowerIsOnFeedback.OutputChange += (sender, args) =>
            {
                if (args.BoolValue)
                    poll.Reset(25, 10000);
            };
        }

        private void SetupFeedbacks()
        {
            PowerIsOnFeedback = new BoolFeedback("PowerIsOn", () => PowerIsOn);

            // Main volume range = 0 - 98
            VolumeLevelFeedback = new IntFeedback("Volume", () =>
                CrestronEnvironment.ScaleWithLimits(VolumeLevel, 980, 0, 65535, 0));

            MuteFeedback = new BoolFeedback("Mute", () => MuteIsOn);

            IsCoolingDownFeedback = new BoolFeedback("IsCoolingDown", () => _isCoolingDown);
            IsWarmingUpFeedback = new BoolFeedback("IsWarmingUp", () => _isWarmingUp);


            Feedbacks = new FeedbackCollection<Feedback>
            {
                PowerIsOnFeedback,
                MuteFeedback,
                IsOnline,
                IsCoolingDownFeedback,
                IsWarmingUpFeedback,
                VolumeLevelFeedback,
            };

            Feedbacks.Where(f => !string.IsNullOrEmpty(f.Key))
                .ToList()
                .ForEach(f =>
                {
                    if (f is StringFeedback)
                    {
                        f.OutputChange +=
                            (sender, args) => Debug.Console(1, this, "[{0}] - {1}", f.Key, args.StringValue);
                    }

                    if (f is IntFeedback)
                    {
                        f.OutputChange += (sender, args) => Debug.Console(1, this, "[{0}] - {1}", f.Key, args.IntValue);
                    }

                    if (f is BoolFeedback)
                    {
                        f.OutputChange += (sender, args) => Debug.Console(1, this, "[{0}] - {1}", f.Key, args.BoolValue);
                    }
                });
        }

        public override void Initialize()
        {
            _comms.Connect();
            _commsMonitor.Start();
        }

        private void OnSocketConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
        {
            Debug.Console(1, this, Debug.ErrorLogLevel.Notice, "Connection status updated:{0}", args.Client.ClientStatus);
        }

        private void OnGatherLineReceived(object sender, GenericCommMethodReceiveTextArgs args)
        {
            var rx = args.Text.Trim();

            _receiveQueue.Enqueue(new ProcessStringMessage(rx, ParseResponse));

        }

        private void ParseResponse(string rx)
        {
            if (rx.StartsWith("MVMAX"))
            {
                try
                {
                    var maxVolString = rx.Substring(5).Trim();
                    MaxVolLevel = int.Parse(maxVolString);
                }
                catch (Exception ex)
                {
                    Debug.Console(2, Debug.ErrorLogLevel.Notice, "Caught an exception parsing max volume response: {0}{1}",
                    rx, ex);
                }
            }
            else if (rx.StartsWith("MV"))
            {
                // MV80<CR>
                // TODO: Need to deal with 3 digit values that indicate half decibels
                try
                {
                    var volumeString = rx.Substring(2).Trim();
                    var level = int.Parse(volumeString);

                    // Multiply 2 digit values by 10
                    if (volumeString.Length <= 2) VolumeLevel = level * 10;
                    else VolumeLevel = level;

                }
                catch (Exception ex)
                {
                    Debug.Console(2, Debug.ErrorLogLevel.Notice, "Caught an exception parsing volume response: {0}{1}",
                        rx, ex);
                }
            }
            else if (rx.StartsWith("CV") && !rx.Contains("END"))
            {
                // CVFL 50<CR>
                // TODO: Need to deal with 3 digit values that indicate half decibels
                try
                {
                    var volumeString = rx.Substring(2).Trim();
                    var parts = volumeString.Split(new[] { ' ' });
                    var channelName = parts[0];
                    var response = parts[1];

                    MarantzChannelVolume channel;

                    var surroundChannel = MarantzSurroundChannel.Parse(channelName);
                    if (surroundChannel == SurroundChannel.Unknown)
                        return;

                    if (!_surroundChannels.TryGetValue(surroundChannel, out channel))
                    {
                        channel = new MarantzChannelVolume(channelName, this);
                        _surroundChannels.Add(surroundChannel, channel);

                        var handler = SurroundChannelsUpdated;
                        if (handler != null)
                            handler(this, EventArgs.Empty);
                    }

                    channel.ParseResponse(response);
                }
                catch (Exception ex)
                {
#if SERIES4
                    Debug.LogMessage(ex, "Caught an exception parsing channel volume response: {response}", this, rx);
#else
                    Debug.Console(2, Debug.ErrorLogLevel.Notice, "Caught an exception parsing volume response: {0}{1}",
                        rx, ex);
#endif
                }
            }
            else if (rx.StartsWith("SI"))
            {
                // SISAT/CBL<CR>
                try
                {
                    var input = rx.Substring(2).Trim();

                    if (Inputs.Items.ContainsKey(input))
                    {

                        foreach (var item in Inputs.Items)
                        {
                            item.Value.IsSelected = item.Key.Equals(input);
                        }
                    }

                    Inputs.CurrentItem = input;
                }
                catch (Exception ex)
                {
                    Debug.Console(2, Debug.ErrorLogLevel.Notice, "Caught an exception parsing input response: {0}{1}",
                        rx, ex);
                }
            }
            else if (rx.StartsWith("MS"))
            {
                // MSDOLBY PLII MV<CR>
                try
                {
                    var surroundMode = rx.Substring(2);

                    //Debug.Console(2, this, "surroundMode: {0}", surroundMode);

                    var matchString = surroundMode;

                    //Debug.Console(2, this, "matchString: {0}", matchString);

                    var mode = SurroundSoundModes.Items.FirstOrDefault
                        (x => matchString.StartsWith(((x.Value) as MarantzSurroundMode).MatchString));


                    if (mode.Value != null)
                    {
                        // must set this first, as the mode select will fire an event
                        SurroundSoundModes.CurrentItem = mode.Key;

                        foreach (var item in SurroundSoundModes.Items)
                        {
                            var isSelected = item.Key.Equals(mode.Key);
                            item.Value.IsSelected = isSelected;
                        }
;
                    }
                    else
                    {
                        SurroundSoundModes.CurrentItem = eSurroundModes.Unknown;
                        SurroundSoundModes.Items.All(x => x.Value.IsSelected = false);
                        Debug.Console(2, this, "Unknown Surround Mode: {0}", surroundMode);
                    }

                }
                catch (Exception ex)
                {
                    Debug.Console(2, Debug.ErrorLogLevel.Notice,
                        "Caught an exception parsing surround mode response: {0}{1}", rx, ex);
                }
            }
            else if (rx.StartsWith("Z2"))
            {
                if (_zone2 == null) return;

                _zone2.ParseRx(rx);
            }
            else
            {
                switch (rx)
                {
                    //POWER RESPONSES
                    case "ZMON": // alternate: PWON (affects the AVR device, all zones)
                        PowerIsOn = true;
                        break;
                    case "ZMOFF": // alternate: PWSTANDBY (affects the AVR device, all zones)
                        PowerIsOn = false;
                        break;

                    //MUTE RESPONSES
                    case "MUON":
                        MuteIsOn = true;
                        break;
                    case "MUOFF":
                        MuteIsOn = false;
                        break;
                }
            }
        }

        public void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var messageToSend = text.Trim() + CommsDelimiter;
            _comms.SendText(messageToSend);
        }

        private static void Poll(object state)
        {
            var device = (MarantzDevice)state;
            device.SendText("ZM?"); // alternate: PW? (query for AVR overall device power)

            if (!device.PowerIsOn) return;

            CrestronInvoke.BeginInvoke((o) =>
            {
                Thread.Sleep(100);
                device.SendText("MV?");
                Thread.Sleep(100);
                device.SendText("MU?");
                Thread.Sleep(100);
                device.SendText("MS?");
                Thread.Sleep(100);
                device.SendText("SI?");
                Thread.Sleep(100);
                device.SendText("Z2?");
                Thread.Sleep(100);
                device.SendText("CV?");

            });
        }

        public override void LinkToApi(BasicTriList trilist, uint joinStart, string joinMapKey, EiscApiAdvanced bridge)
        {
            var joinMap = new MarantzJoinMap(joinStart);

            // This adds the join map to the collection on the bridge
            if (bridge != null)
            {
                bridge.AddJoinMap(Key, joinMap);
            }

            var customJoins = JoinMapHelper.TryGetJoinMapAdvancedForDevice(joinMapKey);

            if (customJoins != null)
            {
                joinMap.SetCustomJoinData(customJoins);
            }

            Debug.Console(1, "Linking to Trilist '{0}'", trilist.ID.ToString("X"));
            Debug.Console(0, "Linking to Bridge Type {0}", GetType().Name);

            IsOnline.LinkInputSig(trilist.BooleanInput[joinMap.IsOnline.JoinNumber]);
            trilist.SetString(joinMap.DeviceName.JoinNumber, Name);
        }

        public BoolFeedback IsOnline
        {
            get { return _commsMonitor.IsOnlineFeedback; }
        }

        public StatusMonitorBase CommunicationMonitor
        {
            get { return _commsMonitor; }
        }


        public void PowerOn()
        {
            SendText("ZMON"); // alternate: PWON

            _isWarmingUp = true;
            IsWarmingUpFeedback.FireUpdate();

            _warmupTimer = new CTimer(o =>
            {
                _isWarmingUp = false;
                IsWarmingUpFeedback.FireUpdate();
            }, _warmingTimeMs);
        }

        public void PowerOff()
        {
            SendText("ZMOFF"); // alternate: PWSTANDBY

            _isCoolingDown = true;
            IsCoolingDownFeedback.FireUpdate();

            _cooldownTimer = new CTimer(o =>
            {
                _isCoolingDown = false;
                IsCoolingDownFeedback.FireUpdate();
            }, _coolingTimeMs);
        }

        public void PowerToggle()
        {
            if (PowerIsOn)
            {
                PowerOff();
            }
            else
            {
                PowerOn();
            }
        }

        public BoolFeedback PowerIsOnFeedback { get; private set; }

        private volatile bool _rampVolumeUp;

        public void VolumeUp(bool pressRelease)
        {
            if (_rampVolumeUp && pressRelease)
                return;

            _rampVolumeUp = pressRelease;

            if (!_rampVolumeUp) return;

            _rampVolumeDown = false;

            CrestronInvoke.BeginInvoke(RampVolumeUp, this);
        }

        private static void RampVolumeUp(object state)
        {
            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "ramping volume up...");

            var device = (MarantzDevice)state;

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Volume Level: {0}", device.VolumeLevel);

            using (var wh = new CEvent(true, false))
            {
                var newLevel = device.VolumeLevel;
                while (device._rampVolumeUp && newLevel < 980)
                {
                    newLevel += 5;
                    var request = MarantzUtils.VolumeCommand(newLevel, "MV");
                    device.SendText(request);
                    wh.Wait(device._rampRepeatTimeMs);
                }
            }
        }

        private volatile bool _rampVolumeDown;

        public void VolumeDown(bool pressRelease)
        {
            if (_rampVolumeDown && pressRelease)
                return;

            _rampVolumeDown = pressRelease;

            if (!_rampVolumeDown) return;

            _rampVolumeUp = false;

            CrestronInvoke.BeginInvoke(RampVolumeDown, this);
        }

        private static void RampVolumeDown(object state)
        {
            Debug.Console(2, "ramping volume down...");

            var device = (MarantzDevice)state;

            Debug.Console(2, "Volume Level: {0}", device.VolumeLevel);

            using (var wh = new CEvent(true, false))
            {
                var newLevel = device.VolumeLevel;
                while (device._rampVolumeDown && newLevel > 0)
                {
                    newLevel -= 5;
                    var request = MarantzUtils.VolumeCommand(newLevel, "MV");
                    device.SendText(request);
                    wh.Wait(device._rampRepeatTimeMs);
                }
            }
        }

        public void MuteToggle()
        {
            if (MuteIsOn)
            {
                MuteOff();
            }
            else
            {
                MuteOn();
            }
        }

        public void MuteOn()
        {
            SendText("MUON");
        }

        public void MuteOff()
        {
            SendText("MUOFF");
        }

        public void SetVolume(ushort level)
        {
            var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, 65535, 0, 980, 0);
            var request = MarantzUtils.VolumeCommand((int)desiredLevel, "MV");
            SendText(request);
        }

        public void SetInput(string input)
        {
            var inputToSend = "SI" + input.Trim().ToUpper();
            SendText(inputToSend);

            SendText("SI?");
        }

        public void SetSurroundSoundMode(string surroundMode)
        {
            var surroundModeToSend = "MS" + surroundMode.Trim().ToUpper();
            SendText(surroundModeToSend);

            SendText("MS?");
        }

        public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        //public StringFeedback CurrentInputFeedback { get; private set; }
        public StringFeedback CurrentSurroundModeStringFeedback { get; private set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            try
            {
                var inputToSend = (string)inputSelector;
                var zone = outputSelector as string ?? string.Empty;

                switch (zone)
                {
                    case "Z2":
                        break;
                    default:
                        SetInput(inputToSend);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.Console(1, Debug.ErrorLogLevel.Notice, "Caught an exception routing: {0}", ex);
            }
        }

        public FeedbackCollection<Feedback> Feedbacks { get; private set; }

        public event EventHandler SurroundChannelsUpdated;

        public Dictionary<SurroundChannel, IBasicVolumeWithFeedback> SurroundChannels
        {
            get
            {
                return _surroundChannels.ToDictionary(pair => pair.Key, pair => pair.Value as IBasicVolumeWithFeedback);
            }
        }

        public string CurrentSourceInfoKey
        {
            get
            {
                return _currentSourceListKey;
            }
            set
            {
                _currentSourceListKey = value;
            }
        }

        public SourceListItem CurrentSourceInfo
        {
            get
            {
                return _currentSourceItem;
            }
            set
            {
                if (value == _currentSourceItem) return;

                var handler = CurrentSourceChange;

                if (handler != null)
                    handler(_currentSourceItem, ChangeType.WillChange);

                _currentSourceItem = value;

                if (handler != null)
                    handler(_currentSourceItem, ChangeType.DidChange);
            }
        }

        private string _currentSourceListKey;

        private SourceListItem _currentSourceItem;

        public event SourceInfoChangeHandler CurrentSourceChange;

        public void ExecuteSwitch(object inputSelector)
        {

            var inputToSend = (string)inputSelector;

            try
            {
                if (_powerIsOn)
                {
                    SetInput(inputToSend);
                    return;
                }


                // One-time event handler to wait for power on before executing switch
                EventHandler<FeedbackEventArgs> handler = null; // necessary to allow reference inside lambda to handler
                handler = (o, a) =>
                {
                    if (_isWarmingUp)
                    {
                        return;
                    }

                    IsWarmingUpFeedback.OutputChange -= handler;
                    SetInput(inputToSend);
                };
                IsWarmingUpFeedback.OutputChange += handler; // attach and wait for on FB
                PowerOn();
            }
            catch (Exception ex)
            {
                Debug.Console(1, Debug.ErrorLogLevel.Notice, "Caught an exception routing: {0}", ex);
            }
        }

        public void UpdateDeviceInfo()
        {
            var socket = _comms as GenericTcpIpClient;
            if (socket == null)
                return;

            DeviceInfo = new DeviceInfo
            {
                FirmwareVersion = "",
                HostName = "",
                IpAddress = socket.Hostname,
                MacAddress = "",
                SerialNumber = ""
            };

            var handler = DeviceInfoChanged;
            if (handler == null) return;

            handler(this, new DeviceInfoEventArgs { DeviceInfo = DeviceInfo });
        }

        public DeviceInfo DeviceInfo { get; private set; }

        private int _warmingTimeMs = 5000;

        private int _coolingTimeMs = 2000;

        private bool _isWarmingUp;

        private bool _isCoolingDown;

        private CTimer _warmupTimer;

        private CTimer _cooldownTimer;

        public BoolFeedback IsWarmingUpFeedback { get; private set; }

        public BoolFeedback IsCoolingDownFeedback { get; private set; }

        public event DeviceInfoChangeHandler DeviceInfoChanged;
    }
}


// Useful consol commands for testing
// devjson:4 {"deviceKey":"avr", "methodName":"SendText", "params":["PW?"]}
// setdevicestreamdebug:4 avr-com both 120
// appdebug:4 2
// devjson:4 {"deviceKey":"avr", "methodName":"SetVolume", "params":[1000]}
// devjson:4 {"deviceKey":"avr", "methodName":"SendText", "params":["MV09"]}
// devjson:4 { "deviceKey":"avr", "methodName":"SendText", "params":["CVFL 50"]}
