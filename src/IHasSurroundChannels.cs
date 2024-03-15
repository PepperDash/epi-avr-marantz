using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public interface IHasSurroundChannels: IKeyName
    {
        event EventHandler SurroundChannelsUpdated;
        IDictionary<SurroundChannel, IBasicVolumeWithFeedback> Channels { get; }

        void SetDefaultChannelLevels();
    }
}