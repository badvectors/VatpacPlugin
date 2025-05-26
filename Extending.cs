using System.Collections.Generic;
using System.Linq;
using vatsys;

namespace VatpacPlugin
{
    public class Extending
    {
        private static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>()
        {
            { "BN-KPL_CTR","CVN 133.8 and SWY 133.2 use 125.9" }
        };

        public static void Check()
        {
            if (!Network.Me.Callsign.EndsWith("_CTR")) return;

            var extending = new List<string>();

            var mapping = new List<string>();

            foreach (var frequency in Audio.VSCSFrequencies)
            {
                if (!frequency.Transmit) continue;

                if (!frequency.Name.EndsWith("_CTR")) continue;

                var mapOk = Mapping.TryGetValue(frequency.Name, out string text);

                if (mapOk)
                {
                    mapping.Add(text);
                }
     
                if (frequency.Name == Network.Me.Callsign) continue;

                var shortName = frequency.Name
                    .Replace("_CTR", "")
                    .Replace("BN-", "")
                    .Replace("ML-", "");

                extending.Add($"{shortName} {Conversions.FrequencyToString(frequency.Frequency)}");
            }

            var extendingText = string.Empty;

            var mappingText = string.Empty;

            if (extending.Any())
            {
                extendingText = $"Extending {DoText(extending)}";
            }

            if (mapping.Any())
            {
                mappingText = $"Uncontactable on {DoText(mapping)}";
            }

            UpdateInfo(extendingText, mappingText);
        }

        private static string DoText(List<string> input)
        {
            int count = 0;

            string text = string.Empty;

            foreach (var item in input)  
            {
                count++;

                if (count == 1)
                {
                    text = item;
                    continue;
                }

                if (count <= input.Count - 1)
                {
                    text = $"{text}, {item}";
                    continue;
                }

                text = $"{text} and {item}";
            }

            return text;
        }

        private static void UpdateInfo(string extending, string mapping)
        {
            var controllerInfo = Network.ControllerInfo;

            if (controllerInfo == null) return;

            var newInfo = new List<string>();

            foreach (var line in controllerInfo)
            {
                if (!line.StartsWith("Extending") && !line.StartsWith("Uncontactable"))
                {
                    newInfo.Add(line);

                    continue;
                }
            }

            var newLine = string.Empty;

            if (extending != string.Empty)
            {
                newLine = extending;
            }

            if (mapping != string.Empty)
            {
                if (newLine != string.Empty) 
                {
                    newLine += " | ";
                }

                newLine += mapping;
            }

            if (newLine != string.Empty)
            {
                newInfo.Add(newLine);
            }

            Network.ControllerInfo = newInfo.ToArray();
        }
    }
}