using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public interface IHasSurroundChannels
    {
        event EventHandler SurroundChannelsUpdated;
        IDictionary<SurroundChannel, IBasicVolumeWithFeedback> Channels { get; }
    }
}