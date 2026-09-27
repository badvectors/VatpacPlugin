using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using vatsys;

namespace VatpacPlugin
{
    /// <summary>
    /// Has vatSys's arrivals lists show the simulator's aircraft while it is connected to a simulator
    /// server, rather than the network's.
    ///
    /// Each list gets its flights, and the order controllers drag them into, from a shared sequencing
    /// service on the network: vatSys polls getsequence.php there every five seconds for each airport
    /// with a list open, and calls setsequence.php when a flight is dragged. It does that whatever it is
    /// connected to, so in a session the list is full of the network's traffic and none of the session's.
    ///
    /// vatSys makes both requests through one HttpClient of its own, a private static field of its
    /// SequenceLadder. This puts a client in its place, once, whose handler looks at each request as it
    /// goes: on the live network, or not connected at all, it goes where it was going untouched; on a
    /// simulator server it goes to that server's /sequence instead, which answers from the session (see
    /// the server's SequenceController); and connected anywhere else - a server that isn't a simulator,
    /// or one the plugin hasn't found yet - the list is empty, since the network's traffic is wrong there
    /// too.
    ///
    /// Like the other reflection here it fails open: the field is bound to one build (0.4.9305), and if it
    /// isn't there nothing is changed and the lists carry on showing the network.
    /// </summary>
    internal static class ArrivalLists
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        /// <summary>What vatSys sends when there is nothing to show - no change number, then no flights.</summary>
        private const string Empty = "0[]";

        /// <summary>The service's code for a failed connection, which is what a drag gets while there is nowhere to send it.</summary>
        private const string NoConnection = "-5";

        /// <summary>Whether vatSys's client was found and replaced.</summary>
        public static bool Available { get; private set; }

        public static void Init()
        {
            try
            {
                var field = typeof(Network).Assembly.GetType("vatsys.SequenceLadder")?.GetField("httpClient", Any);

                if (field == null || field.FieldType != typeof(HttpClient)) return;

                field.SetValue(null, new HttpClient(new Redirect(new HttpClientHandler())));

                Available = true;
            }
            catch
            {
                Available = false;
            }
        }

        private sealed class Redirect : DelegatingHandler
        {
            public Redirect(HttpMessageHandler inner) : base(inner) { }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var page = request.RequestUri?.Segments.LastOrDefault()?.ToLowerInvariant();

                var get = page == "getsequence.php";
                var set = page == "setsequence.php";

                if ((!get && !set) || !Network.IsConnected || Network.IsOfficialServer) return base.SendAsync(request, cancellationToken);

                var server = Simulator.ConnectedServer;
                var cid = Network.ControllerId;

                if (server == null || string.IsNullOrWhiteSpace(cid)) return Task.FromResult(Answer(request, get ? Empty : NoConnection));

                var query = Parse(request.RequestUri.Query);

                var url = $"{server.Url.TrimEnd('/')}/sequence/{(get ? "get" : "set")}?cid={Uri.EscapeDataString(cid)}&ades={Value(query, "ades")}";

                if (set) url += $"&acid={Value(query, "acid")}&seq_index={Value(query, "seq_index")}";

                return base.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), cancellationToken);
            }

            private static HttpResponseMessage Answer(HttpRequestMessage request, string content) =>
                new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request, Content = new StringContent(content) };

            /// <summary>
            /// The query vatSys builds, pulled apart leniently - its getsequence query has spaces around the
            /// &amp; and the = ("ades=YSSY &amp; token_sum = 0"), so keys and values are trimmed.
            /// </summary>
            private static Dictionary<string, string> Parse(string query)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in query.TrimStart('?').Split('&'))
                {
                    var split = pair.IndexOf('=');

                    if (split < 0) continue;

                    var key = Uri.UnescapeDataString(pair.Substring(0, split)).Trim();
                    var value = Uri.UnescapeDataString(pair.Substring(split + 1)).Trim();

                    if (key.Length > 0) values[key] = value;
                }

                return values;
            }

            private static string Value(Dictionary<string, string> query, string key) =>
                Uri.EscapeDataString(query.TryGetValue(key, out var value) ? value : "");
        }
    }
}
