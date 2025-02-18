// GetHeadlines VM plugin: Get top three national news headlines_p
// v1.0.0.2
// Copyright © 2024 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GetHeadlinesPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.

    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "GetHeadlines";

        public string Description => "Get top three national news headlines_p\r\nArgument 1: News API key";

        public string ID => "6e85fa50-8496-4d7c-a289-fb53e82e7ff6";

        public void Init()
        {
            // Initialization routines go here
        }

        public void ReceiveParams(string Param1, string Param2, string Param3, bool Synchron)
        {
            // Remove quotes from arguments if present
            Param1 = Param1.Replace("\"", "");

            Task.Run(async () =>
            {
                string response = await GetHeadlines(Param1);

                // Set headlines_p string with ChatGPT response
                vmCommand.SetVariable("headlines_p", response);

                // Show command in VoiceMacro's log in blue as information
                vmCommand.AddLogEntry(response, Color.Blue, ID, "H", "Headlines response received");
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

        private const string UserAgent = "GetHeadlines/1.0 (newsmanupdate2024@gmail.com)";

        // Get top three national news headlines_p
        // Argument 1: News API key
        private static async Task<string> GetHeadlines(string apiKey)
        {
            string apiUrl = $"https://newsapi.org/v2/top-headlines?country=us&apiKey={apiKey}";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                HttpResponseMessage response = await client.GetAsync(apiUrl);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseObject = JsonConvert.DeserializeObject<NewsApiResponse>(responseString);
                    if (responseObject?.articles?.Count >= 3)
                    {
                        string headline1 = responseObject.articles[0]?.title ?? "No title";
                        string headline2 = responseObject.articles[1]?.title ?? "No title";
                        string headline3 = responseObject.articles[2]?.title ?? "No title";

                        return $"The top three national news stories from the previous day are {headline1} and ... {headline2} and ... {headline3}."
                        .Replace("\"", "")
                        .Replace("'", "")
                        .Replace(",", " ... ");
                    }
                    else
                    {
                        return "Not enough news articles found.";
                    }
                }
                else
                {
                    return responseString;
                }
            }
        }

        public class NewsApiResponse
        {
            public List<Article> articles { get; set; }
        }

        public class Article
        {
            public string title { get; set; }
        }
    }
}