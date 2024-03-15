using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public interface IHasSurroundChannels: IKeyName
    {
        event EventHandler SurroundChannelsUpdated;
        [JsonProperty("surroundChannels")]
        IDictionary<SurroundChannel, IBasicVolumeWithFeedback> Channels { get; }

        void SetDefaultChannelLevels();
    }
}