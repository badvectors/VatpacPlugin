using System.Collections.Generic;
using vatsys;

namespace VatpacPlugin
{
    public class Extending
    {
        private static readonly Dictionary<string, string> Mapping = new Dictionary<string, string>()
        {
            { "ML-GUN_CTR","BIK 129.8 use 128.4 " },
            { "ML-BLA_CTR","ELW 123.75 use 132.2 " },
            { "ML-HYD_CTR","PIY 133.9 use 118.2 " },
            { "ML-MUN_CTR","YWE 134.325 use 132.6 " },
        };

        public static void Check()
        {
            if (!Network.Me.Callsign.EndsWith("_CTR")) return;

            var extending = string.Empty;

            var mapping = string.Empty;

            foreach (var frequency in Audio.VSCSFrequencies)
            {
                if (!frequency.Transmit) continue;

                if (!frequency.Name.EndsWith("_CTR")) continue;

                var mapOk = Mapping.TryGetValue(frequency.Name, out string text);

                if (mapOk)
                {
                    mapping += text;
                }
     
                if (frequency.Name == Network.Me.Callsign) continue;

                var shortName = frequency.Name
                    .Replace("_CTR", "")
                    .Replace("BN-", "")
                    .Replace("ML-", "");

                extending += $"{shortName} {Conversions.FrequencyToString(frequency.Frequency)} ";
            }

            if (extending != string.Empty)
            {
                extending = $"Extending {extending}";
            }

            if (mapping != string.Empty)
            {
                mapping = $"Uncontactable on {mapping}";
            }

            UpdateInfo(extending, mapping);
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

            if (extending != string.Empty)
            {
                newInfo.Add(extending);
            }

            if (mapping != string.Empty)
            {
                newInfo.Add(mapping);
            }

            Network.ControllerInfo = newInfo.ToArray();
        }
    }
}
