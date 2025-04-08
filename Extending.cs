using System.Collections.Generic;
using vatsys;

namespace VatpacPlugin
{
    public class Extending
    {
        public static void Check()
        {
            if (!Network.Me.Callsign.EndsWith("_CTR")) return;

            var extending = string.Empty;

            foreach (var frequency in Audio.VSCSFrequencies)
            {
                if (!frequency.Transmit) continue;

                if (!frequency.Name.EndsWith("_CTR")) continue;

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

            UpdateInfo(extending);
        }

        private static void UpdateInfo(string extending)
        {
            var controllerInfo = Network.ControllerInfo;

            if (controllerInfo == null) return;

            var newInfo = new List<string>();

            foreach (var line in controllerInfo)
            {
                if (!line.StartsWith("Extending"))
                {
                    newInfo.Add(line);

                    continue;
                }
            }

            if (extending != string.Empty)
            {
                newInfo.Add(extending);
            }

            Network.ControllerInfo = newInfo.ToArray();
        }
    }
}
