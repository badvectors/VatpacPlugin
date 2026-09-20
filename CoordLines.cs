using System;
using System.Linq;
using System.Net;
using System.Reflection;
using vatsys;

namespace VatpacPlugin
{
    /// <summary>
    /// Switches on the hotline and coldline buttons of vatSys's own VSCS while it is connected to a
    /// simulator server, so coordination with the instructor's simulated controllers happens on the real
    /// panel rather than a web page standing in for it.
    ///
    /// Almost none of that needs anything from here. A landline call is signalled over ordinary FSD, which
    /// the simulator's server speaks: it lists the simulated controllers as callable, turns a line pressed
    /// in vatSys into a call on the instructor's Coordination page, and sends the instructor's side back
    /// so the line rings, connects and drops (tones and all) exactly as it would on the network.
    ///
    /// What is left is a single test. vatSys greys the line buttons out, and ignores presses on them,
    /// unless it is connected to its voice server (Mumble) - and it only ever connects to that from the
    /// live network. There is no setting for it and nothing in the plugin API, so this makes the test come
    /// out true by reflection: it hands vatSys's Mumble protocol object a connection that reports itself
    /// connected and was never opened.
    ///
    /// That is safe because of what vatSys does with the connection afterwards, which without a voice
    /// server is nothing. Opening a line unmutes the other controller's Mumble user, and there are no
    /// users, so it finds nobody and stops. Microphone audio is only sent while at least one user is
    /// unmuted, so none is. Nothing is ever written to the socket that isn't there. And it carries no
    /// voice, which is no different from before: instructor and student talk however they already do.
    ///
    /// vatSys closing the connection itself is expected - it does on every disconnect - and simply means
    /// the next Apply puts a new one in. It never engages on the live network, where the connection in
    /// that slot is real: see Apply.
    ///
    /// The reflection is bound to private members of one build (0.4.9305), which any release may rename,
    /// so like RadarFreeze every part of this fails open. It resolves once, up front; if anything is
    /// missing, Available is false, every call is a no-op and the buttons just stay grey.
    /// </summary>
    internal static class CoordLines
    {
        private static readonly FieldInfo MumbleInstance;
        private static readonly FieldInfo MumbleProtocol;
        private static readonly PropertyInfo ProtocolConnection;
        private static readonly ConstructorInfo NewConnection;
        private static readonly PropertyInfo ConnectionState;
        private static readonly object StateConnected;

        private static readonly object Gate = new object();

        /// <summary>The connection this put in place, so it can tell its own from a real one and never touches the latter.</summary>
        private static object _connection;

        /// <summary>Whether the private members this needs were found. False means every call here does nothing.</summary>
        public static bool Available { get; }

        /// <summary>Whether the line buttons are currently switched on by this.</summary>
        public static bool Enabled { get; private set; }

        static CoordLines()
        {
            try
            {
                const BindingFlags any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

                var mumble = typeof(Network).Assembly.GetType("vatsys.Mumble");

                MumbleInstance = mumble?.GetField("instance", any);
                MumbleProtocol = mumble?.GetField("protocol", any);

                ProtocolConnection = MumbleProtocol?.FieldType.GetProperty("Connection", any);

                var connection = ProtocolConnection?.PropertyType;

                // The constructor that takes an endpoint rather than a host name: the other one resolves
                // the name as it is called, and this one touches nothing.
                NewConnection = connection?.GetConstructors().FirstOrDefault(x =>
                {
                    var parameters = x.GetParameters();

                    return parameters.Length == 3
                        && parameters[0].ParameterType == typeof(IPEndPoint)
                        && parameters[1].ParameterType.IsAssignableFrom(MumbleProtocol.FieldType)
                        && parameters[2].ParameterType == typeof(bool);
                });

                ConnectionState = connection?.GetProperty("State", any);

                if (ConnectionState != null) StateConnected = Enum.Parse(ConnectionState.PropertyType, "Connected");

                Available = MumbleInstance != null && MumbleProtocol != null && NewConnection != null && StateConnected != null
                    && ProtocolConnection?.GetSetMethod(true) != null
                    && ConnectionState.GetSetMethod(true) != null;
            }
            catch
            {
                Available = false;
            }
        }

        /// <summary>
        /// The only entry point, called on every poll and on disconnect. Enables only while positively
        /// told vatSys is on a simulator server that handles the calls; everything else is a call with
        /// false, which puts things back. Has to keep being called while enabled, because vatSys throws
        /// the connection away whenever it disconnects.
        /// </summary>
        public static void Apply(bool enable)
        {
            if (!Available) return;

            // Never on the live network, where what is in this slot is the real voice connection.
            // Checked on every call rather than once: the plugin stays loaded across connections.
            if (Network.IsOfficialServer) enable = false;

            // vatSys puts its own address in every call it makes, and gets that address from the server
            // at login. Without one a pressed line fails inside vatSys rather than here, so the buttons
            // stay off until it has arrived.
            if (enable && !IPAddress.TryParse(Network.OurIP ?? string.Empty, out _)) enable = false;

            lock (Gate)
            {
                try
                {
                    if (enable) Enable(); else Disable();
                }
                catch
                {
                    Enabled = false;
                }
            }
        }

        private static void Enable()
        {
            var protocol = GetProtocol();

            if (protocol == null)
            {
                Enabled = false;
                return;
            }

            var current = ProtocolConnection.GetValue(protocol);

            if (current != null && current != _connection)
            {
                // Somebody else's - which can only be vatSys's own. Not this class's to replace.
                Enabled = false;
                return;
            }

            if (current == null)
            {
                _connection = NewConnection.Invoke(new[] { new IPEndPoint(IPAddress.Loopback, 0), protocol, (object)false });

                ProtocolConnection.GetSetMethod(true).Invoke(protocol, new[] { _connection });
            }

            // Every time, not just on a new one: vatSys closing it leaves it in place but disconnected.
            ConnectionState.GetSetMethod(true).Invoke(_connection, new[] { StateConnected });

            Enabled = true;
        }

        private static void Disable()
        {
            Enabled = false;

            if (_connection == null) return;

            var protocol = GetProtocol();

            // Only if it is still the one in the slot - vatSys may have cleared or replaced it already.
            if (protocol != null && ProtocolConnection.GetValue(protocol) == _connection)
            {
                ProtocolConnection.GetSetMethod(true).Invoke(protocol, new object[] { null });
            }

            _connection = null;
        }

        /// <summary>vatSys's Mumble protocol object, or null before its audio has started (it is created with it, shortly after the plugin is).</summary>
        private static object GetProtocol()
        {
            var mumble = MumbleInstance.GetValue(null);

            return mumble == null ? null : MumbleProtocol.GetValue(mumble);
        }
    }
}
