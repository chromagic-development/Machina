// AskVisionGPT VM plugin: Get response_p from prompt using LLM Vision AI
// Bruce Alexander 2024 v2

using vmAPI;
using System.Drawing;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenCvSharp;
using Newtonsoft.Json.Linq;
using System;

namespace AskVisionGPTPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.

    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "AskVisionGPT";

        public string Description => "Get response_p from prompt using LLM Vision AI\r\nArgument 1: OpenAI API key\r\nArgument 2: RTSP URL (blank for first camera device)\r\nArgument 3: Prompt text";

        public string ID => "3bcdd962-292c-4507-9818-504dc7b1ecce";

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
                // Call the async method and get the result
                string response = await AskVisionGPT(Param1, Param2, Param3);

                // Set response_p string with VisionGPT response
                vmCommand.SetVariable("response_p", response);

            // Show command in VoiceMacro's log in blue as information
            vmCommand.AddLogEntry(response, Color.Blue, ID, "C", "VisionGPT response received");
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

        // Get response_p from prompt using LLM Vision AI
        // Argument 1: OpenAI API key
        // Argument 2: RTSP URL or leave blank
        // Argument 3: Prompt text
        private static async Task<string> AskVisionGPT(string apiKey, string rtspUrl, string text)
        {
            // Capture an image from the RTSP stream
            byte[] imageBytes = CaptureImage(rtspUrl);

            // Encode the image to base64
            string base64Image = Convert.ToBase64String(imageBytes);

            // Create the JSON payload
            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                new {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = text },
                        new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64Image}" } }
                    }
                }
            },
                max_tokens = 300
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);

            // Send the request to the OpenAI API
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                string responseContent = await response.Content.ReadAsStringAsync();

                // Parse the response JSON and extract the content key
                var jsonResponse = JObject.Parse(responseContent);
                var contentValue = jsonResponse["choices"]?[0]?["message"]?["content"];

                return contentValue?.ToString() ?? "I don't see anything like that.";
            }
        }

        static byte[] CaptureImage(string rtspUrl)
        {
            // using (var capture = new VideoCapture(rtspUrl))
            using (var capture = rtspUrl.StartsWith("rtsp") ? new VideoCapture(rtspUrl) : new VideoCapture(0))
            {
                using (var image = new Mat())
                {
                    capture.Read(image);
                    if (!image.Empty())
                    {
                        return image.ToBytes(".jpg");
                    }
                    else
                    {
                        throw new Exception("Failed to capture image from the stream.");
                    }
                }
            }
        }
    }
}
