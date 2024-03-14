using Newtonsoft.Json;
using PepperDash.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDT.Plugins.Marantz
{
    public interface ISurroundSoundModes: IKeyName
    {
        event EventHandler SurroundModesUpdated;

        [JsonProperty("surroundModes", NullValueHandling = NullValueHandling.Ignore)]
        Dictionary<eSurroundModes, MarantzSurroundMode> SurroundModes { get; }

        [JsonProperty("currentSurroundMode", NullValueHandling = NullValueHandling.Ignore)]
        string CurrentSurroundMode { get; }
    }
}
