using Newtonsoft.Json;
using System;

namespace VatpacPlugin
{
    public class Aircraft
    {
        [JsonProperty(PropertyName = "callsign")] 
        public string Callsign { get; private set; }

        [JsonProperty(PropertyName = "scratchPad")] 
        public string ScratchPad { get; private set; }

        [JsonProperty(PropertyName = "global")] 
        public string Global { get; private set; }

        [JsonProperty(PropertyName = "cflLower")] 
        public int? CFLLower { get; private set; }

        [JsonProperty(PropertyName = "cflUpper")] 
        public int? CFLUpper { get; private set; }

        [JsonProperty(PropertyName = "cflVisual")] 
        public bool CFLVisual { get; private set; }

        [JsonProperty(PropertyName = "controllerTracking")] 
        public string ControllerTracking { get; private set; }

        [JsonProperty(PropertyName = "previousTracking")]
        public string PreviousTracking { get; private set; }

        [JsonProperty(PropertyName = "lastUpdateUtc")]
        public DateTime LastUpdateUtc { get; private set; }
    }
}
