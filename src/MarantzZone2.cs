using PepperDash.Essentials.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using Crestron.SimplSharp;
using PepperDash.Core;


namespace PDT.Plugins.Marantz
{
    public class MarantzZone2 : EssentialsDevice,
        IHasPowerControlWithFeedback,
        IBasicVolumeWithFeedbackAdvanced,
        IHasInputs<string>,
        IWarmingCooling
    {
        private MarantzDevice _parent;

        public ISelectableItems<string> Inputs { get; private set; }


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
                    //CurrentSourceInfoKey = null;
                    //CurrentSourceInfo = null;

                    MuteIsOn = false;

                    foreach (var item in Inputs.Items)
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
            private set
            {
                _maxVolLevel = value;
            }
        }

        private int _minVolLevel;
        public int MinVolLevel
        {
            get { return _minVolLevel; }
            private set
            {
                _minVolLevel = value;
            }
        }

        public MarantzZone2(
            string key,
            string name,
            MarantzDevice parent)
            : base(key, name)
        {
            _parent = parent;

            SetupInputs();

            SetupConsoleCommands();

            SetupFeedbacks();
        }

        private void SetupInputs()
        {
            Inputs = new MarantzInputs
            {
                Items = new Dictionary<string, ISelectableItem>
                {
                    {"SOURCE", new MarantzZone2Input("SOURCE", "Main Source", this, "SOURCE")},
                    {"DVD", new MarantzZone2Input("DVD", "DVD", this, "DVD")},
                    {"BD", new MarantzZone2Input("BD", "BD", this, "BD")},
                    {"TV", new MarantzZone2Input("TV", "TV", this, "TV")},
                    {"SAT/CBL", new MarantzZone2Input("SAT/CBL", "SAT/CBL", this, "SAT/CBL")},
                    {"MPLAY", new MarantzZone2Input("MPLAY", "MPLAY", this, "MPLAY")},
                    {"GAME", new MarantzZone2Input("GAME", "GAME", this, "GAME")},
                    {"8K", new MarantzZone2Input("8K", "8K", this, "8K")},
                    {"AUX1", new MarantzZone2Input("AUX1", "AUX1", this, "AUX1")},
                    {"AUX2", new MarantzZone2Input("AUX2", "AUX2", this, "AUX2")},
                    //{"AUX3", new MarantzZone2Input("AUX3", "AUX3", this, "AUX3")},
                    //{"AUX4", new MarantzZone2Input("AUX4", "AUX4", this, "AUX4")},
                    //{"AUX5", new MarantzZone2Input("AUX5", "AUX5", this, "AUX5")},
                    //{"AUX6", new MarantzZone2Input("AUX6", "AUX6", this, "AUX6")},
                    //{"AUX7", new MarantzZone2Input("AUX7", "AUX7", this, "AUX7")},
                    {"CD", new MarantzZone2Input("CD", "CD", this, "CD")},
                    {"NET", new MarantzZone2Input("NET", "NET", this, "NET")},
                    {"BT", new MarantzZone2Input("BT", "BT", this, "BT")},
                }
            };
        }

        private void SetupConsoleCommands()
        {
            CrestronConsole.AddNewConsoleCommand(s =>
            {
                var request = s.Trim().ToLower();
                switch (request)
                {
                    case "z2poweron":
                        PowerOn();
                        break;
                    case "z2poweroff":
                        PowerOff();
                        break;
                    case "z2input?":
                        CrestronConsole.ConsoleCommandResponse("{0}", Inputs.CurrentItem);
                        break;
                    case "z2power?":
                        CrestronConsole.ConsoleCommandResponse("{0}", PowerIsOn);
                        break;
                    default:
                        CrestronConsole.ConsoleCommandResponse("Unknown device request:{0}", s);
                        break;
                }
            }, Key, "Commands to trigger device actions", ConsoleAccessLevelEnum.AccessOperator);
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

        internal void ParseRx(string rx)
        {
            switch (rx)
            {
                case "Z2ON":
                    PowerIsOn = true;
                    break;
                case "Z2OFF":
                    PowerIsOn = false;
                    break;
                case "Z2MUON":
                    MuteIsOn = true;
                    break;
                case "Z2MUOFF":
                    MuteIsOn = false;
                    break;
                default:
                    {
                        // Check for volume level by trying to parse as int
                        var volumeString = rx.Substring(2).Trim();
                        if (int.TryParse(volumeString, out int level))
                        {
                            // Multiply 2 digit values by 10
                            if (volumeString.Length <= 2) VolumeLevel = level * 10;
                            else VolumeLevel = level;
                        }
                        else
                        {
                            // if not volume level, treat as input response
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
                        break;
                    }
            }
        }

        public void PowerOn()
        {
            _parent.SendText("Z2ON");

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
            _parent.SendText("Z2OFF");

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

            var device = (MarantzZone2)state;

            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, "Volume Level: {0}", device.VolumeLevel);

            using (var wh = new CEvent(true, false))
            {
                var level = device.VolumeLevel;
                while (device._rampVolumeUp && level < 980)
                {
                    var newLevel = level + 5;
                    var request = MarantzUtils.VolumeCommand(newLevel, "Z2");
                    device._parent.SendText(request);
                    wh.Wait(50);
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

            var device = (MarantzZone2)state;

            Debug.Console(2, "Volume Level: {0}", device.VolumeLevel);

            using (var wh = new CEvent(true, false))
            {
                var level = device.VolumeLevel;
                while (device._rampVolumeDown && level > 0)
                {
                    var newLevel = level - 5;
                    var request = MarantzUtils.VolumeCommand(newLevel, "Z2");
                    device._parent.SendText(request);
                    wh.Wait(50);
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
            _parent.SendText("Z2MUON");
        }

        public void MuteOff()
        {
            _parent.SendText("Z2MUOFF");
        }

        public void SetVolume(ushort level)
        {
            var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, 65535, 0, 980, 0);
            var request = MarantzUtils.VolumeCommand((int)desiredLevel, "Z2");
            _parent.SendText(request);
        }

        public void SetInput(string input)
        {
            var inputToSend = "Z2" + input.Trim().ToUpper();
            _parent.SendText(inputToSend);

            _parent.SendText("Z2?");
        }

        public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }

        public FeedbackCollection<Feedback> Feedbacks { get; private set; }

        private int _warmingTimeMs = 5000;

        private int _coolingTimeMs = 2000;

        private bool _isWarmingUp;

        private bool _isCoolingDown;

        private CTimer _warmupTimer;

        private CTimer _cooldownTimer;


        public BoolFeedback IsWarmingUpFeedback { get; private set; }

        public BoolFeedback IsCoolingDownFeedback { get; private set; }
    }
}
