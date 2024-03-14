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
    public class ISurroundSoundModesMessenger : MessengerBase
    {
        private readonly ISurroundSoundModes _surroundDevice;

        public ISurroundSoundModesMessenger(string key, string messagePath, ISurroundSoundModes device) : base(key, messagePath, device)
        {
            _surroundDevice = device;
        }

        protected override void RegisterActions()
        {
            base.RegisterActions();

            AddAction("/fullStatus", (id, content) =>
            {
                var message = new SurroundSoundModesState
                {
                    CurrentSurroundMode = _surroundDevice.CurrentSurroundMode,
                    SurroundModes = _surroundDevice.SurroundModes.ToDictionary(kv => kv.Key, kv =>
                        new SurroundMode { IsSelected = kv.Value.IsSelected, Key = kv.Value.Key, Name = kv.Value.Name })
                };

                PostStatusMessage(message);
            });

            foreach (var mode in _surroundDevice.SurroundModes)
            {
                var key = mode.Key;
                var localMode = mode.Value;

                AddAction($"/{localMode.Key}", (id, content) =>
                {
                    localMode.Select();
                });

                localMode.IsSelectedChanged += (sender, args) =>
                {
                    PostStatusMessage(JToken.FromObject(new
                    {
                        currentSurroundMode = _surroundDevice.CurrentSurroundMode,
                        surroundModes = _surroundDevice.SurroundModes.ToDictionary(kv => kv.Key, kv =>
                            new SurroundMode { IsSelected = kv.Value.IsSelected, Key = kv.Value.Key, Name = kv.Value.Name })
                    }));
                };
            }
        }
    }

    public class SurroundSoundModesState : DeviceStateMessageBase
    {
        [JsonProperty("currentSurroundMode", NullValueHandling = NullValueHandling.Ignore)]
        public string CurrentSurroundMode { get; set; }

        [JsonProperty("surroundModes", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<eSurroundModes, SurroundMode> SurroundModes { get; set; }
    }

    public class SurroundMode: IKeyName
    {
        [JsonProperty("isSelected")]
        public bool IsSelected { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
