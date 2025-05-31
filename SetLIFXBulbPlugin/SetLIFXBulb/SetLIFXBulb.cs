// SetLIFXBulb VM plugin: Set LIFX light bulb on or off
// v1.0.1.1
// Copyright © 2025 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SetLIFXBulbPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();
    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "SetLIFXBulb";

        public string Description => "Set LIFX light bulb on or off\r\nArgument 1: LIFX Access Token\r\nArgument 2: Bulb Label\r\nArgument 3: Power (on/off)";

        public string ID => "906a41d9-68e2-4394-9272-b6293a2eb2f1";

        public void Init()
        {
            // Initialization routines
        }

        public void ReceiveParams(string Param1, string Param2, string Param3, bool Synchron)
        {
            // Remove quotes from arguments if present
            Param1 = Param1.Replace("\"", "");
            Param2 = Param2.Replace("\"", "");
            Param3 = Param3.Replace("\"", "");

            Task.Run(async () =>
            {
                string response = await SetLIFXState(Param1, Param2, Param3);

                // Set response string to VM variable
                vmCommand.SetVariable("LIFX_p", response);

                // Log in blue if success, red if error
                Color logColor = response.StartsWith("Power") ? Color.Blue : Color.Red;
                vmCommand.AddLogEntry(response, logColor, ID, "L", "LIFX bulb power set");
            });
        }

        public void ProfileSwitched(string ProfileGUID, string ProfileName)
        {
            // Profile switching handler
        }

        public void Dispose()
        {
            // Cleanup when VoiceMacro shuts down
        }

        private static async Task<string> SetLIFXState(string accessToken, string label, string powerState)
        {
            string safeLabel = Uri.EscapeDataString(label);
            string apiUrl = $"https://api.lifx.com/v1/lights/label:{safeLabel}/state";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var payload = new
                {
                    power = powerState.ToLower(),   // "on" or "off"
                };

                string jsonPayload = JsonConvert.SerializeObject(payload);
                HttpContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PutAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return $"Power {powerState} {label}";
                }
                else
                {
                    string errorDetails = await response.Content.ReadAsStringAsync();
                    return $"Error: {response.StatusCode} - {errorDetails}";
                }
            }
        }
    }
}
