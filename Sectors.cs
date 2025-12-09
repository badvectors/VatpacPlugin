using System.Linq;
using vatsys;

namespace VatpacPlugin
{
    public class Sectors
    {
        public static void CheckActive()
        {
            var primarySector = SectorsVolumes.Sectors.FirstOrDefault(x => x.Callsign == Network.Me.Callsign);

            if (primarySector == null) return;

            var currentSectors = MMI.SectorsControlled.ToList();

            foreach (var frequency in Audio.VSCSFrequencies)
            {
                var frequencySector = SectorsVolumes.Sectors.FirstOrDefault(x => x.Callsign == frequency.Name);

                if (frequencySector == null) continue;

                var currentlyControlled = currentSectors.FirstOrDefault(x => x.Callsign == frequency.Name);

                if (currentlyControlled == null)
                {
                    // not currently controlled

                    if (!frequency.Transmit) continue;

                    currentSectors.Add(frequencySector);

                    foreach (var subsector in frequencySector.SubSectors)
                    {
                        currentSectors.Add(subsector);
                    }
                }
                else
                {
                    // currently controlled

                    if (frequency.Transmit) continue;

                    if (primarySector == currentlyControlled) continue;

                    currentSectors.Remove(currentlyControlled);

                    foreach (var subsector in currentlyControlled.SubSectors)
                    {
                        currentSectors.Remove(subsector);
                    }
                }
            }

            MMI.SetControlledSectors(currentSectors);
        }
    }
}