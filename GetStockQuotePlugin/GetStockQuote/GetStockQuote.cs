// GetStockQuote VM plugin: Get stock price_p from symbol
// Bruce Alexander 2024 v2

using vmAPI;
using System;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GetStockQuotePlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.

    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "GetStockQuote";

        public string Description => "Get stock price_p from symbol\r\nArgument 1: Alpha Vantage API key\r\nArgument 2: Stock symbol";

        public string ID => "c079bdce-5441-4f98-bfc2-ab992cb7256e";

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
                string response = await GetQuote(Param1, Param2);

                // Set price_p string with Alpha Vantage response
                vmCommand.SetVariable("price_p", response);

                // Show command in VoiceMacro's log in blue as information
                vmCommand.AddLogEntry(response, Color.Blue, ID, "Q", "Stock quote received");
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

        // Get stock price_p from symbol
        // Argument 1: Alpha Vantage API key
        // Argument 2: Stock symbol
        private static async Task<string> GetQuote(string apiKey, string stockSymbol)
        {
            string apiUrl = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={stockSymbol}&apikey={apiKey}";

            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseObject = JsonConvert.DeserializeObject<AlphaVantageResponse>(responseString);

                    if (responseObject?.GlobalQuote != null)
                    {
                        string symbol = responseObject.GlobalQuote.Symbol;
                        string price = RoundUpToTwoDecimalPlaces(responseObject.GlobalQuote.Price);
                        string changePercent = RoundUpToTwoDecimalPlaces(responseObject.GlobalQuote.ChangePercent.TrimEnd('%'));

                        return $"{symbol} shares ended the last trading day at {price} dollars with a change of {changePercent}% from the previous close.";
                    }
                }
                else
                {
                    return "Error retrieving stock quote from Alpha Vantage API.";
                }

                return null;
            }
        }

        private static string RoundUpToTwoDecimalPlaces(string value)
        {
            if (decimal.TryParse(value, out decimal decimalValue))
            {
                decimal roundedValue = Math.Ceiling(decimalValue * 100) / 100;
                return roundedValue.ToString("F2");
            }
            return value;
        }
		
        public class AlphaVantageResponse
        {
            [JsonProperty("Global Quote")]
            public GlobalQuote GlobalQuote { get; set; }
        }

        public class GlobalQuote
        {
            [JsonProperty("01. symbol")]
            public string Symbol { get; set; }

            [JsonProperty("05. price")]
            public string Price { get; set; }

            [JsonProperty("10. change percent")]
            public string ChangePercent { get; set; }
        }
    }
}
