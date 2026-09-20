namespace VatpacPlugin
{
    /// <summary>
    /// What /session answers with - mirrors the server's SessionStateDto. Declared here rather than shared:
    /// the server's contracts assembly targets a framework this plugin can't reference, and a handful of
    /// booleans aren't worth coupling a vatSys plugin to it for.
    /// </summary>
    public class SessionState
    {
        /// <summary>
        /// Whether vatSys is connected to this server with this CID right now - not merely permitted into
        /// a session on it. This is what picks the server out (see Simulator.Connected). A server that
        /// predates the field leaves it false, so it is never mistaken for the one vatSys is on.
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>Whether this CID owns the session - the instructor running the scenario - rather than being a student permitted into someone else's.</summary>
        public bool IsInstructor { get; set; }

        /// <summary>Whether a scenario is loaded at all. A session with none isn't paused, it just hasn't started.</summary>
        public bool ScenarioLoaded { get; set; }

        /// <summary>Whether the simulation is running rather than paused.</summary>
        public bool Running { get; set; }

        /// <summary>
        /// Whether the server handles calls made on vatSys's own VSCS - see CoordLines. A server that
        /// predates the field leaves it false, which is the point of it: the line buttons must stay off
        /// against one of those.
        /// </summary>
        public bool Landlines { get; set; }

        /// <summary>Whether the server answers vatSys's frequency lookups - see VscsFrequencies. False from a server that predates the field, against which vatSys's voice client is left alone.</summary>
        public bool Frequencies { get; set; }
    }
}
