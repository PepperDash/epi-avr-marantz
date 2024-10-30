using System;
using System.Collections.Generic;

namespace PDT.Plugins.Marantz
{
    public static class MarantzSurroundChannel
    {
        private static readonly IDictionary<string, SurroundChannel> Mappings =
            new Dictionary<string, SurroundChannel>(StringComparer.OrdinalIgnoreCase)
            {
                {"FL", SurroundChannel.FrontLeft},
                {"C", SurroundChannel.Center},
                {"FR", SurroundChannel.FrontRight},
                {"SL", SurroundChannel.SurroundLeft},
                {"SR", SurroundChannel.SurroundRight},
                {"SBL", SurroundChannel.SurroundBackLeft},
                {"SBR", SurroundChannel.SurroundBackRight},
                {"SW", SurroundChannel.Subwoofer},
                {"SB", SurroundChannel.SurroundBack},
                {"FHL", SurroundChannel.FrontHeightLeft},
                {"FHR", SurroundChannel.FrontHeightRight},
                {"FWL", SurroundChannel.FrontWideLeft},
                {"FWR", SurroundChannel.FrontWideRight},
                {"TFL", SurroundChannel.TopFrontLeft},
                {"TFR", SurroundChannel.TopFrontRight},
                {"TML", SurroundChannel.TopMiddleLeft},
                {"TMR", SurroundChannel.TopMiddleRight},
                {"TRL", SurroundChannel.TopRearLeft},
                {"TRR", SurroundChannel.TopRearRight},
                {"RHL", SurroundChannel.RearHeightLeft},
                {"RHR", SurroundChannel.RearHeightRight},
                {"FDL", SurroundChannel.FrontDolbyLeft},
                {"FDR", SurroundChannel.FrontDolbyRight},
                {"SDL", SurroundChannel.SurroundDolbyLeft},
                {"SDR", SurroundChannel.SurroundDolbyRight},
                {"BDL", SurroundChannel.BackDolbyLeft},
                {"BDR", SurroundChannel.BackDolbyRight},
                {"SHL", SurroundChannel.SurroundHeightLeft},
                {"SHR", SurroundChannel.SurroundHeightRight},
                {"TS", SurroundChannel.TopSurround},
                {"CH", SurroundChannel.CenterHeight},
                {"SW2", SurroundChannel.Subwoofer2},
            };

        public static SurroundChannel Parse(string surroundChannel)
        {
            SurroundChannel result;
            return Mappings.TryGetValue(surroundChannel, out result) 
                ? result 
                : SurroundChannel.Unknown;
        }
    }
}