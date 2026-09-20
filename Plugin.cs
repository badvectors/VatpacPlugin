using System;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace VatpacPlugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        public static bool Testing = false;
        public string Name => "VATPAC";
        public static string DisplayName => "VATPAC";

        public static readonly HttpClient Client = new HttpClient();

        private static CustomToolStripMenuItem _menu;
        private static VatpacWindow _window;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        public Plugin()
        {
            Audio.VSCSFrequenciesChanged += Audio_VSCSFrequenciesChanged;
            Audio.FrequencyErrorStateChanged += Audio_VSCSFrequenciesChanged;
            Network.PrimaryFrequencyChanged += Audio_VSCSFrequenciesChanged;
            Network.Connected += Network_Connected;
            Network.Disconnected += Network_Disconnected;

            _menu = new CustomToolStripMenuItem(CustomToolStripMenuItemWindowType.Main, CustomToolStripMenuItemCategory.Windows, new ToolStripMenuItem(DisplayName));
            _menu.Item.Click += Menu_Click;
            MMI.AddCustomMenuItem(_menu);

            Simulator.Init();

            if (Testing)
            {
                AllocConsole();
            }
        }

        private void Menu_Click(object sender, EventArgs e)
        {
            MMI.InvokeOnGUI((MethodInvoker)delegate ()
            {
                if (_window == null || _window.IsDisposed)
                {
                    _window = new VatpacWindow();
                }
                else if (_window.Visible) return;

                _window.Show();
            });
        }

        private void Network_Disconnected(object sender, EventArgs e)
        {
            Simulator.Disconnected();
        }

        private void Network_Connected(object sender, EventArgs e)
        {
            Sectors.Init();

            if (Network.IsOfficialServer) return;

            Simulator.Connected();
        }

        private void Audio_VSCSFrequenciesChanged(object sender, EventArgs e)
        {
            Extending.CheckEnroute();
            Extending.CheckApproach();
            Sectors.CheckActive();
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
        }

        public async void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
        }
    }
}