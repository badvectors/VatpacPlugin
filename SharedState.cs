using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using vatsys;
using static vatsys.FDP2;

namespace VatpacPlugin
{
    public class SharedState
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly HashSet<string> _trackedAircraft = new HashSet<string>();
        private readonly Dictionary<string, Aircraft> _toApply = new Dictionary<string, Aircraft>();

        private readonly string Server = "https://vss.prod1.badvectors.dev/api";
        private readonly List<string> Fields = new List<string>{ "LabelOpData", "State",
            "CFLUpper", "CFLLower", "CFLVisual", "GlobalOpData", "ControllerTracking" };

        public async Task Start()
        {
            //if (!Network.IsOfficialServer) return;
            //if (!Network.IsValidATC) return;
            await GetExisting();
        }

        public void End()
        {
            _trackedAircraft.Clear();
            _toApply.Clear();
        }

        public void OnFdrUpdate(FDP2.FDR updated)
        {
            var trackedAircraft = _trackedAircraft.Contains(updated.Callsign);

            if (trackedAircraft) return;

            _trackedAircraft.Add(updated.Callsign);

            updated.PropertyChanged += Fdr_PropertyChanged;
        }

        public void OnRadarUpdate(RDP.RadarTrack updated)
        {
            if (updated.ActualAircraft == null) return;

            var callsign = updated.ActualAircraft.Callsign;

            var success = _toApply.TryGetValue(callsign, out var aircraft);

            if (!success) return;

            var fdr = FDP2.GetFDRs.FirstOrDefault(x => x.Callsign == callsign);

            if (fdr == null) return;

            // COORDINATED, UNCONTROLLED && CONTROLLED
            if (aircraft.State == "STATE_COORDINATED")
            {
                ApplySharedState(aircraft, fdr);

                if (!fdr.ESTed) MMI.EstFDR(fdr);

                _toApply.Remove(fdr.Callsign);

                return;
            }

            // COORDINATED, UNCONTROLLED && CONTROLLED
            if (aircraft.State == "STATE_CONTROLLED" ||
                aircraft.State == "STATE_UNCONTROLLED")
            {
                if (fdr.ATD == DateTime.MaxValue)
                {
                    DepartFDR(fdr, DateTime.UtcNow.AddMinutes(-10));
                }

                if (!fdr.ESTed) MMI.EstFDR(fdr);

                if (updated.CoupledFDR == null) return;

                ApplySharedState(aircraft, updated.CoupledFDR);

                _toApply.Remove(callsign);

                return;
            }

            _toApply.Remove(callsign);
        }

        private async void Fdr_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Console.WriteLine(e.PropertyName);

            if (!Fields.Contains(e.PropertyName)) return;

            var fdr = (FDP2.FDR)sender;

            if (fdr.ControllerTracking != null && !fdr.IsTrackedByMe) return;

            switch (e.PropertyName)
            {
                case "State":
                    Console.WriteLine($"{fdr.Callsign} {e.PropertyName} {fdr.State}");
                    await SendState(fdr.Callsign, fdr.State.ToString());
                    break;
                case "ControllerTracking":
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
                    await SendScratchPad(fdr.Callsign, fdr.LabelOpData);
                    break;
                case "GlobalOpData":
                    await SendGlobal(fdr.Callsign, fdr.GlobalOpData);
                    break;
                default:
                    break;
            }

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

            if (aircraft.State == "STATE_CONTROLLED" &&
                aircraft.LastController != null &&
                aircraft.LastController == Network.Me.Callsign)
            {
                MMI.AcceptJurisdiction(fdr);
            }
        }

        private async Task SendState(string callsign, string scratchPad)
        {
            try
            {
                await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/State?value={scratchPad}", null);
            }
            catch { }
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
    }
}
