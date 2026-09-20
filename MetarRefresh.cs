using System;
using System.Collections.Generic;
using System.Linq;
using vatsys;

namespace VatpacPlugin
{
    /// <summary>
    /// Gets a METAR changed in the simulator in front of the ATIS in vatSys straight away, rather than
    /// whenever vatSys next thinks to look.
    ///
    /// The server can't do this by itself. vatSys only takes a METAR it has asked for - MET matches each
    /// one that arrives to a request it has waiting, and drops it if there isn't one - so sending the new
    /// text down the FSD connection unasked does nothing. Left alone, vatSys asks again for a METAR it is
    /// subscribed to once it is five minutes old or the half hour has turned, which in a session is a long
    /// time to be reading out weather the instructor has already changed.
    ///
    /// So the server says what its METARs are in the answer to the session poll, and this has vatSys ask
    /// again for any that differ from what it last saw. Asking is all it does: the answer comes back over
    /// FSD and into MET the way it always does, and whoever is watching the request - vatSys's own ATIS,
    /// the ATIS plugin - hears about it from MET as they would after any refresh.
    ///
    /// Only subscribed requests are touched, which is what running an ATIS makes. Without one there is
    /// nothing in vatSys keeping a METAR up to date, and this does nothing. A METAR looked up once in the
    /// MET window is vatSys's to leave as it was when asked for, here as on the network.
    ///
    /// Everything used is public plugin API, so unlike RadarFreeze and CoordLines there is nothing to
    /// resolve up front. It still swallows whatever goes wrong: it runs on the session timer's thread.
    /// </summary>
    internal static class MetarRefresh
    {
        /// <summary>The text each METAR had when it was last dealt with - asked for again, or found to be what vatSys already had.</summary>
        private static readonly Dictionary<string, string> Seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Gate = new object();

        /// <summary>
        /// Takes the server's METARs, ICAO to raw text, from the latest poll. Null - not on a simulator
        /// server, or one too old to say, or one that couldn't just now - is nothing known and nothing done.
        /// </summary>
        public static void Apply(Dictionary<string, string> metars)
        {
            if (metars == null) return;

            try
            {
                lock (Gate)
                {
                    foreach (var metar in metars)
                    {
                        if (string.IsNullOrWhiteSpace(metar.Key) || string.IsNullOrWhiteSpace(metar.Value)) continue;

                        if (Seen.TryGetValue(metar.Key, out var seen) && seen == metar.Value) continue;

                        if (Refresh(metar.Key, metar.Value)) Seen[metar.Key] = metar.Value;
                    }
                }
            }
            catch { }
        }

        /// <summary>vatSys has disconnected - what was seen belonged to that session.</summary>
        public static void Clear()
        {
            lock (Gate)
            {
                Seen.Clear();
            }
        }

        /// <summary>
        /// Has vatSys ask again for this METAR wherever it is subscribed to it and hasn't already got this
        /// text. False when there is still something to do on a later poll: nothing subscribed to it yet,
        /// or a request for it already on its way.
        /// </summary>
        private static bool Refresh(string icao, string value)
        {
            var met = MET.Instance;

            if (met == null) return false;

            var requests = met.Products.Keys
                .Where(x => x != null && x.Subscribe && x.Type == MET.ProductType.VATSIM_METAR && string.Equals(x.Icao, icao, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (requests.Count == 0) return false;

            var done = true;

            foreach (var request in requests)
            {
                var latest = met.GetProducts(request)?.FirstOrDefault();

                // Already has it - the first poll after connecting, or vatSys's own refresh got there first.
                if (latest?.Text != null && latest.Text.Contains(value.Trim())) continue;

                // Asked a second time while the first is still out, vatSys loses track of the first one's
                // timeout, which then goes off and puts "No product available" over the answer. Wait for
                // it - if what comes back is the old text, the next poll asks again.
                if (request.Timer != null && request.Timer.Enabled)
                {
                    done = false;
                    continue;
                }

                met.RequestProduct(request);
            }

            return done;
        }
    }
}
