// GetCommand VM plugin: Get command_p text using voice AI STT
// Implements simple RMS VAD
// The durationSeconds parameter now becomes maxDurationSeconds
// Bruce Alexander 2024 v2

using vmAPI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using NAudio.Wave;

namespace GetCommandPlugin
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
                return "Get command_p text using voice AI STT\r\nArgument 1: Deepgram API key\r\nArgument 2: Maximum speech duration in seconds";
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

        // Get command_p text using voice AI STT
        // Argument 1: DEEPGRAM_API_KEY
        // Argument 2: Maximum speech duration in seconds
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

        static async Task<string> GetSTT(string apiKey, int maxDurationSeconds)
        {
            // Deepgram API endpoint
            string url = "https://api.deepgram.com/v1/listen?model=nova-2&smart_format=true";

            // Record audio from the microphone
            byte[] audioData = RecordAudioFromMicrophone(maxDurationSeconds);

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

        static byte[] RecordAudioFromMicrophone(int maxDurationSeconds)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var waveIn = new WaveInEvent())
                {
                    waveIn.WaveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono

                    object lockObject = new object();
                    bool voiceDetected = false;
                    int silenceCounter = 0;
                    const int silenceThreshold = 2; // 2 seconds of silence
                    const int checkIntervalMs = 100; // Check for silence every 100 ms
                    const double voiceActivityThreshold = 0.02; // Threshold for voice activity detection

                    waveIn.DataAvailable += (sender, e) =>
                    {
                        lock (lockObject)
                        {
                            memoryStream.Write(e.Buffer, 0, e.BytesRecorded);

                            // Calculate RMS to determine if voice activity is present
                            double rms = CalculateRms(e.Buffer, e.BytesRecorded);
                            if (rms > voiceActivityThreshold)
                            {
                                voiceDetected = true;
                                silenceCounter = 0;
                            }
                        }
                    };

                    waveIn.StartRecording();

                    DateTime recordingStartTime = DateTime.Now;
                    while (true)
                    {
                        Thread.Sleep(checkIntervalMs);
                        lock (lockObject)
                        {
                            if ((DateTime.Now - recordingStartTime).TotalSeconds >= maxDurationSeconds)
                            {
                                break;
                            }

                            if (!voiceDetected)
                            {
                                silenceCounter++;
                                if (silenceCounter * checkIntervalMs >= silenceThreshold * 1000)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                voiceDetected = false;
                            }
                        }
                    }

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

        private static double CalculateRms(byte[] buffer, int bytesRecorded)
        {
            int sampleCount = bytesRecorded / 2; // 2 bytes per sample (16-bit audio)
            double sumSquares = 0;

            for (int i = 0; i < bytesRecorded; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                double sample32 = sample / 32768.0; // Convert to -1 to 1 range
                sumSquares += sample32 * sample32;
            }

            return Math.Sqrt(sumSquares / sampleCount);
        }
    }

    public class IgnoreDisposeStream : Stream
    {
        private readonly Stream _innerStream;

        public IgnoreDisposeStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }

        protected override void Dispose(bool disposing)
        {
            // Don't dispose the inner stream
        }

        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;

        public override long Position
        {
            get => _innerStream.Position;
            set => _innerStream.Position = value;
        }

        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
    }
}