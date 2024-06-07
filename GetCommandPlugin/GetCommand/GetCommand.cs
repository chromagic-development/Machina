// GetCommand VM plugin: Get command_p text using Deepgram STT API
// Bruce Alexander 2024 v1

using vmAPI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using NAudio.Utils;
using NAudio.Wave;

namespace GetCommandPluginCS
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.

    }

    public class VoiceMacro : vmInterface
    {

        // This region is required
        #region "vmInterface"
        public string DisplayName // the Name of plugin displayed in VoiceMacro
        {
            get
            {
                return "GetCommand";
            }
        }

        public string Description // description of plugin
        {
            get
            {
                return "Get command_p text using Deepgram STT API\r\nArgument 1: Deepgram API key\r\nArgument 2: Speech duration in seconds";
            }
        }

        public string ID
        {
            // Unique ID for plugin
            get
            {
                return "f73e6ce3-ea89-484f-9516-1bc9c12d17bd";
            }
        }

        // This is started when plugin is activated
        void vmInterface.Init()
        {
            // Initialization routines go here

        }

        // This is invoked when you use SendToPlugin action
        void vmInterface.ReceiveParams(string Param1, string Param2, string Param3, bool Synchron)
        {
            // Get command_p using Deepgram STT API
            // Argument 1: DEEPGRAM_API_KEY
            // Argument 2: Speech duration in seconds
            Task.Run(() => GetCommand(Param1, Param2));
        }

        // This is invoked when a profile is switched
        void vmInterface.ProfileSwitched(string ProfileGUID, string ProfileName)
        {
            // GetCommand plugin received profile switching to profile

        }

        // This is started when VoiceMacro is terminated
        void vmInterface.Dispose()
        {
            // Stop activities because VoiceMacro is shutting down
            // Terminate Machina Wake Word engine
            Process[] processes = Process.GetProcessesByName("Machina");
            foreach (var proc in processes)
            {
                proc.Kill();
            }
        }
        #endregion

        // Get command using Deepgram STT API
        // Argument 1: DEEPGRAM_API_KEY
        // Argument 2: Speech duration in seconds
        async Task GetCommand(string param1, string param2)
        {
            // Remove quotes
            param1 = param1.Replace("\"", "");

            // Convert seconds to integer
            int intParam2 = int.Parse(param2);

            // Enforce range for duration
            if (intParam2 == 0 || intParam2 > 20)
            {
                intParam2 = 5;
            }

            // Get command from STT
            string transcription = await GetSTT(param1, intParam2);

            // Set command variable from STT result
            vmCommand.SetVariable("command_p", transcription);

            // Show command in VoiceMacro's log in blue as information
            vmCommand.AddLogEntry(transcription, Color.Blue, ID, "V", "STT for command received");
        }

        static async Task<string> GetSTT(string apiKey, int durationSeconds)
        {
            // Deepgram API endpoint
            string url = "https://api.deepgram.com/v1/listen?model=nova-2&smart_format=true";

            // Record audio from the microphone
            byte[] audioData = RecordAudioFromMicrophone(durationSeconds);

            // Create an instance of HttpClient
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    // Prepare the HTTP request content
                    HttpContent content = new ByteArrayContent(audioData);

                    // Set the content type header
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");

                    // Add Authorization header
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Token " + apiKey);

                    // Send the POST request
                    HttpResponseMessage response = await httpClient.PostAsync(url, content);

                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        // Read the response content (transcription)
                        string jsonResponse = await response.Content.ReadAsStringAsync();

                        // Parse JSON response to extract the transcript
                        JObject json = JObject.Parse(jsonResponse);

                        string transcript = null;

                        if (json["results"] != null &&
                            json["results"]["channels"] != null &&
                            json["results"]["channels"][0] != null &&
                            json["results"]["channels"][0]["alternatives"] != null &&
                            json["results"]["channels"][0]["alternatives"][0] != null &&
                            json["results"]["channels"][0]["alternatives"][0]["transcript"] != null)
                        {
                            transcript = (string)json["results"]["channels"][0]["alternatives"][0]["transcript"];
                        }

                        // Return the transcript or a default message
                        return transcript ?? "Nothing.";
                    }
                    else
                    {
                        return "API request failed: " + response.StatusCode;
                    }
                }
                catch (Exception ex)
                {
                    return "Error: " + ex.Message;
                }
            }
        }

        static byte[] RecordAudioFromMicrophone(int durationSeconds)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var waveIn = new WaveInEvent())
                {
                    waveIn.WaveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono
                    waveIn.DataAvailable += (sender, e) =>
                    {
                        memoryStream.Write(e.Buffer, 0, e.BytesRecorded);
                    };

                    waveIn.StartRecording();

                    // Record for the specified duration
                    Thread.Sleep(durationSeconds * 1000);

                    waveIn.StopRecording();
                }

                // Ensure the stream is correctly positioned at the beginning
                memoryStream.Position = 0;

                // Write WAV header
                using (var waveFileWriter = new WaveFileWriter(new IgnoreDisposeStream(memoryStream), new WaveFormat(16000, 16, 1)))
                {
                    // Write the audio data to the wave file
                    waveFileWriter.Write(memoryStream.ToArray(), 0, (int)memoryStream.Length);
                    waveFileWriter.Flush();
                }

                return memoryStream.ToArray();
            }
        }
    }
}