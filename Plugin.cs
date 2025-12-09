using Newtonsoft.Json;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using vatsys;
using vatsys.Plugin;

namespace VatpacPlugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        public static bool Testing = false;
        public static bool StateSavingDisabled = true;
        public string Name => "VATPAC";
        public static string DisplayName => "VATPAC";

        public static readonly Version Version = new Version(1, 21);

        public static readonly HttpClient Client = new HttpClient();
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

            if (Testing)
            {
                AllocConsole();
            }

            if (StateSavingDisabled) return;

            _ = SharedState.Init();
        }
        private void Network_Disconnected(object sender, EventArgs e)
        {
            if (StateSavingDisabled) return;
            SharedState.Disconnected();
        }

        private void Network_Connected(object sender, EventArgs e)
        {
            if (StateSavingDisabled) return;
            SharedState.Connected();
        }

        private void Audio_VSCSFrequenciesChanged(object sender, EventArgs e)
        {
            Extending.CheckEnroute();
            Extending.CheckApproach();
            Sectors.CheckActive();
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            if (StateSavingDisabled) return;
            SharedState.OnFdrUpdate(updated);
        }

        public async void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (StateSavingDisabled) return;
            await SharedState.OnRadarUpdate(updated);
        }
    }
}
