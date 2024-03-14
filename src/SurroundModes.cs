using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PepperDash.Core;

namespace PDT.Plugins.Marantz
{
    public enum eSurroundModes
    {
        Direct,
        PureDirect,
        Stereo,
        Standard,
        DolbyDigital,
        DTS,
        MultiChannelStereo,
        RockArena,
        JazzClub,
        MonoMovie,
        Matrix,
        VideoGame,
        Virtual,
        Auto,
        Neural,
        Auro3D,
        Auro2DSurround,
        Left,
        Right,
        Movie,
        Music,
        Game,
    }

    public interface ISelectableItem: IKeyName
    {
        event EventHandler IsSelectedChanged;

        [JsonProperty("isSelected")]
        bool IsSelected { get; set; }

        void Select();
    }
}
