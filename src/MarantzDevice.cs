// For Basic SIMPL# Classes
// For Basic SIMPL#Pro classes

using System;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DeviceSupport;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Bridges;

namespace PDT.Plugins.Marantz
{
	public class MarantzDevice : EssentialsBridgeableDevice, IOnline, IHasPowerControlWithFeedback, IBasicVolumeWithFeedback, IRouting, ICommunicationMonitor
    {
		private readonly GenericCommunicationMonitor _commsMonitor;
	    private readonly IBasicCommunication _comms;

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

        public MarantzDevice(string key, string name, MarantzProps config, IBasicCommunication comms)
			: base(key, name)
        {
            _comms = comms;

		    var monitorConfig = config.Monitor ?? new CommunicationMonitorConfig()
		    {
                PollString = "PW?" + CommsDelimiter,
                PollInterval = 300000,
		        TimeToWarning = 60000,
		        TimeToError = 120000
		    };

            _commsMonitor = new GenericCommunicationMonitor(this, comms, monitorConfig);

            var socket = comms as ISocketStatus;
			if (socket != null)
			{
                socket.ConnectionChange += OnSocketConnectionChange;
            }

            var gather = new CommunicationGather(comms, CommsDelimiter);
            gather.LineReceived += OnGatherLineReceived;

            PowerIsOnFeedback = new BoolFeedback("Power", () => PowerIsOn);
            VolumeLevelFeedback = new IntFeedback("Volume", () => CrestronEnvironment.ScaleWithLimits(VolumeLevel, 100, 0, int.MaxValue, int.MinValue));
            MuteFeedback = new BoolFeedback("Mute", () => MuteIsOn);

            var poll = new CTimer(s =>
            {
                var device = (MarantzDevice) s;
                device.SendText("PW?");
                device.SendText("MU?");
                device.SendText("MS?");
                device.SendText("SI?");
            }, this, Timeout.Infinite);

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

        public override void Initialize()
        {
            _commsMonitor.Start();
        }

		private void OnSocketConnectionChange(object sender, GenericSocketStatusChageEventArgs args)
		{
		}

		private void OnGatherLineReceived(object sender, GenericCommMethodReceiveTextArgs args)
		{
		    var rx = args.Text.Trim();

		    if (rx.StartsWith("MV"))
		    {
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
		    else
		    {
                switch (rx)
                {
                    case "PWON":
                        PowerIsOn = true;
                        break;
                    case "PWSTANDBY":
                        PowerIsOn = false;
                        break;
                    case "MUON":
                        MuteIsOn = true;
                        break;
                    case "MUOFF":
                        MuteIsOn = false;
                        break;
                    case "SIPHONO":
                        break;
                    case "SICD":
                        break;
                    case "SIDVD":
                        break;
                    case "SITV":
                        break;
                    case "SISAT/CBL":
                        break;
                    case "SIMPLAY":
                        break;
                    case "SIGAME":
                        break;
                    case "SITUNER":
                        break;
                    case "SIRADIO":
                        break;
                    case "SISIRIUSXM":
                        break;
                    case "SIPANDORA":
                        break;
                    case "SIPAUX1":
                        break;
                    case "SIPAUX2":
                        break;
                    case "SIPAUX3":
                        break;
                    case "SIPAUX4":
                        break;
                    case "SIPAUX5":
                        break;
                    case "MSAUTO":
                        break;
                    case "MSSTEREO":
                        break;
                    case "MSDOLBYDIGITAL":
                        break;
                    case "MSDTS SURROUND":
                        break;
                    default:
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

	        CrestronInvoke.BeginInvoke(state =>
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
	        }, this);
	    }

        private volatile bool _rampVolumeDown;
	    public void VolumeDown(bool pressRelease)
	    {
            if (_rampVolumeDown && pressRelease)
                return;

            _rampVolumeDown = pressRelease;

	        if (!_rampVolumeDown) return;

	        _rampVolumeUp = false;

	        CrestronInvoke.BeginInvoke(state =>
	        {
                var device = (MarantzDevice) state;
	            using (var wh = new CEvent(true, false))
                {
                    var level = device.VolumeLevel;
                    while (device._rampVolumeUp && level > 1)
	                {
                        var newLevel = --level;
	                    var request = MarantzUtils.VolumeCommand(newLevel);
                        device.SendText(request);
	                    wh.Wait(50);
	                }
	            }
	        }, this);
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
	        var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, uint.MaxValue, uint.MinValue, 100, 0);
	        var request = MarantzUtils.VolumeCommand((int) desiredLevel);
            SendText(request);
	    }

	    public BoolFeedback MuteFeedback { get; private set; }
	    public IntFeedback VolumeLevelFeedback { get; private set; }

	    public RoutingPortCollection<RoutingInputPort> InputPorts { get; private set; }
	    public RoutingPortCollection<RoutingOutputPort> OutputPorts { get; private set; }

	    public void ExecuteSwitch(object inputSelector, object outputSelector, eRoutingSignalType signalType)
	    {
	        throw new NotImplementedException();
	    }
    }
}

