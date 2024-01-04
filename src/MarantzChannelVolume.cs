using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public class MarantzChannelVolume : IKeyed
    {
        private readonly string _channelName;
        private readonly MarantzDevice _parent;

        private int _volumeLevel;
        public int VolumeLevel
        {
            get { return _volumeLevel; }
            set { _volumeLevel = value; VolumeLevelFeedback.FireUpdate(); }
        }

        public MarantzChannelVolume(string channelName, MarantzDevice parent)
        {
            _channelName = channelName;
            _parent = parent;

            Key = parent.Key + "-" + channelName;

            // Main volume range = 38 - 62 | 0db = 50
            VolumeLevelFeedback = new IntFeedback(_channelName + "-Volume", () =>
                CrestronEnvironment.ScaleWithLimits(VolumeLevel, 62, 38, int.MaxValue, int.MinValue));
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
                while (device._rampVolumeUp && level < 62)
                {
                    ++level;
                    var request = MarantzUtils.ChannelVolumeCommand(device._channelName, level);
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
                while (device._rampVolumeDown && level > 38)
                {
                    --level;
                    var request = MarantzUtils.ChannelVolumeCommand(device._channelName, level);
                    device._parent.SendText(request);
                    wh.Wait(50);
                }
            }
        }

        public void SetVolume(ushort level)
        {
            var desiredLevel = CrestronEnvironment.ScaleWithLimits(level, uint.MaxValue, uint.MinValue, 62, 38);
            var request = MarantzUtils.ChannelVolumeCommand(_channelName, (int)desiredLevel);
            _parent.SendText(request);
        }

        public IntFeedback VolumeLevelFeedback { get; private set; }

        public void ParseResponse(string response)
        {
            VolumeLevel = int.Parse(response);
        }

        public string Key { get; private set; }
    }
}