using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public class Plugin : IPlugin, IDisposable
    {
        public string Name => "VATPAC";
        public static string DisplayName => "VATPAC";

        private static string Server => "https://localhost:7013/api/Aircraft";
        private HttpClient _httpClient = new HttpClient();
        private HashSet<string> _checkedAircraft = new HashSet<string>();
        private HashSet<string> _trackedAircraft = new HashSet<string>();


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        public Plugin()
        {
            Audio.VSCSFrequenciesChanged += Audio_VSCSFrequenciesChanged;
            Audio.FrequencyErrorStateChanged += Audio_VSCSFrequenciesChanged;
            Network.PrimaryFrequencyChanged += Audio_VSCSFrequenciesChanged;
            Network.Disconnected += Network_Disconnected;

            AllocConsole();
        }

        private List<string> Fields = new List<string>{ "LabelOpData",
            "CFLUpper", "CFLLower", "CFLVisual", "GlobalOpData" };

        private async void Fdr_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Fields.Contains(e.PropertyName)) return;

            var fdr = (FDP2.FDR) sender;

            if (fdr.ControllerTracking != null && !fdr.IsTrackedByMe) return;

            switch (e.PropertyName)
            {
                case "CFLUpper":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.CFLUpper}");
                    await SendCFLUpper(fdr.Callsign, fdr.CFLUpper);
                    break;
                case "CFLLower":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.CFLLower}");
                    await SendCFLLower(fdr.Callsign, fdr.CFLLower);
                    break;
                case "CFLVisual":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.CFLVisual}");
                    await SendCFLVisual(fdr.Callsign, fdr.CFLVisual);
                    break;
                case "LabelOpData":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.LabelOpData}");
                    await SendScratchPad(fdr.Callsign, fdr.LabelOpData);
                    break;
                case "GlobalOpData":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.GlobalOpData}");
                    await SendGlobal(fdr.Callsign, fdr.GlobalOpData);
                    break;
                default:
                    break;
            }

        }

        private void Network_Disconnected(object sender, EventArgs e)
        {
            _checkedAircraft.Clear();
            _trackedAircraft.Clear();
        }

        private void Audio_VSCSFrequenciesChanged(object sender, EventArgs e)
        {
            CheckExtending();
        }

        private void CheckExtending()
        {
            if (!Network.Me.Callsign.EndsWith("_CTR")) return;

            var extending = string.Empty;

            foreach (var frequency in Audio.VSCSFrequencies)
            {
                if (!frequency.Transmit) continue;

                if (!frequency.Name.EndsWith("_CTR")) continue;

                if (frequency.Name == Network.Me.Callsign) continue;

                var shortName = frequency.Name
                    .Replace("_CTR", "")
                    .Replace("BN-", "")
                    .Replace("ML-", "");

                extending += $"{shortName} {Conversions.FrequencyToString(frequency.Frequency)} ";
            }

            if (extending != string.Empty)
            {
                extending = $"Extending {extending}";
            }

            UpdateInfo(extending);
        }

        private void UpdateInfo(string extending)
        {
            var controllerInfo = Network.ControllerInfo;

            if (controllerInfo == null) return;

            var lastLine = controllerInfo.Last();

            if (lastLine.StartsWith("Extending"))
            {
                controllerInfo = controllerInfo.Take(controllerInfo.Count() - 1).ToArray();
            }

            if (extending == string.Empty)
            {
                Network.ControllerInfo = controllerInfo.ToArray();

                return;
            }

            Network.ControllerInfo = controllerInfo.Append(extending).ToArray();
        }

        public async void OnFDRUpdate(FDP2.FDR updated)
        {
            var trackedAircraft = _trackedAircraft.Contains(updated.Callsign);

            if (!trackedAircraft)
            {
                Console.WriteLine($"{updated.Callsign}");

                _trackedAircraft.Add(updated.Callsign);

                updated.PropertyChanged += Fdr_PropertyChanged;
            }

            var checkedAircraft = _checkedAircraft.Contains(updated.Callsign);

            if (!checkedAircraft)
            {
                _checkedAircraft.Add(updated.Callsign);

                await CheckSharedState(updated.Callsign);
            }
        }

        private async Task SendScratchPad(string callsign, string scratchPad)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/{callsign}/ScratchPad?value={scratchPad}", null);
            }
            catch { }
        }

        private async Task SendGlobal(string callsign, string global)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/{callsign}/Global?value={global}", null);
            }
            catch { }
        }

        private async Task SendCFLUpper(string callsign, int cflUpper)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/{callsign}/CFLUpper?value={cflUpper}", null);
            }
            catch { }
        }

        private async Task SendCFLLower(string callsign, int cflLower)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/{callsign}/CFLLower?value={cflLower}", null);
            }
            catch { }
        }

        private async Task SendCFLVisual(string callsign, bool cflVisual)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/{callsign}/CFLVisual?value={cflVisual}", null);
            }
            catch { }
        }

        private async Task CheckSharedState(string callsign)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{Server}/{callsign}");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var aircraft = JsonConvert.DeserializeObject<Aircraft>(content);

                if (aircraft == null) return;

                var fdr = FDP2.GetFDRs.FirstOrDefault(x => x.Callsign == callsign);

                if (fdr == null) return;

                if (aircraft.Global != null)
                {
                    fdr.GlobalOpData = aircraft.Global;
                }

                if (aircraft.ScratchPad != null)
                {
                    fdr.LabelOpData = aircraft.ScratchPad;
                }

                if (aircraft.CFLUpper != null)
                {
                    fdr.CFLUpper = aircraft.CFLUpper.Value;
                }
            }
            catch { }
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {

            return;
        }

        public void Dispose()
        {

        }
    }
}
