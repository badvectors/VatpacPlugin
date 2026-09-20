using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using vatsys;

namespace VatpacPlugin
{
    public class Simulator
    {
        public static List<Server> Servers { get; } = LoadServers();

        /// <summary>
        /// The simulator server vatSys is connected to, or null when it isn't on one. Never chosen by hand:
        /// Connected() works it out by asking every known server, and Disconnected() clears it. Everything
        /// else here - the pause poll, sending selections - does nothing while this is null.
        /// </summary>
        public static Server ConnectedServer { get; private set; }

        /// <summary>Whether this CID owns the session on ConnectedServer - the instructor running it - rather than being a student permitted into it. False while there isn't one.</summary>
        public static bool IsInstructor { get; private set; }

        /// <summary>Whether ConnectedServer handles calls made on vatSys's own VSCS - what CoordLines goes by. False while there isn't one, and for a server too old to say.</summary>
        public static bool Landlines { get; private set; }

        /// <summary>Whether ConnectedServer answers vatSys's frequency lookups - what VscsFrequencies goes by. False while there isn't one, and for a server too old to say.</summary>
        public static bool Frequencies { get; private set; }

        /// <summary>
        /// What the last poll found, for the Simulator page to show. Every one of these except Paused
        /// and Running is a reason nothing will freeze, and the point of surfacing it is that they are
        /// otherwise indistinguishable from a broken plugin.
        /// </summary>
        public static SessionStatus Status { get; private set; } = SessionStatus.NotConnected;

        /// <summary>
        /// How many times, and how far apart, the servers are asked after vatSys connects before giving up.
        /// More than once because Network.Connected fires when vatSys has connected, not when the server
        /// has finished letting it in - the server checks the login against VATSIM before it counts the
        /// connection as part of a session, so the first ask can land before it would say yes.
        /// </summary>
        private const int SearchAttempts = 6;
        private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Longest any one server gets to answer. The shared HttpClient's own timeout is 100 seconds, and
        /// a search waits on every server, so one that is down would otherwise hold up finding the one
        /// that isn't.
        /// </summary>
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Moves on every connect and disconnect, so a search still running from an earlier connection can
        /// tell it has been superseded and must not set anything.
        /// </summary>
        private static int _connection;

        private static bool _searching;

        /// <summary>
        /// How often the connected server is asked whether its session is paused. One second: this is what
        /// decides how quickly the display settles after the instructor hits pause, and the answer is a
        /// few values read out of memory. AutoReset is off and the timer is restarted after each poll
        /// finishes, so a slow or hanging server can't stack requests up.
        /// </summary>
        private static readonly System.Timers.Timer _sessionTimer =
            new System.Timers.Timer(TimeSpan.FromSeconds(1).TotalMilliseconds) { AutoReset = false };

        public static void Init()
        {
            MMI.SelectedTrackChanged += MMI_SelectedTrackChanged;

            MMI.SelectedGroundTrackChanged += MMI_SelectedGroundTrackChanged;

            _sessionTimer.Elapsed += SessionTimer_Elapsed;
            _sessionTimer.Start();
        }

        /// <summary>
        /// vatSys has connected to something - find out whether it's one of the simulator servers, by
        /// asking each of them whether this CID is connected to it. Only the server vatSys is actually on
        /// says yes: being permitted into a session somewhere else isn't enough (see SessionState.Connected).
        /// </summary>
        public static async void Connected()
        {
            var connection = Interlocked.Increment(ref _connection);

            Clear();

            if (Network.IsOfficialServer) return;

            _searching = true;

            try
            {
                for (var attempt = 0; attempt < SearchAttempts; attempt++)
                {
                    if (attempt > 0) await Task.Delay(SearchInterval);

                    if (connection != _connection) return;

                    var cid = Network.ControllerId;

                    if (string.IsNullOrWhiteSpace(cid)) continue;

                    var answers = await Task.WhenAll(Servers.Select(x => GetSession(x, cid)));

                    if (connection != _connection) return;

                    var found = answers.FirstOrDefault(x => x.State != null && x.State.Connected);

                    if (found == null) continue;

                    IsInstructor = found.State.IsInstructor;
                    Landlines = found.State.Landlines;
                    Frequencies = found.State.Frequencies;
                    ConnectedServer = found.Server;

                    return;
                }
            }
            catch
            {
                // Nothing here may reach vatSys - this is an async void on its event thread.
            }
            finally
            {
                if (connection == _connection) _searching = false;
            }
        }

        /// <summary>
        /// vatSys has disconnected - forget the server and let go of the radar picture if a pause was
        /// holding it. The next poll would release it anyway, but that is up to a second away (longer if
        /// a request is in flight), and a picture held over from a session this vatSys has left is wrong
        /// for every moment of that - so it goes now, not when the timer gets round to it.
        /// </summary>
        public static void Disconnected()
        {
            Interlocked.Increment(ref _connection);

            _searching = false;

            Clear();

            Status = SessionStatus.NotConnected;

            // This runs on vatSys's event thread, so nothing may escape it.
            try { RadarFreeze.Apply(false); } catch { }
            try { CoordLines.Apply(false); } catch { }
            try { VscsFrequencies.Apply(null); } catch { }
        }

        private static void Clear()
        {
            ConnectedServer = null;
            IsInstructor = false;
            Landlines = false;
            Frequencies = false;
        }

        private static async void SessionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                var paused = await IsSessionPaused();

                // Checked again here, after the wait: a disconnect while the server was being asked has
                // already released the picture (see Disconnected), and an answer of "paused" that was
                // on its way back at the time must not take hold of it again.
                RadarFreeze.Apply(paused && ConnectedServer != null);

                // Not tied to the session running: a paused session still has an instructor in it to talk
                // to. Just to being on a server that will do something with the call.
                CoordLines.Apply(ConnectedServer != null && Landlines);

                var server = ConnectedServer;

                VscsFrequencies.Apply(server != null && Frequencies ? server.Url : null);
            }
            catch
            {
                // Belt and braces - Apply and IsSessionPaused both swallow their own failures, but an
                // unhandled exception on this thread would take vatSys down with it, and a plugin must
                // never do that. Anything unexpected releases the freeze.
                try { RadarFreeze.Apply(false); } catch { }
                try { CoordLines.Apply(false); } catch { }
                try { VscsFrequencies.Apply(null); } catch { }
            }
            finally
            {
                _sessionTimer.Start();
            }
        }

        /// <summary>
        /// Whether the simulator session this vatSys is connected to is loaded but paused. False for every
        /// other case, including all the failure ones: not connected, the real network, not on a simulator
        /// server, no session for this CID, an unreachable server, an answer that doesn't parse.
        /// RadarFreeze only holds the display while this keeps saying true, so every one of those
        /// releases it.
        /// </summary>
        private static async Task<bool> IsSessionPaused()
        {
            if (!Network.IsConnected)
            {
                Status = SessionStatus.NotConnected;
                return false;
            }

            if (Network.IsOfficialServer)
            {
                Status = SessionStatus.OfficialNetwork;
                return false;
            }

            var server = ConnectedServer;

            if (server == null)
            {
                Status = _searching ? SessionStatus.Searching : SessionStatus.NoServer;
                return false;
            }

            var cid = Network.ControllerId;

            if (string.IsNullOrWhiteSpace(cid))
            {
                Status = SessionStatus.NotConnected;
                return false;
            }

            var answer = await GetSession(server, cid);

            // Answered while a disconnect (or a connect to somewhere else) happened - it's about a
            // session this vatSys is no longer in.
            if (server != ConnectedServer) return false;

            if (answer.State == null)
            {
                Status = answer.NotFound ? SessionStatus.NoSession : SessionStatus.Unreachable;
                return false;
            }

            IsInstructor = answer.State.IsInstructor;
            Landlines = answer.State.Landlines;
            Frequencies = answer.State.Frequencies;

            // A session with no scenario loaded isn't paused, it just hasn't started - freezing there
            // would hold the display still before there was ever anything on it.
            if (!answer.State.ScenarioLoaded)
            {
                Status = SessionStatus.NoScenario;
                return false;
            }

            Status = answer.State.Running ? SessionStatus.Running : SessionStatus.Paused;

            return !answer.State.Running;
        }

        /// <summary>What one server said when asked about a CID. State is null for every kind of failure; NotFound picks out the one that means "reachable, but this CID isn't in a session here".</summary>
        private sealed class Answer
        {
            public Server Server;
            public SessionState State;
            public bool NotFound;
        }

        /// <summary>
        /// Asks a server about this CID's session. Never throws - a server that is down, slow, or an older
        /// one that answers /session with a web page rather than JSON all come back as a null State.
        /// </summary>
        private static async Task<Answer> GetSession(Server server, string cid)
        {
            var answer = new Answer { Server = server };

            try
            {
                using (var timeout = new CancellationTokenSource(RequestTimeout))
                {
                    var response = await Plugin.Client.GetAsync($"{server.Url}/session?cid={Uri.EscapeDataString(cid)}", timeout.Token);

                    if (!response.IsSuccessStatusCode)
                    {
                        answer.NotFound = response.StatusCode == System.Net.HttpStatusCode.NotFound;
                        return answer;
                    }

                    answer.State = JsonConvert.DeserializeObject<SessionState>(await response.Content.ReadAsStringAsync());
                }
            }
            catch { }

            return answer;
        }

        /// <summary>
        /// Reads the server list from the embedded Servers.json. Embedded rather than read from disk so it
        /// travels with the assembly the launcher installs - there is nothing else to deploy alongside it.
        /// </summary>
        private static List<Server> LoadServers()
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VatpacPlugin.Servers.json"))
                using (var reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<List<Server>>(reader.ReadToEnd()) ?? new List<Server>();
                }
            }
            catch
            {
                return new List<Server>();
            }
        }

        private static async void MMI_SelectedGroundTrackChanged(object sender, EventArgs e)
        {
            if (Network.IsOfficialServer) return;

            var callsign = MMI.SelectedGroundTrack?.GetFDR()?.Callsign;

            if (callsign == null) return;

            await SendToServer(callsign);
        }

        private static async void MMI_SelectedTrackChanged(object sender, EventArgs e)
        {
            if (Network.IsOfficialServer) return;

            var callsign = MMI.SelectedTrack?.GetFDR()?.Callsign;

            if (callsign == null) return;

            await SendToServer(callsign);
        }

        /// <summary>
        /// Tells the simulator which aircraft was just selected here.
        ///
        /// The CID matters on the multi world simulator: a server there runs one session per instructor
        /// rather than the single shared simulation the older ones do, and this is what tells it which of
        /// those sessions the selection belongs to - the same CID vatSys connected to that server with, so
        /// it resolves the session exactly as the FSD login did.
        ///
        /// Only from the instructor. The selection drives the instructor's own simulator page, so a
        /// student's clicks - same session, different vatSys - would keep pulling it off whatever the
        /// instructor was working on.
        /// </summary>
        private static async Task SendToServer(string callsign)
        {
            var server = ConnectedServer;

            if (server == null) return;

            if (!IsInstructor) return;

            var url = $"{server.Url}/select/{Uri.EscapeDataString(callsign)}";

            var cid = Network.ControllerId;

            if (!string.IsNullOrWhiteSpace(cid)) url += $"?cid={Uri.EscapeDataString(cid)}";

            try
            {
                await Plugin.Client.GetAsync(url);
            }
            catch { }
        }
    }
}
