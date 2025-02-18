// GetWeather VM plugin: Get forecast_p for City, State using NWS
// v1.0.0.2
// Copyright © 2024 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GetWeatherPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.

    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "GetWeather";

        public string Description => "Get forecast_p for City, State using NWS\r\nArgument 1: Open Cage API key\r\nArgument 2: City\r\nArgument 3: State";

        public string ID => "6907fe53-5cd1-4d0a-be30-417134832d2e";

        public void Init()
        {
            // Initialization routines go here
        }

        public void ReceiveParams(string Param1, string Param2, string Param3, bool Synchron)
        {
            // Remove quotes from arguments if present
            Param1 = Param1.Replace("\"", "");
            Param2 = Param2.Replace("\"", "");

            Task.Run(async () =>
            {
                string response = await GetWeather(Param1, Param2, Param3);

                // Set forecast_p string with weather forecast
                vmCommand.SetVariable("forecast_p", response);

                // Show command in VoiceMacro's log in blue as information
                vmCommand.AddLogEntry(response, Color.Blue, ID, "W", "Weather forecast received");
            });
        }

        public void ProfileSwitched(string ProfileGUID, string ProfileName)
        {
            // GetCommand plugin received profile switching to profile
        }

        public void Dispose()
        {
            // Stop activities because VoiceMacro is shutting down
        }

        private static readonly HttpClient client = new HttpClient();
        private const string UserAgent = "GetWeather/1.0 (weathermanupdate2024@gmail.com)";

        // Get forecast_p for City, State using NWS
        // Argument 1: Open Cage API key
        // Argument 2: City
        // Argument 3: State
        private static async Task<string> GetWeather(string openCageApiKey, string city, string state)
        {
            string response;

            try
            {
                var (latitude, longitude) = await GetCoordinates(city, state, openCageApiKey);
                var forecast = await GetWeatherForecast(latitude, longitude);
                response = $"The upcoming weather for {city}, {state} is {forecast}";
            }
            catch (Exception ex)
            {
                // Give error message
                response = ex.Message;
            }
            return response;
        }

        static async Task<(double, double)> GetCoordinates(string city, string state, string openCageApiKey)
        {
            string url = $"https://api.opencagedata.com/geocode/v1/json?q={Uri.EscapeDataString(city + ", " + state)}&key={openCageApiKey}";
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            var response = await client.GetStringAsync(url);
            var json = JObject.Parse(response);

            var results = json["results"];
            if (results.HasValues)
            {
                var location = results[0]["geometry"];
                double latitude = location["lat"].ToObject<double>();
                double longitude = location["lng"].ToObject<double>();
                return (latitude, longitude);
            }
            throw new Exception("Coordinates not found for the given city and state.");
        }

        static async Task<string> GetWeatherForecast(double latitude, double longitude)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

            string url = $"https://api.weather.gov/points/{latitude},{longitude}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error getting weather points: {response.ReasonPhrase}");
            }

            var json = JObject.Parse(await response.Content.ReadAsStringAsync());
            string forecastUrl = json["properties"]["forecast"].ToString();

            response = await client.GetAsync(forecastUrl);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error getting forecast: {response.ReasonPhrase}");
            }

            json = JObject.Parse(await response.Content.ReadAsStringAsync());
            var periods = json["properties"]["periods"];

            // Check for "Today" or use the first period as a fallback
            foreach (var period in periods)
            {
                if (period["name"].ToString().ToLower().Contains("today"))
                {
                    return period["detailedForecast"].ToString();
                }
            }

            // Fallback to the first period if "Today" is not found
            if (periods.HasValues)
            {
                return periods[0]["detailedForecast"].ToString();
            }

            return "Forecast data not available.";
        }
    }
}
