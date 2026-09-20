using System;
using System.Windows.Forms;
using vatsys;

namespace VatpacPlugin
{
    /// <summary>
    /// The plugin's one window, opened from Windows &gt; VATPAC, with a page per feature picked from the
    /// dropdown at the top. A new page is a TabPage added to pages in the designer - the dropdown is
    /// filled from the pages' own Text and the constructor colours every page it finds, so there's
    /// nothing to register here.
    /// </summary>
    public partial class VatpacWindow : BaseForm
    {
        /// <summary>
        /// Refreshes the Simulator page's status lines. A WinForms timer, so it ticks on the UI thread and
        /// can touch the label directly. Only runs while the window is open - there's nothing to show
        /// otherwise.
        /// </summary>
        private readonly Timer _simulatorTimer = new Timer { Interval = 1000 };

        public VatpacWindow()
        {
            InitializeComponent();

            BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);

            // Pages don't inherit the form's colours - a TabPage defaults to the Windows theme's.
            foreach (TabPage page in pages.TabPages)
            {
                page.UseVisualStyleBackColor = false;
                page.BackColor = BackColor;
                page.ForeColor = ForeColor;

                comboBoxPage.Items.Add(page.Text);
            }

            comboBoxPage.SelectedIndex = pages.SelectedIndex;

            _simulatorTimer.Tick += (s, e) => ShowSimulatorStatus();
            FormClosed += (s, e) => _simulatorTimer.Stop();
        }

        private void VatpacWindow_Load(object sender, EventArgs e)
        {
            LoadSimulatorPage();
        }

        private void ComboBoxPage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxPage.SelectedIndex >= 0) pages.SelectedIndex = comboBoxPage.SelectedIndex;
        }

        /// <summary>The other direction: Ctrl+Tab still turns the pages of a TabControl with no tabs showing, and the dropdown has to follow.</summary>
        private void Pages_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxPage.SelectedIndex = pages.SelectedIndex;
        }

        #region Simulator

        private void LoadSimulatorPage()
        {
            ShowSimulatorStatus();

            _simulatorTimer.Start();
        }

        /// <summary>
        /// Says plainly what the plugin is doing, because every way it can do nothing looks identical from
        /// the outside. The server is found rather than picked (see Simulator.Connected), so which one it
        /// found - or that it found none - is the first thing to say.
        /// </summary>
        private void ShowSimulatorStatus()
        {
            var connectedServer = Simulator.ConnectedServer;

            string server;
            string session;

            switch (Simulator.Status)
            {
                case SessionStatus.NotConnected: server = "vatSys not connected"; session = "-"; break;
                case SessionStatus.OfficialNetwork: server = "live network - inactive"; session = "-"; break;
                case SessionStatus.Searching: server = "searching..."; session = "-"; break;
                case SessionStatus.NoServer: server = "not a simulator server"; session = "-"; break;
                case SessionStatus.Unreachable: server = null; session = "SERVER NOT RESPONDING"; break;
                case SessionStatus.NoSession: server = null; session = "NO SESSION FOR THIS CID"; break;
                case SessionStatus.NoScenario: server = null; session = "NO SCENARIO LOADED"; break;
                case SessionStatus.Running: server = null; session = "RUNNING"; break;
                case SessionStatus.Paused: server = null; session = "PAUSED"; break;
                default: server = null; session = "-"; break;
            }

            // The poll that sets Status runs a second behind a connect or disconnect, so go by what is
            // known now rather than printing a server alongside a status from before there was one.
            if (server == null) server = connectedServer == null ? "-" : connectedServer.Name;

            var role = connectedServer == null ? "-" : Simulator.IsInstructor ? "Instructor" : "Student";

            var radar = RadarFreeze.Frozen
                ? "HELD"
                : RadarFreeze.Available ? "LIVE" : "HOLD UNAVAILABLE";

            var coord = CoordLines.Enabled
                ? "ON"
                : !CoordLines.Available ? "UNAVAILABLE"
                : connectedServer != null && !Simulator.Landlines ? "NOT ON THIS SERVER" : "OFF";

            var freqs = VscsFrequencies.Enabled
                ? "ON"
                : !VscsFrequencies.Available ? "UNAVAILABLE"
                : connectedServer != null && !Simulator.Frequencies ? "NOT ON THIS SERVER" : "OFF";

            labelSimulatorStatus.Text = string.Join(Environment.NewLine, new[]
            {
                "Server:   " + server,
                "Role:     " + role,
                "Session:  " + session,
                "Radar:    " + radar,
                "Coord:    " + coord,
                "Freqs:    " + freqs,
            });
        }

        #endregion
    }
}
