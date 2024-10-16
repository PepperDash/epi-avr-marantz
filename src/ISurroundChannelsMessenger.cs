using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Crestron.SimplSharp;
using PepperDash.Essentials.AppServer;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public class ISurroundChannelsMessenger : MessengerBase
    {
        private readonly IHasSurroundChannels _surroundDevice;

        private CTimer _updateTimer;

        public ISurroundChannelsMessenger(string key, string messagePath, IHasSurroundChannels device) : base(key, messagePath, device)
        {
            _surroundDevice = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) =>
            {
                SendFullStatus();
            });

            AddAction("/setDefaultChannelLevels", (id, content) =>
            {
                _surroundDevice.SetDefaultChannelLevels();
            });

            RegisterChannels();

            // TODO: Add resettable timer here to only fire after a delay
            _surroundDevice.SurroundChannelsUpdated += (sender, args) =>
            {
                RegisterChannels();
                SendFullStatus();

                //if (_updateTimer != null)
                //{
                //    _updateTimer.Reset();
                //    return;
                //}
                //else
                //{
                //    _updateTimer = new CTimer(o =>
                //    {
                //        RegisterChannels();
                //        Debug.Console(2, this, "*********Surround channels updated, sending full status*********");
                //        SendFullStatus();
                //    }, 1000);
                //}
            };
        }

        private void SendFullStatus()
        {
            var message = new LevelControlStateMessage
            {
                Levels = GetVolumeLevels()
            };
              
            PostStatusMessage(message);
        }

        private void RegisterChannels()
        {
            Debug.LogMessage(Serilog.Events.LogEventLevel.Debug, this, "Channel Count: {0}", _surroundDevice.SurroundChannels.Count);

            foreach (var levelControl in _surroundDevice.SurroundChannels)
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Verbose, this, "Registering channel: {0}", levelControl.Key);

                // reassigning here just in case of lambda closure issues
                var key = levelControl.Key.ToString();
                var control = levelControl.Value;

                AddAction($"/{key}/level", (id, content) =>
                {
                    var request = content.ToObject<MobileControlSimpleContent<ushort>>();

                    control.SetVolume(request.Value);
                });

                AddAction($"/{key}/muteToggle", (id, content) =>
                {
                    control.MuteToggle();
                });

                AddAction($"/{key}/muteOn", (id, content) => control.MuteOn());

                AddAction($"/{key}/muteOff", (id, content) => control.MuteOff());

                AddAction($"/{key}/volumeUp", (id, content) => PressAndHoldHandler.HandlePressAndHold(key, content, (b) => control.VolumeUp(b)));

                AddAction($"/{key}/volumeDown", (id, content) => PressAndHoldHandler.HandlePressAndHold(key, content, (b) => control.VolumeDown(b)));

                control.VolumeLevelFeedback.OutputChange += (o, a) => PostStatusMessage(JToken.FromObject(new
                {
                    levelControls = GetVolumeLevels()
                }));

                control.MuteFeedback.OutputChange += (o, a) => PostStatusMessage(JToken.FromObject(new
                {
                    levelControls = GetVolumeLevels()
                }));
            }

        }

        private Dictionary<string, Volume> GetVolumeLevels()
        {

            return _surroundDevice.SurroundChannels.ToDictionary(
                kvp => kvp.Key.ToString(), kvp =>
                    new Volume
                    {
                        Level = kvp.Value.VolumeLevelFeedback.IntValue,
                        Muted = kvp.Value.MuteFeedback.BoolValue,
                        Label = kvp.Key.ToString(),
                        RawValue = (kvp.Value as MarantzChannelVolume).RawVolumeLevel.ToString(),
                        Units = eVolumeLevelUnits.Decibels
                    });
 
        }
    }

}
