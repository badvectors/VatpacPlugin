using System;
using System.ComponentModel.Composition;
using System.Net.Http;
using System.Runtime.InteropServices;
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

            if (Testing)
            {
                AllocConsole();
            }
        }
        private void Network_Disconnected(object sender, EventArgs e)
        {
        }

        private void Network_Connected(object sender, EventArgs e)
        {
            Sectors.Init();
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