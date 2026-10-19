using Newtonsoft.Json;

namespace PDT.Plugins.Marantz.Mock
{
    /// <summary>
    /// Starting state and behaviour of the mock receiver, from the optional "mock" block in properties
    /// </summary>
    public class MockMarantzAvrOptions
    {
        /// <summary>
        /// Creates the defaults: standby, SAT/CBL, stereo, 45 dB, max 98, 30ms per reply line
        /// </summary>
        public MockMarantzAvrOptions()
        {
            Input = "SAT/CBL";
            SurroundMode = "STEREO";
            Volume = 450;
            MaxVolume = 98;
            ResponseDelayMs = 30;
        }

        /// <summary>
        /// Delay before each reply line, in ms. The driver's poll spaces its queries 100ms apart.
        /// </summary>
        [JsonProperty("responseDelayMs")]
        public int ResponseDelayMs { get; set; }

        /// <summary>
        /// Whether the receiver starts with the main zone on
        /// </summary>
        [JsonProperty("powerOn")]
        public bool PowerOn { get; set; }

        /// <summary>
        /// Starting input, e.g. SAT/CBL, DVD, BD, TV, GAME, AUX1, NET
        /// </summary>
        [JsonProperty("input")]
        public string Input { get; set; }

        /// <summary>
        /// Starting surround mode, e.g. STEREO, MCH STEREO, DOLBY DIGITAL
        /// </summary>
        [JsonProperty("surroundMode")]
        public string SurroundMode { get; set; }

        /// <summary>
        /// Starting master volume on the driver's 0-980 scale (450 = 45.0 dB)
        /// </summary>
        [JsonProperty("volume")]
        public int Volume { get; set; }

        /// <summary>
        /// The MVMAX the receiver reports, in whole dB
        /// </summary>
        [JsonProperty("maxVolume")]
        public int MaxVolume { get; set; }
    }
}
