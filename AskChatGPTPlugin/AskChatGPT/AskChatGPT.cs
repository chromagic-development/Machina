// AskChatGPT VM plugin: Get response_p from prompt using LLM AI
// Bruce Alexander 2024 v2

using vmAPI;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace AskChatGPTPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.

    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "AskChatGTP";

        public string Description => "Get response_p from prompt using LLM AI\r\nArgument 1: OpenAI API key\r\nArgument 2: ChatGPT model\r\nArgument 3: Prompt text";

        public string ID => "de96fc6f-0409-4d3a-8d1a-dc7ba5e718c1";

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
                string response = await AskChatGPT(Param1, Param2, Param3);

                // Set response_p string with ChatGPT response
                vmCommand.SetVariable("response_p", response);

                // Show command in VoiceMacro's log in blue as information
                vmCommand.AddLogEntry(response, Color.Blue, ID, "C", "ChatGPT response received");
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

        // Get response_p from prompt using LLM AI
        // Argument 1: OpenAI API key
        // Argument 2: ChatGPT mode
        // Argument 3: Prompt text
        private static async Task<string> AskChatGPT(string apiKey, string model, string prompt)
        {
            string apiUrl = "https://api.openai.com/v1/chat/completions";

            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                string jsonBody = JsonConvert.SerializeObject(requestBody);
                HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var responseObject = JsonConvert.DeserializeObject<OpenAIResponse>(responseString);

                    if (responseObject?.choices?.Count > 0 && responseObject.choices[0]?.message?.content != null)
                    {
                        string result = responseObject.choices[0].message.content;
                        return result;
                    }
                }
                else
                { 
                    return responseString; 
                }
                return null;
            }
        }

        public class OpenAIResponse
        {
            public List<Choice> choices { get; set; }
        }

        public class Choice
        {
            public Message message { get; set; }
        }

        public class Message
        {
            public string content { get; set; }
        }
    }
}
