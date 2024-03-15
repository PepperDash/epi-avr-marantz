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

namespace PDT.Plugins.Marantz
{
    public class ISurroundChannelsMessenger : MessengerBase
    {
        private readonly IHasSurroundChannels _surroundDevice;

        private CTimer _updateTimer;

        public ISurroundChannelsMessenger(string key, string messagePath, IHasSurroundChannels device) : base(key, messagePath, device)
        {
            _surroundDevice = device;

            _updateTimer = new CTimer(o =>
            {
                SendFullStatus();
            }, null, 0, 1000);
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

            // TODO: Add resettable timer here to only fire after a delay
            _surroundDevice.SurroundChannelsUpdated += (sender, args) =>
            {
                if (_updateTimer != null) 
                {
                    _updateTimer.Reset();
                    return;
                } else
                {
                    _updateTimer = new CTimer(o =>
                    {
                        SendFullStatus();
                    }, null, 0, 1000);
                }
            };
        }

        private void SendFullStatus()
        {
            if (_updateTimer != null)
            {
                _updateTimer.Stop();
                _updateTimer = null;
            }
            PostStatusMessage(JToken.FromObject(_surroundDevice));
        }
    }

}
