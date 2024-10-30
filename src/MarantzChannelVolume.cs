using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public class MarantzChannelVolume : IKeyed, IBasicVolumeWithFeedback
    {
        private readonly string _channelName;
        private readonly MarantzDevice _parent;

        private int _lastVolume;
        private int _volumeLevel;
        public int VolumeLevel
        {
            get { return _volumeLevel; }
            set {
                if(value == _volumeLevel) return;

                _volumeLevel = value;
                VolumeLevelFeedback.FireUpdate(); 
            }
        }


        /// <summary>
        /// The raw decimal volume of the channel from -12 (-120) to 12 (120) in decibels
        /// </summary>
        public int RawVolumeLevel { get
            {
                return CrestronEnvironment.ScaleWithLimits(VolumeLevel, 620, 380, 120, -120);
            } 
        }

        public MarantzChannelVolume(string channelName, MarantzDevice parent)
        {
            _channelName = channelName;
            _parent = parent;

            Key = parent.Key + "-" + channelName;

            // Main volume range = 38 - 62 | 0db = 50
            VolumeLevelFeedback = new IntFeedback(_channelName + "-Volume", () =>
                CrestronEnvironment.ScaleWithLimits(VolumeLevel, 620, 380, 65535, 0));

            MuteFeedback = new BoolFeedback(_channelName + "-Mute", () => VolumeLevelFeedback.IntValue == 0);

            VolumeLevelFeedback.OutputChange += (sender, args) => MuteFeedback.FireUpdate();
        }

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
            var device = (MarantzChannelVolume)state;

            using (var wh = new CEvent(true, false))
            {
                var level = device.VolumeLevel;
                while (device._rampVolumeUp && level < 620)
                {
                    var newLevel = level + 5;
                    var request = MarantzUtils.ChannelVolumeCommand(device._channelName, newLevel);
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
            var device = (MarantzChannelVolume)state;

            using (var wh = new CEvent(true, false))
            {
                var level = device.VolumeLevel;
                while (device._rampVolumeDown && level > 380)
                {
                    var newLevel = level - 5;
                    var request = MarantzUtils.ChannelVolumeCommand(device._channelName, newLevel);
                    device._parent.SendText(request);
                    wh.Wait(50);
                }
            }
        }

        public void MuteOn()
        {
            _lastVolume = VolumeLevelFeedback.IntValue;
            SetVolume(0);
        }

        public void MuteOff()
        {
            SetVolume((ushort)_lastVolume);
        }

        public void MuteToggle()
        {
            if (MuteFeedback.BoolValue)
            {
                MuteOff();
            }
            else
            {
                MuteOn();
            }
        }

        public void SetVolume(ushort level)
        {
            var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, 65535, 0, 620, 380);
            var request = MarantzUtils.ChannelVolumeCommand(_channelName, (int)desiredLevel);
            _parent.SendText(request);
        }

        public BoolFeedback MuteFeedback { get; private set; }

        public IntFeedback VolumeLevelFeedback { get; private set; }

        public void ParseResponse(string response)
        {
            var level = int.Parse(response);

            if (response.Length <= 2) VolumeLevel = level * 10;
            else VolumeLevel = level;
        }

        public string Key { get; private set; }
    }
}