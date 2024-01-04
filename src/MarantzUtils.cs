using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace PDT.Plugins.Marantz
{
    public static class MarantzUtils
    {
        public static string VolumeCommand(int requestedLevel)
        {
            if (requestedLevel > 99)
                throw new ArgumentOutOfRangeException("requested level", "the max level allowed is 99");

            var level = requestedLevel.ToString("D").PadLeft(2, '0');
            return "MV" + level;
        }

        public static string ChannelVolumeCommand(string channelName, int requestedLevel)
        {
            if (requestedLevel > 62 || requestedLevel < 38)
                throw new ArgumentOutOfRangeException("requested level", "the max level allowed is 99");

            var level = requestedLevel.ToString("D").PadLeft(2, '0');
            return "CV" + channelName + " " + level;
        }
    }
}