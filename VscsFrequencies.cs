using System;
using System.Reflection;
using vatsys;

namespace VatpacPlugin
{
    /// <summary>
    /// Makes the frequency side of vatSys's VSCS work while it is connected to a simulator server: the
    /// frequency buttons down the left, Setup's Add Freq, and Idle/Receive/Transmit on each.
    ///
    /// vatSys builds all of that on its voice client (AFV). It only offers any of it while that client
    /// says it is connected, and it gets every frequency it shows by asking AFV's API for it by callsign -
    /// and it only ever connects that client from the live network. So this does two things to the client
    /// for the length of a session, by reflection, and undoes both at the end of it:
    ///
    /// It swaps the client's API connection for one pointed at the simulator server, which answers the
    /// same lookups from the session's own positions (the server's AfvController). The original is kept
    /// and put back untouched, so what vatSys uses on the network is never modified, only stepped around.
    /// The stand-in is marked as already signed in, so it never signs in - which is what would send a
    /// password - and the simulator is told the CID where AFV's token would go.
    ///
    /// And it marks the client connected. From there everything is vatSys's own code doing what it does on
    /// the network: its own frequencies load on connecting, Add Freq looks callsigns up, selecting
    /// Transmit tells the FSD server which frequencies this position is on.
    ///
    /// There is no voice and no connection to the real voice server, which is the point. vatSys's voice
    /// connection is never started - none of its tasks exist, nothing is sent anywhere - and the API
    /// requests vatSys makes go to the simulator or nowhere. The one loose end is that with Transmit
    /// selected, push-to-talk still encodes the microphone and queues it for a sender that isn't running,
    /// so the queue is emptied on every poll.
    ///
    /// It never engages on the live network (see Apply), and like CoordLines it fails open: the private
    /// members are bound to one build (0.4.9305) and resolved once, up front, and if any is missing
    /// Available is false and every call is a no-op - the frequency side stays as dead as it was.
    /// </summary>
    internal static class VscsFrequencies
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        private static readonly FieldInfo AfvInstance;
        private static readonly FieldInfo AfvClient;
        private static readonly PropertyInfo ClientConnection;
        private static readonly MethodInfo SetClientCallsign;
        private static readonly FieldInfo ConnectionData;
        private static readonly PropertyInfo TransmitQueue;
        private static readonly MethodInfo TransmitQueueTake;
        private static readonly PropertyInfo DataIsConnected;
        private static readonly PropertyInfo DataApi;
        private static readonly ConstructorInfo NewApi;
        private static readonly MethodInfo SetApiAuthenticated;
        private static readonly FieldInfo ApiJwt;
        private static readonly FieldInfo ApiUsername;
        private static readonly FieldInfo ApiExpiry;
        private static readonly FieldInfo AudioInstance;
        private static readonly MethodInfo LoadFrequencies;
        private static readonly MethodInfo UnloadFrequencies;

        private static readonly object Gate = new object();

        /// <summary>The API connection this put in place - how it tells its own from vatSys's - and the one it took out to make room.</summary>
        private static object _api;
        private static object _originalApi;

        /// <summary>What _api was built for. A different server or CID means a different one.</summary>
        private static string _url;
        private static string _cid;

        /// <summary>Whether the private members this needs were found. False means every call here does nothing.</summary>
        public static bool Available { get; }

        /// <summary>Whether vatSys's frequencies are currently being served by the simulator.</summary>
        public static bool Enabled { get; private set; }

        static VscsFrequencies()
        {
            try
            {
                AfvInstance = typeof(AFV).GetField("instance", Any);
                AfvClient = typeof(AFV).GetField("Client", Any);

                // Declared on the client's base classes, and a private setter is only visible from the
                // type that declares it - hence Declared() rather than asking the client's own type.
                ClientConnection = Declared(AfvClient?.FieldType, "Connection");
                SetClientCallsign = Declared(AfvClient?.FieldType, "Callsign")?.GetSetMethod(true);

                var connection = ClientConnection?.PropertyType;

                ConnectionData = connection?.GetField("connection", Any);
                TransmitQueue = connection?.GetProperty("VoiceServerTransmitQueue", Any);
                TransmitQueueTake = TransmitQueue?.PropertyType.GetMethod("TryTake", new[] { TransmitQueue.PropertyType.GetGenericArguments()[0].MakeByRefType() });

                var data = ConnectionData?.FieldType;

                DataIsConnected = data?.GetProperty("IsConnected", Any);
                DataApi = data?.GetProperty("ApiServerConnection", Any);

                var api = DataApi?.PropertyType;

                NewApi = api?.GetConstructor(new[] { typeof(string) });
                SetApiAuthenticated = api?.GetProperty("Authenticated", Any)?.GetSetMethod(true);
                ApiJwt = api?.GetField("jwt", Any);
                ApiUsername = api?.GetField("username", Any);
                ApiExpiry = api?.GetField("expiryLocalUtc", Any);

                AudioInstance = typeof(Audio).GetField("Instance", Any);
                LoadFrequencies = typeof(Audio).GetMethod("LoadFrequencies", Any, null, new[] { typeof(string), typeof(bool), typeof(bool) }, null);
                UnloadFrequencies = typeof(Audio).GetMethod("UnloadFrequencies", Any, null, Type.EmptyTypes, null);

                Available = AfvInstance != null && AfvClient != null && ClientConnection != null && SetClientCallsign != null
                    && ConnectionData != null && TransmitQueueTake != null
                    && DataIsConnected?.GetSetMethod(true) != null && DataApi?.GetSetMethod(true) != null
                    && NewApi != null && SetApiAuthenticated != null && ApiJwt != null && ApiUsername != null
                    && ApiExpiry != null && ApiExpiry.FieldType == typeof(DateTime)
                    && AudioInstance != null && LoadFrequencies != null && UnloadFrequencies != null;
            }
            catch
            {
                Available = false;
            }
        }

        /// <summary>
        /// The only entry point, called on every poll and on disconnect. A server URL means vatSys is
        /// positively known to be on a simulator server that answers the lookups; null is every other
        /// case, and puts everything back.
        /// </summary>
        public static void Apply(string serverUrl)
        {
            if (!Available) return;

            // Never on the live network, where this client is the real voice connection. Checked on every
            // call rather than once: the plugin stays loaded across connections.
            if (Network.IsOfficialServer || !Network.IsConnected) serverUrl = null;

            var cid = Network.ControllerId;
            var callsign = Network.Callsign;

            if (string.IsNullOrWhiteSpace(cid) || string.IsNullOrWhiteSpace(callsign)) serverUrl = null;

            lock (Gate)
            {
                try
                {
                    if (serverUrl != null) Enable(serverUrl, cid, callsign); else Disable();
                }
                catch
                {
                    // Half-applied is the one state that must not persist - put it back.
                    try { Disable(); } catch { Enabled = false; }
                }
            }
        }

        private static void Enable(string serverUrl, string cid, string callsign)
        {
            if (_api != null && (_url != serverUrl || _cid != cid)) Disable();

            var client = GetClient();

            var data = client == null ? null : ConnectionData.GetValue(ClientConnection.GetValue(client));

            if (data == null)
            {
                Enabled = false;
                return;
            }

            var current = DataApi.GetValue(data);

            if (_api != null && current == _api)
            {
                // Already in place. vatSys has no reason to mark it disconnected, but nothing is lost by
                // making sure, and this is the moment to clear out anything push-to-talk has queued.
                DataIsConnected.SetValue(data, true);
                Drain(client);
                Enabled = true;
                return;
            }

            // Connected on its own account, which off the live network it never should be - but if it
            // is, it isn't this class's to take over.
            if ((bool)DataIsConnected.GetValue(data))
            {
                Enabled = false;
                return;
            }

            var api = NewApi.Invoke(new object[] { serverUrl.TrimEnd('/') + "/afv" });

            // Signed in as far as it knows, and not due to renew until long after anyone has gone home -
            // renewing is signing in again, which is the one request that carries a password.
            SetApiAuthenticated.Invoke(api, new object[] { true });
            ApiExpiry.SetValue(api, DateTime.MaxValue);
            ApiUsername.SetValue(api, cid);
            ApiJwt.SetValue(api, cid);

            _originalApi = current;
            _api = api;
            _url = serverUrl;
            _cid = cid;

            DataApi.SetValue(data, api);
            SetClientCallsign.Invoke(client, new object[] { callsign });
            DataIsConnected.SetValue(data, true);

            Enabled = true;

            // What vatSys does for itself on connecting to voice: its own callsign's frequencies, quietly
            // (the last argument) so a position with none isn't reported as an error.
            var audio = AudioInstance.GetValue(null);

            if (audio != null) LoadFrequencies.Invoke(audio, new object[] { callsign, true, true });
        }

        private static void Disable()
        {
            Enabled = false;

            if (_api == null) return;

            var api = _api;
            var original = _originalApi;

            _api = null;
            _originalApi = null;
            _url = null;
            _cid = null;

            var client = GetClient();

            var data = client == null ? null : ConnectionData.GetValue(ClientConnection.GetValue(client));

            // Only if it is still the one in place - anything else there isn't this class's to touch.
            if (data == null || DataApi.GetValue(data) != api) return;

            DataIsConnected.SetValue(data, false);
            DataApi.SetValue(data, original);

            Drain(client);

            // And what vatSys does on losing voice: the frequencies go. They came from the simulator, so
            // off it they are not just stale but wrong.
            var audio = AudioInstance.GetValue(null);

            if (audio != null) UnloadFrequencies.Invoke(audio, null);
        }

        /// <summary>Empties the queue push-to-talk fills - see the class summary. There is no sender to take from it, so without this it only ever grows.</summary>
        private static void Drain(object client)
        {
            var queue = TransmitQueue.GetValue(ClientConnection.GetValue(client));

            if (queue == null) return;

            var item = new object[1];

            while ((bool)TransmitQueueTake.Invoke(queue, item)) { }
        }

        /// <summary>vatSys's voice client, or null before its audio has started (it is created with it, shortly after the plugin is).</summary>
        private static object GetClient()
        {
            var afv = AfvInstance.GetValue(null);

            return afv == null ? null : AfvClient.GetValue(afv);
        }

        /// <summary>A property as seen from the type that declares it, walking up from the given one.</summary>
        private static PropertyInfo Declared(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var property = type.GetProperty(name, Any | BindingFlags.DeclaredOnly);

                if (property != null) return property;
            }

            return null;
        }
    }
}
