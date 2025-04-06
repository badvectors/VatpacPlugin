using Newtonsoft.Json;
using System;

namespace VatpacPlugin
{
    public class Aircraft
    {
        [JsonProperty(PropertyName = "callsign")] public string Callsign { get; private set; }
        [JsonProperty(PropertyName = "scratchPad")] public string ScratchPad { get; private set; }
        [JsonProperty(PropertyName = "global")] public string Global { get; private set; }
        [JsonProperty(PropertyName = "cflLower")] public int? CFLLower { get; private set; }
        [JsonProperty(PropertyName = "cflUpper")] public int? CFLUpper { get; private set; }
        [JsonProperty(PropertyName = "cflVisual")] public bool CFLVisual { get; private set; }
        public DateTime LastUpdateUtc { get; private set; }
    }
}
