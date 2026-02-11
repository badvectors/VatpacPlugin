using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace VatpacPlugin
{
    public class Aircraft
    {
        [JsonProperty(PropertyName = "callsign")] 
        public string Callsign { get; private set; }

        [JsonProperty(PropertyName = "isSweatbox")]
        public string IsSweatbox { get; private set; }

        [JsonProperty(PropertyName = "state")]
        public string State { get; private set; }

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

        [JsonProperty(PropertyName = "atd")]
        public DateTime? ATD { get; private set; }

        [JsonProperty(PropertyName = "lastController")] 
        public string LastController { get; private set; }

        [JsonProperty(PropertyName = "positions")]
        public string Positions { get; private set; }

        [JsonProperty(PropertyName = "lastUpdateUtc")]
        public DateTime LastUpdateUtc { get; private set; }
    }
}
