using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.AppServer.Messengers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PDT.Plugins.Marantz
{
    public class ISurroundChannelsMessenger : MessengerBase
    {
        private readonly IHasSurroundChannels _surroundDevice;

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

            _surroundDevice.SurroundChannelsUpdated += (sender, args) =>
            {
                SendFullStatus();
            };
        }

        private void SendFullStatus()
        {
            PostStatusMessage(JToken.FromObject(_surroundDevice.Channels));
        }
    }

}
