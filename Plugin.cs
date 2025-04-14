﻿using Newtonsoft.Json;
using System;
using System.ComponentModel.Composition;
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
        public string Name => "VATPAC";
        public static string DisplayName => "VATPAC";

        public static readonly Version Version = new Version(0, 9);

        private static readonly string VersionUrl = "https://raw.githubusercontent.com/badvectors/VatpacPlugin/master/Version.json";

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

            SharedState.Init();

            _ = CheckVersion();

            // AllocConsole();
        }

        private static async Task CheckVersion()
        {
            try
            {
                var response = await Client.GetStringAsync(VersionUrl);

                var version = JsonConvert.DeserializeObject<Version>(response);

                if (version.Major == Version.Major && version.Minor == Version.Minor) return;

                Errors.Add(new Exception("A new version of the plugin is available."), DisplayName);
            }
            catch { }
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
