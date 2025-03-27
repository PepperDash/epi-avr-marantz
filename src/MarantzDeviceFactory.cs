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
            MinimumEssentialsFrameworkVersion = "2.1.0";
            TypeNames = new List<string>() {"MarantzAvr"};
        }

        public override EssentialsDevice BuildDevice(PepperDash.Essentials.Core.Config.DeviceConfig dc)
        {
            Debug.Console(1, "[{0}] Factory Attempting to create new device from type: {1}", dc.Key, dc.Type);

            var propertiesConfig = dc.Properties.ToObject<MarantzProps>();

            var comms = CommFactory.CreateCommForDevice(dc);
            if (comms != null) 
                return new MarantzDevice(dc.Key, dc.Name, propertiesConfig, comms);

            Debug.Console(1, "[{0}] Factory Notice: No control object present for device {1}", dc.Key, dc.Name);
            throw new Exception(string.Format("No control object present for device: {0}", dc.Key));
        }
    }
}

          