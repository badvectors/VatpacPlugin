using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using vatsys;
using vatsys.Plugin;

namespace VatpacPlugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        public string Name => "VATPAC";
        public static string DisplayName => "VATPAC";
        private static SharedState SharedState { get; set; } = new SharedState();

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

            SharedState.Init();

            // AllocConsole();
        }

        private void Network_Disconnected(object sender, EventArgs e)
        {
            SharedState.Disconnected();
        }

        private async void Network_Connected(object sender, EventArgs e)
        {
            await SharedState.Connected();
        }

        private void Audio_VSCSFrequenciesChanged(object sender, EventArgs e)
        {
            Extending.Check();
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            SharedState.OnFdrUpdate(updated);
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            SharedState.OnRadarUpdate(updated);
        }
    }
}
