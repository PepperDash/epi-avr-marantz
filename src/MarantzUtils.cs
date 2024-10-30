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

            var level = requestedLevel;

            if(requestedLevel % 10 == 0) level = requestedLevel / 10;

            var levelToSend = level.ToString();

            // if the level is less than 10, pad it with a 0
            if(requestedLevel < 100)
            {
                Debug.LogMessage(Serilog.Events.LogEventLevel.Verbose, $"Requested level is less than 100, padding with 0. Requested level: {level}");

                if (requestedLevel % 10 == 0) levelToSend = level.ToString("D").PadLeft(2, '0');
                else if (requestedLevel % 10 == 5) levelToSend = level.ToString("D").PadLeft(3, '0');
            }

            return prefix + levelToSend;
        }

        public static string ChannelVolumeCommand(string channelName, int requestedLevel)
        {
            if (requestedLevel > 620 || requestedLevel < 380)
                throw new ArgumentOutOfRangeException("requested level", "the max level allowed is 380");

            if (requestedLevel % 10 == 0) requestedLevel = requestedLevel / 10;
            var level = requestedLevel.ToString();

            // if the level is less than 10, pad it with a 0
            if (requestedLevel < 10)  level = requestedLevel.ToString("D").PadLeft(2, '0');

            return "CV" + channelName + " " + level;
        }
    }
}