using System;
using System.Collections.Generic;
using PepperDash.Core;
using PepperDash.Essentials.Core;

namespace PDT.Plugins.Marantz
{
    public class MarantzDeviceFactory : EssentialsPluginDeviceFactory<MarantzDevice>
    {
        public MarantzDeviceFactory()
        {
            MinimumEssentialsFrameworkVersion = "2.4.4";
            TypeNames = new List<string>() {"MarantzAvr"};
        }

        public override EssentialsDevice BuildDevice(PepperDash.Essentials.Core.Config.DeviceConfig dc)
        {
            Debug.LogDebug("[{Key}] Factory Attempting to create new device from type: {type}", dc.Key, dc.Type);

            var propertiesConfig = dc.Properties.ToObject<MarantzProps>();

            var comms = CommFactory.CreateCommForDevice(dc);
            if (comms != null) 
                return new MarantzDevice(dc.Key, dc.Name, propertiesConfig, comms);

            Debug.LogDebug("[{Key}] Factory Notice: No control object present for device {Name}", dc.Key, dc.Name);
            throw new Exception($"No control object present for device: {dc.Key}");
        }
    }
}

          