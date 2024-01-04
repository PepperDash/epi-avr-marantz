// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using System;
using System.Collections.Generic;
using System.Linq;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;
using Feedback = PepperDash.Essentials.Core.Feedback;

namespace PDT.Plugins.Marantz
{
    public class MarantzDevice : EssentialsBridgeableDevice, IOnline, IHasPowerControlWithFeedback, IBasicVolumeWithFeedback, IRouting, ICommunicationMonitor, IHasFeedback
    {
        private readonly IBasicCommunication _comms;
		private readonly GenericCommunicationMonitor _commsMonitor;
        private readonly IDictionary<string, MarantzChannelVolume> _channelVolumes;

		private const string CommsDelimiter = "\r";

	    private bool _powerIsOn;
	    public bool PowerIsOn
	    {
	        get { return _powerIsOn; }
            set { _powerIsOn = value; PowerIsOnFeedback.FireUpdate(); }
	    }

        private bool _muteIsOn;
        public bool MuteIsOn
        {
            get { return _muteIsOn; }
            set { _muteIsOn = value; MuteFeedback.FireUpdate(); }
        }

        private int _volumeLevel;
        public int VolumeLevel
        {
            get { return _volumeLevel; }
            set { _volumeLevel = value; VolumeLevelFeedback.FireUpdate(); }
        }

        private string _currentInput;
        public string CurrentInput
        {
            get { return _currentInput; }
            set { _currentInput = value; CurrentInputFeedback.FireUpdate(); }
        }

        private string _currentSurroundMode;
        public string CurrentSurroundMode
        {
            get { return _currentSurroundMode; }
            set { _currentSurroundMode = value; CurrentInputFeedback.FireUpdate(); }
        }

        public MarantzDevice(string key, string name, MarantzProps config, IBasicCommunication comms)
			: base(key, name)
        {
            _channelVolumes = new Dictionary<string, MarantzChannelVolume>(StringComparer.OrdinalIgnoreCase);

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
                new RoutingInputPort("SAT/CBL", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "SAT/CBL",
                    this),
                new RoutingInputPort("MPLAY", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "MPLAY", this),
                new RoutingInputPort("GAME", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "GAME", this),
                new RoutingInputPort("TUNER", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "TUNER", this),
                new RoutingInputPort("HDRADIO", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "HDRADIO", this),
                new RoutingInputPort("PHONO", eRoutingSignalType.Audio, eRoutingPortConnectionType.LineAudio, "PHONO",
                    this),
                new RoutingInputPort("AUX1", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX1", this),
                new RoutingInputPort("AUX2", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX2", this),
                new RoutingInputPort("AUX3", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX3", this),
                new RoutingInputPort("AUX4", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX4", this),
                new RoutingInputPort("AUX5", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX5", this),
                new RoutingInputPort("AUX6", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX6", this),
                new RoutingInputPort("AUX7", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "AUX7", this),
                new RoutingInputPort("NET", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Streaming, "NET", this),
                new RoutingInputPort("BT", eRoutingSignalType.Audio, eRoutingPortConnectionType.Streaming, "BT", this),
            };

            OutputPorts = new RoutingPortCollection<RoutingOutputPort>
            {
                new RoutingOutputPort("ZONE1", eRoutingSignalType.AudioVideo, eRoutingPortConnectionType.Hdmi, "", this)
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
                    poll.Reset(25, 2000);
                }
                else
                {
                    poll.Stop();
                }
            };

            PowerIsOnFeedback.OutputChange += (sender, args) =>
            {
                if (args.BoolValue)
                    poll.Reset(25, 2000);
            };
        }

        private void SetupFeedbacks()
        {

            PowerIsOnFeedback = new BoolFeedback("Power", () => PowerIsOn);

            // Main volume range = 0 - 98
            VolumeLevelFeedback = new IntFeedback("Volume", () =>
                CrestronEnvironment.ScaleWithLimits(VolumeLevel, 98, 0, int.MaxValue, int.MinValue));

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
                        f.OutputChange += (sender, args) => Debug.Console(1, this, "[{0}] - {1}", f.Key, args.StringValue);
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

		    if (rx.StartsWith("MV"))
		    {
                // MV80<CR>
		        try
                {
                    var volumeString = rx.TrimStart(new[] { 'M', 'V' });
                    VolumeLevel = int.Parse(volumeString);
		        }
		        catch (Exception ex)
		        {
		            Debug.Console(1, Debug.ErrorLogLevel.Notice, "Caught an exception parsing volume response: {0}{1}", rx, ex);
		        }
		    }
            else if (rx.StartsWith("CV"))
            {
                // CVFL 50<CR>
                try
                {
                    var volumeString = rx.TrimStart(new[] { 'C', 'V' });
                    var parts = volumeString.Split(new[] {' '});
                    var channelName = parts[0];
                    var response = parts[1];

                    MarantzChannelVolume channel;

                    if (!_channelVolumes.TryGetValue(channelName, out channel))
                    {
                        channel = new MarantzChannelVolume(channelName, this);
                        _channelVolumes.Add(channelName, channel);
                    }

                    channel.ParseResponse(response);
                }
                catch (Exception ex)
                {
                    Debug.Console(1, Debug.ErrorLogLevel.Notice, "Caught an exception parsing channel volume response: {0}{1}", rx, ex);
                }
            }
            else if (rx.StartsWith("SI"))
            {
                // SISAT/CBL<CR>
                try
                {
                    var input = rx.TrimStart(new[] { 'S', 'I' });
                    CurrentInput = input;
                }
                catch (Exception ex)
                {
                    Debug.Console(1, Debug.ErrorLogLevel.Notice, "Caught an exception parsing input response: {0}{1}", rx, ex);
                }
            }
            else if (rx.StartsWith("MS"))
            {
                // MSDOLBY PLII MV<CR>
                try
                {
                    var surroundMode = rx.TrimStart(new[] { 'M', 'S' });
                    CurrentSurroundMode = surroundMode;
                }
                catch (Exception ex)
                {
                    Debug.Console(1, Debug.ErrorLogLevel.Notice, "Caught an exception parsing surround mode response: {0}{1}", rx, ex);
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

	        device.SendText("MU?");
	        device.SendText("MS?");
            device.SendText("SI?");
            device.SendText("CV?");
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
            var device = (MarantzDevice)state;

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
            var device = (MarantzDevice)state;

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

        public void SetChannelVolume(string channelName, ushort level)
        {
            var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, uint.MaxValue, uint.MinValue, 62, 38);
            var request = MarantzUtils.ChannelVolumeCommand(channelName, (int)desiredLevel);
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

        public IEnumerable<string> KnownChannels()
        {
            return _channelVolumes.Keys;
        }

        public IntFeedback GetChannelVolume(string channelName)
        {
            MarantzChannelVolume channel;

            if (!_channelVolumes.TryGetValue(channelName, out channel))
            {
                channel = new MarantzChannelVolume(channelName, this);
                _channelVolumes.Add(channelName, channel);
            }

            return channel.VolumeLevelFeedback;
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
    }
}

