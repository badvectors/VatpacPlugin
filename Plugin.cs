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
using static vatsys.FDP2;

namespace VatpacPlugin
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin, IDisposable
    {
        public string Name => "VATPAC";
        public static string DisplayName => "VATPAC";

        private static string Server => "https://vss.prod1.badvectors.dev/api";
        private HttpClient _httpClient = new HttpClient();
        private HashSet<string> _trackedAircraft = new HashSet<string>();
        private Dictionary<string, Aircraft> _toApply = new Dictionary<string, Aircraft>();


        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();

        public Plugin()
        {
            Audio.VSCSFrequenciesChanged += Audio_VSCSFrequenciesChanged;
            Audio.FrequencyErrorStateChanged += Audio_VSCSFrequenciesChanged;
            Network.PrimaryFrequencyChanged += Audio_VSCSFrequenciesChanged;
            Network.Disconnected += Network_Disconnected;
            Network.Connected += Network_Connected;
        }

        private List<string> Fields = new List<string>{ "LabelOpData",
            "CFLUpper", "CFLLower", "CFLVisual", "GlobalOpData", "ControllerTracking" };

        private async void Fdr_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!Fields.Contains(e.PropertyName)) return;

            var fdr = (FDP2.FDR) sender;

            if (fdr.ControllerTracking != null && !fdr.IsTrackedByMe) return;

            switch (e.PropertyName)
            {
                case "ControllerTracking":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.ControllerTracking?.Callsign}");
                    await SendControllerTracking(fdr.Callsign, fdr.ControllerTracking?.Callsign);
                    break;
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

        private async void Network_Connected(object sender, EventArgs e)
        {
            //if (!Network.IsOfficialServer) return;
            //if (!Network.IsValidATC) return;
            await GetExisting();
        }

        private void Network_Disconnected(object sender, EventArgs e)
        {
            _trackedAircraft.Clear();
            _toApply.Clear();
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

        private async Task SendScratchPad(string callsign, string scratchPad)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/ScratchPad?value={scratchPad}", null);
            }
            catch { }
        }

        private async Task SendGlobal(string callsign, string global)
        {
            try
            {
                var test = await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/Global?value={global}", null);

                test.EnsureSuccessStatusCode();
            }
            catch { }
        }

        private async Task SendControllerTracking(string callsign, string controllerTracking)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/ControllerTracking?value={controllerTracking}", null);
            }
            catch { }
        }

        private async Task SendCFLUpper(string callsign, int cflUpper)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/CFLUpper?value={cflUpper}", null);
            }
            catch { }
        }

        private async Task SendCFLLower(string callsign, int cflLower)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/CFLLower?value={cflLower}", null);
            }
            catch { }
        }

        private async Task SendCFLVisual(string callsign, bool cflVisual)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/CFLVisual?value={cflVisual}", null);
            }
            catch { }
        }

        private async Task GetExisting()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{Server}/Aircraft");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var existing = JsonConvert.DeserializeObject<List<Aircraft>>(content);

                foreach (var aircraft in existing)
                {
                    _toApply.Add(aircraft.Callsign, aircraft);
                }
            }
            catch { }
        }

        private void ApplySharedState(Aircraft aircraft, FDP2.FDR fdr)
        {
            if (aircraft == null) return;

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

            if (aircraft.PreviousTracking != null && 
                aircraft.PreviousTracking == Network.Me.Callsign)
            {
                MMI.AcceptJurisdiction(fdr);
            }
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            var trackedAircraft = _trackedAircraft.Contains(updated.Callsign);

            if (trackedAircraft) return;

            Console.WriteLine($"{updated.Callsign}");

            _trackedAircraft.Add(updated.Callsign);

            updated.PropertyChanged += Fdr_PropertyChanged;
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            if (updated.ActualAircraft == null) return;

            var callsign = updated.ActualAircraft.Callsign;

            var success = _toApply.TryGetValue(callsign, out var aircraft);

            if (!success) return;

            var fdr = FDP2.GetFDRs.FirstOrDefault(x => x.Callsign == callsign);

            if (fdr == null) return;

            // Add a ATD if required.
            if (fdr.ATD == DateTime.MaxValue)
            {
                DepartFDR(fdr, DateTime.UtcNow.AddMinutes(-10));
            }

            // Estimate and couple the track but don't accept it.
            if (!fdr.ESTed) MMI.EstFDR(fdr);

            if (updated.CoupledFDR == null) return;

            // Check if coming through the sector.
            if (!MMI.IsMySectorConcerned(fdr)) return;

            _toApply.Remove(callsign);

            ApplySharedState(aircraft, updated.CoupledFDR);
        }

        public void Dispose()
        {

        }
    }
}
