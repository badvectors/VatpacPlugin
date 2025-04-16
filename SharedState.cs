using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Xml;
using vatsys;
using static vatsys.FDP2;
using Timer = System.Timers.Timer;

namespace VatpacPlugin
{
    public class SharedState
    {
        private readonly HashSet<string> _trackedAircraft = new HashSet<string>();
        private readonly Dictionary<string, Aircraft> _toApply = new Dictionary<string, Aircraft>();

        private readonly string Server = "http://localhost:5126/api";
        //private readonly string Server = "https://vss.prod1.badvectors.dev/api";

        private readonly List<string> Fields = new List<string>{ "LabelOpData", "State",
            "CFLUpper", "CFLLower", "CFLVisual", "GlobalOpData", "ControllerTracking", //"ParsedRoute" 
        };

        private Guid? Token = null;
        private DateTime? ExpiryUtc = null;
        private Timer TokenTimer = new Timer();
        private Settings Settings = null;

        public async void Init()
        {
            GetSettings();

            await GetToken();

            TokenTimer.Elapsed += new ElapsedEventHandler(TokenTimer_Elapsed);
            TokenTimer.Interval = TimeSpan.FromMinutes(30).TotalMilliseconds;
            TokenTimer.AutoReset = true;

            TokenTimer.Start();
        }

        public async Task Connected()
        {
            await GetExisting();
        }

        public void Disconnected()
        {
            _trackedAircraft.Clear();

            _toApply.Clear();
        }

        private async Task GetToken()
        {
            if (Settings == null)
            {
                Errors.Add(new Exception("Could not load settings."), Plugin.DisplayName);
                return;
            }

            if (Token == null)
            {
                await Login(Settings.CID, DecryptString(Settings.Password));
                return;
            }

            if (ExpiryUtc != null && ExpiryUtc < DateTime.UtcNow)
            {
                await Login(Settings.CID, DecryptString(Settings.Password));
                return;
            }

            await Refresh(Settings.CID);
        }

        private async void TokenTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await GetToken();
        }

        public void OnFdrUpdate(FDP2.FDR updated)
        {
            if (!Network.IsValidATC) return;

            if (!Network.IsOfficialServer) return;

            if (_trackedAircraft.Contains(updated.Callsign)) return;

            _trackedAircraft.Add(updated.Callsign);

            updated.PropertyChanged += Fdr_PropertyChanged;
        }

        public void OnRadarUpdate(RDP.RadarTrack updated)
        {
            if (!Network.IsValidATC) return;

            if (!Network.IsOfficialServer) return;

            if (updated.ActualAircraft == null) return;

            var callsign = updated.ActualAircraft.Callsign;

            var success = _toApply.TryGetValue(callsign, out var aircraft);

            if (!success) return;

            var fdr = FDP2.GetFDRs.FirstOrDefault(x => x.Callsign == callsign);

            if (fdr == null) return;

            // COORDINATED
            if (aircraft.State == "STATE_COORDINATED")
            {
                ApplySharedState(aircraft, fdr);

                if (!fdr.ESTed) MMI.EstFDR(fdr);

                _toApply.Remove(fdr.Callsign);

                return;
            }

            //  UNCONTROLLED 
            if (aircraft.State == "STATE_UNCONTROLLED")
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

            //  CONTROLLED
            if (aircraft.State == "STATE_CONTROLLED")
            {
                if (fdr.ATD == DateTime.MaxValue)
                {
                    DepartFDR(fdr, DateTime.UtcNow.AddMinutes(-10));
                }

                if (!fdr.ESTed) MMI.EstFDR(fdr);

                if (updated.CoupledFDR == null) return;

                ApplySharedState(aircraft, updated.CoupledFDR);

                if (aircraft.LastController != null && aircraft.LastController == Network.Me.Callsign)
                {
                    MMI.AcceptJurisdiction(fdr);
                }

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
                //case "ParsedRoute":
                //    var positions = new List<Position>();
                //    foreach (var point in fdr.ParsedRoute)
                //    {
                //        if (!point.IsPETO) continue;
                //        positions.Add(new Position(point.Intersection.Name, point.ETO, point.ATO, point.MPRArmed));
                //    }
                //    if (!positions.Any()) break;
                //    await SendPositions(fdr.Callsign, positions);
                //    break;
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
                var response = await Plugin.Client.GetAsync($"{Server}/Aircraft");

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var existing = JsonConvert.DeserializeObject<List<Aircraft>>(content);

                foreach (var aircraft in existing)
                {
                    if (aircraft == null) continue;

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

            //if (aircraft.Positions.Any())
            //{
            //    foreach (var position in aircraft.Positions)
            //    {
            //        var point = fdr.ParsedRoute.FirstOrDefault(x => x.Intersection.Name == position.Name);

            //        if (point == null) continue;

            //        point.ETO = point.ETO;
            //        point.ATO = point.ATO; 
                    
            //    }
            //}
        }

        //private async Task SendPositions(string callsign, List<Position> positions)
        //{
        //    try
        //    {
        //        var json = JsonConvert.SerializeObject(positions);

        //        var stringContent = new StringContent(json, Encoding.UTF8, "application/json");

        //        await _httpClient.PostAsync($"{Server}/Aircraft/{callsign}/Positions", stringContent);
        //    }
        //    catch { }
        //}

        private async Task CheckTokenError(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();

            if (content != "Invalid token." && content != "Token expired.") return;

            Token = null;

            ExpiryUtc = null;

            await GetToken();
        }

        private async Task SendState(string callsign, string state)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/State?value={state}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
                
            }
            catch { }
        }

        private async Task SendScratchPad(string callsign, string scratchPad)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/ScratchPad?value={scratchPad}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
            }
            catch { }
        }

        private async Task SendGlobal(string callsign, string global)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/Global?value={global}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
            }
            catch { }
        }

        private async Task SendControllerTracking(string callsign, string controllerTracking)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/ControllerTracking?value={controllerTracking}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
            }
            catch { }
        }

        private async Task SendCFLUpper(string callsign, int cflUpper)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/CFLUpper?value={cflUpper}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
            }
            catch { }
        }

        private async Task SendCFLLower(string callsign, int cflLower)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/CFLLower?value={cflLower}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
            }
            catch { }
        }

        private async Task SendCFLVisual(string callsign, bool cflVisual)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Aircraft/{callsign}/CFLVisual?value={cflVisual}", null);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await CheckTokenError(response);
            }
            catch { }
        }

        private async Task Login(int cid, string password)
        {
            try
            {
                var login = new Login(cid, password);

                var response = await Plugin.Client.PostAsync($"{Server}/Atc/{cid}/Login", 
                    new StringContent(JsonConvert.SerializeObject(login), Encoding.UTF8, "application/json"));

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var atc = JsonConvert.DeserializeObject<Atc>(content);

                Token = atc.Token;

                ExpiryUtc = atc.ExpiryUtc;

                UpdateTokenOnClient();
            }
            catch (Exception e) 
            {
                Errors.Add(new Exception($"Could login: {e.Message}"), Plugin.DisplayName);
            }
        }

        private async Task Refresh(int cid)
        {
            try
            {
                var response = await Plugin.Client.PostAsync($"{Server}/Atc/{cid}/Refresh", null);

                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();

                var atc = JsonConvert.DeserializeObject<Atc>(content);

                Token = atc.Token;

                ExpiryUtc = atc.ExpiryUtc;

                UpdateTokenOnClient();
            }
            catch (Exception e)
            {
                Errors.Add(new Exception($"Could refresh token: {e.Message}"), Plugin.DisplayName);
            }
        }

        private async Task Logout(int cid)
        {
            try
            {
                await Plugin.Client.PostAsync($"{Server}/Atc/{cid}/Logout", null);

                UpdateTokenOnClient();
            }
            catch { }
        }

        private void UpdateTokenOnClient()
        {
            Plugin.Client.DefaultRequestHeaders.Remove("vss-token");

            if (Token != null)
            {
                Plugin.Client.DefaultRequestHeaders.Add("vss-token", Token.Value.ToString());
            }
        }

        private void GetSettings()
        {
            var configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);

            if (!configuration.HasFile) return;

            if (!File.Exists(configuration.FilePath)) return;

            var config = File.ReadAllText(configuration.FilePath);

            Settings settings = new Settings();

            XmlDocument xmlDocument = new XmlDocument();

            xmlDocument.LoadXml(config);

            var cidString = string.Empty;

            foreach (XmlNode childNode in xmlDocument.DocumentElement.SelectSingleNode("userSettings").SelectSingleNode("vatsys.Properties.Settings").ChildNodes)
            {
                if (childNode.Attributes.GetNamedItem("name").Value == "VATSIMID")
                    cidString = childNode.InnerText;
                else if (childNode.Attributes.GetNamedItem("name").Value == "Password")
                    settings.Password = childNode.InnerText;
            }

            var success = int.TryParse(cidString, out var cid);

            if (!success)
            {
                Errors.Add(new Exception("Could not load settings."), Plugin.DisplayName);
                return;
            }

            settings.CID = cid;

            Settings = settings;
        }

        private string DecryptString(string encryptedData)
        {
            if (encryptedData == null) return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), Encoding.UTF8.GetBytes(Settings.Entropy), DataProtectionScope.CurrentUser));
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
