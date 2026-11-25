using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using PepperDash.Core;

namespace PDT.Plugins.Marantz
{
    public static class MarantzUtils
    {
        public static string VolumeCommand(int requestedLevel, string prefix)
        {
            if (requestedLevel > 980 || requestedLevel < 0)
                throw new ArgumentOutOfRangeException("requested level", "the max level allowed is 980 the min level is 0");

            return prefix + FormatLevel(requestedLevel);
        }

        /// <summary>
        /// Formats a level on the 0-980 scale (10 per dB) the way the receiver reads it: whole dB as two
        /// digits ("45" = 45 dB), half dB as three digits ending in 5 ("455" = 45.5 dB). Levels in between
        /// are snapped to the nearest half dB, since the receiver ignores anything else, and an unpadded
        /// two-digit value would be read as whole dB ("98" from level 98 means 98 dB, not 9.8 dB).
        /// </summary>
        private static string FormatLevel(int level)
        {
            var snapped = (int)Math.Round(level / 5.0, MidpointRounding.AwayFromZero) * 5;

            return snapped % 10 == 0
                ? (snapped / 10).ToString("D2")
                : snapped.ToString("D3");
        }

        public static string ChannelVolumeCommand(string channelName, int requestedLevel)
        {
            if (requestedLevel > 620 || requestedLevel < 380)
                throw new ArgumentOutOfRangeException("requested level", "the level allowed is 380 to 620");

            return "CV" + channelName + " " + FormatLevel(requestedLevel);
        }
    }
}