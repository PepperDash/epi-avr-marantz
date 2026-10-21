using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using PepperDash.Core;
using PepperDash.Essentials.Core;
using PepperDash.Essentials.Core.Config;

namespace PDT.Plugins.Marantz
{
    /// <summary>
    /// Builds the real <see cref="MarantzDevice"/> on an in-process mock receiver, so a room can be
    /// bench-tested without an AVR. A real config switches to the mock by changing only "type";
    /// any "control" block is ignored.
    /// </summary>
    public class MockMarantzAvrFactory : EssentialsPluginDeviceFactory<MarantzDevice>
    {
        /// <summary>
        /// Registers the "mockMarantzAvr" type
        /// </summary>
        public MockMarantzAvrFactory()
        {
            // Keep equal to the PepperDashEssentials PackageReference version in the csproj: the version
            // this plugin is compiled against is its minimum
            MinimumEssentialsFrameworkVersion = "2.43.0";
            TypeNames = new List<string> { "mockMarantzAvr" };
        }

        /// <inheritdoc />
        public override EssentialsDevice BuildDevice(DeviceConfig dc)
        {
            var properties = dc.Properties ?? new JObject();
            var config = properties.ToObject<MarantzProps>() ?? new MarantzProps();

            var mockToken = properties["mock"];
            var options = mockToken != null && mockToken.Type == JTokenType.Object
                ? mockToken.ToObject<MockMarantzAvrOptions>()
                : new MockMarantzAvrOptions();

            var responder = new MockMarantzAvrResponder(options);
            var comms = new MockMarantzAvrCommunication(dc.Key + "-mockComm", responder, options.ResponseDelayMs);

            MockMarantzAvrConsole.Register(dc.Key, responder);
            Debug.LogInformation("Building mock Marantz AVR '{0}' (no hardware). Console: mockmarantz {0} status", dc.Key);

            return new MarantzDevice(dc.Key, dc.Name, config, comms);
        }
    }
}
