using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.AppServer.Messengers;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;

namespace PDT.Plugins.Marantz
{
    public class SurroundModesMessenger<TKey> : MessengerBase
    {
        private readonly ISelectableItems<TKey> device;
        public SurroundModesMessenger(string key, string messagePath, ISelectableItems<TKey> device) : base(key, messagePath, device as IKeyName)
        {
            this.device = device;
        }
        
        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, context) =>
            {
                SendFullStatus();
            });

            device.ItemsUpdated += (sender, args) =>
            {
                SendFullStatus();
            };

            device.CurrentItemChanged += (sender, args) =>
            {
                SendFullStatus();
            };

            foreach (var input in device.Items)
            {
                var key = input.Key;
                var localItem = input.Value;

                AddAction($"/{key}", (id, content) =>
                {
                    localItem.Select();
                });

                localItem.ItemUpdated += (sender, args) =>
                {
                    SendFullStatus();
                };
            }
        }

        private void SendFullStatus()
        {
            try
            {
                this.LogInformation("Sending full status");

                var stateObject = new ISelectableItemsStateMessage<TKey>
                {
                    SelectableItems = new SurroundSoundModes<TKey>
                    {
                        Items = device.Items,
                        CurrentItem = device.CurrentItem
                    }
                };

                PostStatusMessage(stateObject);
            }
            catch (Exception e)
            {
                this.LogError("Error sending full status: {0}", e.Message);
            }
        }
    }

    public class ISelectableItemsStateMessage<TKey> : DeviceStateMessageBase
    {
        [JsonProperty("surroundSoundModes")]
        public SurroundSoundModes<TKey> SelectableItems { get; set; }
    }

    public class SurroundSoundModes<TKey>
    {
        [JsonProperty("items")]
        public Dictionary<TKey, ISelectableItem> Items { get; set; }

        [JsonProperty("currentItem")]
        [JsonConverter(typeof(StringEnumConverter))]
        public TKey CurrentItem { get; set; }
    }
}
