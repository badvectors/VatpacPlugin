namespace VatpacPlugin
{
    /// <summary>
    /// What the last poll found. Shown on the Simulator page so that "nothing is happening" can be told
    /// apart from "nothing is meant to be happening" - see Simulator.Status.
    /// </summary>
    public enum SessionStatus
    {
        /// <summary>vatSys isn't connected to anything.</summary>
        NotConnected,

        /// <summary>Connected to the real network. Nothing here engages against live traffic, by design.</summary>
        OfficialNetwork,

        /// <summary>Just connected, and still asking the known servers which of them it was to.</summary>
        Searching,

        /// <summary>Connected, but none of the known servers say it's to them - so nothing is polled and nothing will ever freeze.</summary>
        NoServer,

        /// <summary>The server didn't answer, or didn't answer with anything that made sense.</summary>
        Unreachable,

        /// <summary>The server no longer has a session for this Controller ID - it ended, or access to it was removed.</summary>
        NoSession,

        /// <summary>In a session, but it has no scenario loaded yet.</summary>
        NoScenario,

        /// <summary>Running normally.</summary>
        Running,

        /// <summary>Paused - the one state that freezes the radar picture.</summary>
        Paused,
    }
}
