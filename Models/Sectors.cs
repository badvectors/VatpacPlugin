using System.Collections.Generic;
using System.Linq;
using vatsys;

namespace VatpacPlugin
{
    public class Sectors
    {
        public static void Init()
        {
            var primarySector = SectorsVolumes.Sectors.FirstOrDefault(x => x.Callsign == Network.Me.Callsign);

            if (primarySector == null) return;

            var sectors = new List<SectorsVolumes.Sector>
            {
                primarySector
            };

            foreach (var subsector in primarySector.SubSectors.ToList())
            {
                var onlineATC = Network.GetOnlineATCs.FirstOrDefault(x => x.Callsign == subsector.Callsign && x.IsRealATC);

                if (onlineATC != null) continue;

                sectors.Add(subsector);
            }

            MMI.SetControlledSectors(sectors);
        }

        public static void CheckActive()
        {
            var primarySector = SectorsVolumes.Sectors.FirstOrDefault(x => x.Callsign == Network.Me.Callsign);

            if (primarySector == null) return;

            var sectors = new List<SectorsVolumes.Sector>
            {
                primarySector
            };

            var currentSectors = MMI.SectorsControlled.ToList();

            foreach (var frequency in Audio.VSCSFrequencies.ToList())
            {
                var frequencySector = SectorsVolumes.Sectors.FirstOrDefault(x => x.Callsign == frequency.Name);

                if (frequencySector == null) continue;

                var currentlyControlled = currentSectors.FirstOrDefault(x => x.Callsign == frequency.Name);

                if (currentlyControlled == null)
                {
                    if (!frequency.Transmit) continue;

                    currentSectors.Add(frequencySector);

                    foreach (var subsector in frequencySector.SubSectors.ToList())
                    {
                        var onlineATC = Network.GetOnlineATCs.FirstOrDefault(x => x.Callsign == subsector.Callsign && x.IsRealATC);

                        if (onlineATC != null) continue;

                        currentSectors.Add(subsector);
                    }
                }
                else
                {
                    if (frequency.Transmit) continue;

                    if (primarySector == currentlyControlled) continue;

                    currentSectors.Remove(currentlyControlled);

                    foreach (var subsector in currentlyControlled.SubSectors.ToList())
                    {
                        currentSectors.Remove(subsector);
                    }
                }
            }

            MMI.SetControlledSectors(currentSectors);
        }
    }
}