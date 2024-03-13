using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using PepperDash.Essentials.Core.DeviceInfo;
using PepperDash.Essentials.Core.Queues;
using Feedback = PepperDash.Essentials.Core.Feedback;
using Crestron.SimplSharpPro.CrestronThread;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;

// TODO: Add IHasInputs and IInputs back into this repo for 3-series compatibility

#if SERIES4
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#else
using PDT.Plugins.Marantz.Interfaces;
#endif
namespace PDT.Plugins.Marantz
{
    public class MarantzDevice : EssentialsBridgeableDevice, 
        IOnline, 
        IHasPowerControlWithFeedback,
        IBasicVolumeWithFeedback, 
        IRouting, 
        ICommunicationMonitor, 
        IHasFeedback,
        IHasSurroundChannels,
        IHasInputs,
        IRoutingSinkWithSwitching,
        IDeviceInfoProvider
    {
        private readonly IBasicCommunication _comms;
        private readonly GenericCommunicationMonitor _commsMonitor;
        private readonly IDictionary<string, MarantzInput> _inputs;
        private readonly IDictionary<SurroundChannel, MarantzChannelVolume> _surroundChannels;

        private readonly GenericQueue _receiveQueue;

        private const string CommsDelimiter = "\r";

        private bool _powerIsOn;

        public bool PowerIsOn
        {
            get { return _powerIsOn; }
            set
            {
                if(value == _powerIsOn)
                    return;
                _powerIsOn = value;
                PowerIsOnFeedback.FireUpdate();
            }
        }

        private bool _muteIsOn;

        public bool MuteIsOn
        {
            get { return _muteIsOn; }
            set
            {
                if (value == _muteIsOn)
                    return;
                _muteIsOn = value;
                MuteFeedback.FireUpdate();
            }
        }

        private int _volumeLevel;

        public int VolumeLevel
        {
            get { return _volumeLevel; }
            set
            {
                if (value == _volumeLevel)
                    return;
                _volumeLevel = value;
                Debug.Console(2, this, " Volume Level: {0}", _volumeLevel);
                VolumeLevelFeedback.FireUpdate();
            }
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

        private string _currentInput;

        public string CurrentInput
        {
            get { return _currentInput; }
            set
            {
                if (value == _currentInput)
                    return;
                _currentInput = value;
                CurrentInputFeedback.FireUpdate();
            }
        }

        private string _currentSurroundMode;

        public string CurrentSurroundMode
        {
            get { return _currentSurroundMode; }
            set
            {   
                if (value == _currentSurroundMode)
                    return;
                _currentSurroundMode = value;
                CurrentInputFeedback.FireUpdate();
            }
        }

        public MarantzDevice(string key, string name, MarantzProps config, IBasicCommunication comms)
            : base(key, name)
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

            _inputs = new Dictionary<string, MarantzInput>
            {
                {"DVD", new MarantzInput("DVD", "DVD", this, "DVD")},
                {"BD", new MarantzInput("BD", "BD", this, "BD")},
                {"TV", new MarantzInput("TV", "TV", this, "TV")},
                {"SATCBL", new MarantzInput("SATCBL", "SAT/CBL", this, "SAT/CBL")},
                {"MPLAY", new MarantzInput("MPLAY", "MPLAY", this, "MPLAY")},
                {"GAME", new MarantzInput("GAME", "GAME", this, "GAME")},
                {"AUX1", new MarantzInput("AUX1", "AUX1", this, "AUX1")},
                {"AUX2", new MarantzInput("AUX2", "AUX2", this, "AUX2")},
                {"AUX3", new MarantzInput("AUX3", "AUX3", this, "AUX3")},
                {"AUX4", new MarantzInput("AUX4", "AUX4", this, "AUX4")},
                {"AUX5", new MarantzInput("AUX5", "AUX5", this, "AUX5")},
                {"AUX6", new MarantzInput("AUX6", "AUX6", this, "AUX6")},
                {"AUX7", new MarantzInput("AUX7", "AUX7", this, "AUX7")},
                {"CD", new MarantzInput("CD", "CD", this, "CD")},
            };

            SetupGather();

            SetupRoutingPorts();

            SetupFeedbacks();

            SetupPolling();

            SetupConsoleCommands();
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
                new RoutingOutputPort("ZONE1", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "", this),
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
                        CrestronConsole.ConsoleCommandResponse("{0}", CurrentInput);
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
            PowerIsOnFeedback = new BoolFeedback("Power", () => PowerIsOn);

            // Main volume range = 0 - 98
            VolumeLevelFeedback = new IntFeedback("Volume", () =>
                CrestronEnvironment.ScaleWithLimits(VolumeLevel, 98, 0, 65535, 0));

            MuteFeedback = new BoolFeedback("Mute", () => MuteIsOn);

            CurrentInputFeedback = new StringFeedback("Input", () => CurrentInput);

            CurrentSurroundModeStringFeedback = new StringFeedback("Surround Mode", () => CurrentSurroundMode);

            Feedbacks = new FeedbackCollection<Feedback>
            {
                PowerIsOnFeedback,
                MuteFeedback,
                CurrentInputFeedback,
                IsOnline,
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
                    var maxVolString = rx.TrimStart(new[] { 'M', 'V', 'M', 'A', 'X' });
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
                try
                { 
                    var volumeString = rx.TrimStart(new[] {'M', 'V'});
                    VolumeLevel = int.Parse(volumeString);
                }
                catch (Exception ex)
                {
                    Debug.Console(2, Debug.ErrorLogLevel.Notice, "Caught an exception parsing volume response: {0}{1}",
                        rx, ex);
                }
            }
            else if (rx.StartsWith("CV"))
            {
                // CVFL 50<CR>
                try
                {
                    var volumeString = rx.TrimStart(new[] {'C', 'V'});
                    var parts = volumeString.Split(new[] {' '});
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
                    Debug.Console(2, Debug.ErrorLogLevel.Notice,
                        "Caught an exception parsing channel volume response: {0}{1}", rx, ex);
                }
            }
            else if (rx.StartsWith("SI"))
            {
                // SISAT/CBL<CR>
                try
                {
                    var input = rx.TrimStart(new[] {'S', 'I'});

                    if (_inputs.ContainsKey(input))
                    {
                        foreach (var item in _inputs)
                        {
                            item.Value.IsSelected = item.Key.Equals(input);
                        }

                        var handler = InputsUpdated;
                        if (handler != null)
                            handler(this, EventArgs.Empty);
                    }

                    CurrentInput = input;
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
                    var surroundMode = rx.TrimStart(new[] {'M', 'S'});
                    CurrentSurroundMode = surroundMode;
                }
                catch (Exception ex)
                {
                    Debug.Console(2, Debug.ErrorLogLevel.Notice,
                        "Caught an exception parsing surround mode response: {0}{1}", rx, ex);
                }
            }
            else
            {
                switch (rx)
                {
                        //POWER RESPONSES
                    case "PWON":
                        PowerIsOn = true;
                        break;
                    case "PWSTANDBY":
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
            var device = (MarantzDevice) state;
            device.SendText("PW?");

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
                device.SendText("CV?");
                Thread.Sleep(100);
                device.SendText("Z2?");
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
            SendText("PWON");
        }

        public void PowerOff()
        {
            SendText("PWSTANDBY");
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
            var device = (MarantzDevice) state;

            using (var wh = new CEvent(true, false))
            {
                var level = device.VolumeLevel;
                while (device._rampVolumeUp && level < 99)
                {
                    var newLevel = ++level;
                    var request = MarantzUtils.VolumeCommand(newLevel);
                    device.SendText(request);
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
            var device = (MarantzDevice) state;

            using (var wh = new CEvent(true, false))
            {
                var level = device.VolumeLevel;
                while (device._rampVolumeDown && level > 1)
                {
                    --level;
                    var request = MarantzUtils.VolumeCommand(level);
                    device.SendText(request);
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
            SendText("MUON");
        }

        public void MuteOff()
        {
            SendText("MUOFF");
        }

        public void SetVolume(ushort level)
        {
            var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, uint.MaxValue, uint.MinValue, 98, 0);
            var request = MarantzUtils.VolumeCommand((int) desiredLevel);
            SendText(request);
        }

        public void SetInput(string input)
        {
            var inputToSend = "SI" + input.Trim().ToUpper();
            SendText(inputToSend);
        }

        public void SetSurroundMode(string surroundMode)
        {
            var surroundModeToSend = "MS" + surroundMode.Trim().ToUpper();
            SendText(surroundModeToSend);
        }

        public BoolFeedback MuteFeedback { get; private set; }
        public IntFeedback VolumeLevelFeedback { get; private set; }
        public StringFeedback CurrentInputFeedback { get; private set; }
        public StringFeedback CurrentSurroundModeStringFeedback { get; private set; }

        public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
        public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

        public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
        {
            try
            {
                var inputToSend = (string) inputSelector;
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

        public IDictionary<SurroundChannel, IBasicVolumeWithFeedback> Channels
        {
            get
            {           
                return _surroundChannels.ToDictionary(pair => pair.Key, pair => pair.Value as IBasicVolumeWithFeedback);
            }
        }

        public event EventHandler InputsUpdated;

        public IDictionary<string, IInput> Inputs
        {
            get { return _inputs.ToDictionary(pair => pair.Key, pair => pair.Value as IInput); }
        }

        public string CurrentSourceInfoKey { get
            {
                return CurrentSourceInfo.SourceListKey;
            }
            set
            {
                // TODO: Get rid of this setter once the interface definition is updated to remove the setter requirement.
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

        private SourceListItem _currentSourceItem;

        public event SourceInfoChangeHandler CurrentSourceChange;

        public void ExecuteSwitch(object inputSelector)
        {
            try
            {
                var inputToSend = (string)inputSelector;
                SetInput(inputToSend);
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

        public event DeviceInfoChangeHandler DeviceInfoChanged;
    }
}

// devjson:4 {"deviceKey":"avr", "methodName":"SendText", "params":["PW?"]}
// setdevicestreamdebug:4 avr-com both 120
