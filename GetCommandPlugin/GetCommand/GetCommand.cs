// GetCommand VM plugin: Get command_p text using voice AI STT
// v1.3.1.11
// Uses Deepgram Nova-3 model or OpenAI Whisper endpoint
// Combines VAD from WebRTC, Pre-roll buffering, Silence Detection, and a Dynamic RMS Energy Floor
// Pre-speech timeout is 5 seconds
// Pre-roll buffering is 1 second
// Set maxDurationSeconds for maximum listen time
// Set silenceThreshold in seconds
// Copyright © 2024-2025 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NAudio.Wave;
using WebRtcVadSharp;

namespace GetCommandPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();
    }

    public class VoiceMacro : vmInterface
    {
        #region "vmInterface"
        public string DisplayName => "GetCommand";
        public string Description => "Get command_p text using voice AI STT\r\nArgument 1: Deepgram API key or OpenAI Whisper endpoint (http)\r\nArgument 2: Maximum speech duration in seconds\r\nArgument 3: Silence threshold in seconds (optional)";
        public string ID => "f73e6ce3-ea89-484f-9516-1bc9c12d17bd";

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        void vmInterface.Init()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string pluginDir = Path.GetDirectoryName(assemblyPath);
                string nativeLibPath = Path.Combine(pluginDir, "WebRtcVad", "WebRtcVad.dll");

                if (File.Exists(nativeLibPath))
                {
                    IntPtr handle = LoadLibrary(nativeLibPath);
                }
            }
            catch (Exception) { }
        }

        void vmInterface.ReceiveParams(string Param1, string Param2, string Param3, bool Synchron)
        {
            Task.Run(() => GetCommand(Param1, Param2, Param3));
        }

        void vmInterface.ProfileSwitched(string ProfileGUID, string ProfileName) { }

        void vmInterface.Dispose()
        {
            // End wake word listener upon close
            Process[] processes = Process.GetProcessesByName("Machina");
            foreach (var proc in processes)
            {
                if (!proc.HasExited)
                {
                    proc.CloseMainWindow();
                    if (!proc.WaitForExit(5000)) proc.Kill();
                }
            }
        }
        #endregion

        async Task GetCommand(string param1, string param2, string param3)
        {
            if (string.IsNullOrEmpty(param3)) param3 = "2";
            param1 = param1.Replace("\"", "");

            int intParam2;
            int.TryParse(param2, out intParam2);

            int intParam3;
            int.TryParse(param3, out intParam3);

            if (intParam2 == 0 || intParam2 > 45) intParam2 = 5;

            string transcription;

            if (param1.StartsWith("http"))
                transcription = await GetSTTWhisper(param1, intParam2, intParam3);
            else
                transcription = await GetSTTDeepgram(param1, intParam2, intParam3);

            vmCommand.SetVariable("command_p", transcription);
            vmCommand.AddLogEntry(transcription, Color.Blue, ID, "V", "STT for command received");
        }

        async Task<string> GetSTTDeepgram(string apiKey, int maxDurationSeconds, int silenceThreshold)
        {
            string url = "https://api.deepgram.com/v1/listen?model=nova-3&smart_format=true";
            byte[] audioData = RecordAudioWithWebRTC(maxDurationSeconds, silenceThreshold);

            if (audioData == null || audioData.Length == 0) return "";

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpContent content = new ByteArrayContent(audioData);
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                    httpClient.DefaultRequestHeaders.Add("Authorization", "Token " + apiKey);

                    HttpResponseMessage response = await httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        JObject json = JObject.Parse(jsonResponse);
                        string transcript = (string)json["results"]?["channels"]?[0]?["alternatives"]?[0]?["transcript"];
                        return transcript ?? "Nothing.";
                    }
                    else return "API request failed: " + response.StatusCode;
                }
                catch (Exception ex) { return "Error: " + ex.Message; }
            }
        }

        async Task<string> GetSTTWhisper(string apiUrl, int maxDurationSeconds, int silenceThreshold)
        {
            byte[] audioData = RecordAudioWithWebRTC(maxDurationSeconds, silenceThreshold);
            if (audioData == null || audioData.Length == 0) return "";

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        content.Add(new ByteArrayContent(audioData), "file", "audio.wav");
                        content.Add(new StringContent("whisper-1"), "model");
                        HttpResponseMessage response = await httpClient.PostAsync(apiUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResponse = await response.Content.ReadAsStringAsync();
                            JObject json = JObject.Parse(jsonResponse);
                            return json["text"]?.ToString() ?? "Nothing.";
                        }
                        else return "API request failed: " + response.StatusCode;
                    }
                }
                catch (Exception ex) { return "Error: " + ex.Message; }
            }
        }

        byte[] RecordAudioWithWebRTC(int maxDurationSeconds, int silenceThreshold)
        {
            const int SampleRateHz = 16000;
            const int FrameDurationMs = 30;
            const int FrameSize = SampleRateHz * FrameDurationMs / 1000;
            const int FrameBytes = FrameSize * 2;

            // Roll back this amount of time to capture any words clipped before speech detection
            const int PreRollMilliseconds = 1000;

            // Wait this long for speech to start
            const int InitialSilenceTimeoutMs = 5000;

            using (var memoryStream = new MemoryStream())
            using (var waveIn = new WaveInEvent())
            using (var stopSignal = new ManualResetEvent(false))
            {
                try
                {
                    using (var vad = new WebRtcVad()
                    {
                        OperatingMode = OperatingMode.VeryAggressive,
                        SampleRate = SampleRate.Is16kHz
                    })
                    {
                        waveIn.WaveFormat = new WaveFormat(SampleRateHz, 16, 1);

                        List<byte> audioBuffer = new List<byte>();
                        Queue<byte[]> preRollQueue = new Queue<byte[]>();
                        int maxPreRollFrames = PreRollMilliseconds / FrameDurationMs;

                        bool speechStarted = false;
                        DateTime lastVoiceTime = DateTime.Now;
                        DateTime startTime = DateTime.Now;

                        // Initial silence value
                        float noiseFloor = 0.02f;
                        // Minimum allowed floor to prevent oversensitivity
                        float minNoiseFloor = 0.005f;
                        // Maximum allowed floor that can drown out voices
                        float maxNoiseFloor = 0.5f;
                        // Buffer above floor required for speech
                        float energyOffset = 0.03f;

                        waveIn.DataAvailable += (sender, e) =>
                        {
                            if (stopSignal.WaitOne(0)) return;

                            byte[] incoming = new byte[e.BytesRecorded];
                            Array.Copy(e.Buffer, incoming, e.BytesRecorded);
                            audioBuffer.AddRange(incoming);

                            while (audioBuffer.Count >= FrameBytes)
                            {
                                byte[] frame = audioBuffer.GetRange(0, FrameBytes).ToArray();
                                audioBuffer.RemoveRange(0, FrameBytes);

                                // Calculate RMS energy
                                float currentEnergy = CalculateRMS(frame);

                                // If current energy is quieter than initial floor, then drop the floor immediately
                                if (currentEnergy < noiseFloor)
                                {
                                    noiseFloor = currentEnergy;
                                    if (noiseFloor < minNoiseFloor) noiseFloor = minNoiseFloor;
                                }
                                else
                                {
                                    // If louder, then adapt to steady background noise before speech is detected to avoid rolling in the user's voice level
                                    if (!speechStarted)
                                    {
                                        noiseFloor += 0.0005f;
                                        if (noiseFloor > maxNoiseFloor) noiseFloor = maxNoiseFloor;
                                    }
                                }

                                // Calculate dynamic threshold of background noise + offset
                                float dynamicThreshold = noiseFloor + energyOffset;

                                // If energy is higher than background noise, then WebRTC decides if it's speech
                                bool isSpeech = (currentEnergy > dynamicThreshold) && vad.HasSpeech(frame);

                                if (isSpeech)
                                {
                                    lastVoiceTime = DateTime.Now;

                                    if (!speechStarted)
                                    {
                                        speechStarted = true;
                                        // Dump pre-roll
                                        while (preRollQueue.Count > 0)
                                        {
                                            byte[] preFrame = preRollQueue.Dequeue();
                                            memoryStream.Write(preFrame, 0, preFrame.Length);
                                        }
                                    }
                                    memoryStream.Write(frame, 0, frame.Length);
                                }
                                else
                                {
                                    // Silence
                                    if (speechStarted)
                                    {
                                        // Record silence to maintain flow
                                        memoryStream.Write(frame, 0, frame.Length);

                                        if ((DateTime.Now - lastVoiceTime).TotalSeconds >= silenceThreshold)
                                        {
                                            stopSignal.Set();
                                            return;
                                        }
                                    }
                                    else
                                    {
                                        // Buffering Pre-Roll
                                        preRollQueue.Enqueue(frame);
                                        if (preRollQueue.Count > maxPreRollFrames)
                                            preRollQueue.Dequeue();

                                        if ((DateTime.Now - startTime).TotalMilliseconds >= InitialSilenceTimeoutMs)
                                        {
                                            stopSignal.Set();
                                            return;
                                        }
                                    }
                                }
                            }

                            if ((DateTime.Now - startTime).TotalSeconds >= maxDurationSeconds)
                            {
                                stopSignal.Set();
                            }
                        };

                        startTime = DateTime.Now;
                        lastVoiceTime = DateTime.Now;
                        waveIn.StartRecording();
                        stopSignal.WaitOne();
                        waveIn.StopRecording();

                        if (memoryStream.Length == 0) return new byte[0];

                        memoryStream.Position = 0;
                        using (var outputStream = new MemoryStream())
                        {
                            using (var waveFileWriter = new WaveFileWriter(new IgnoreDisposeStream(outputStream), new WaveFormat(SampleRateHz, 16, 1)))
                            {
                                memoryStream.CopyTo(waveFileWriter);
                            }
                            return outputStream.ToArray();
                        }
                    }
                }
                catch (Exception ex)
                {
                    vmCommand.AddLogEntry("WebRtcVad Error: " + ex.Message, Color.Red, ID, "E", "Init Fail");
                    return null;
                }
            }
        }

        private float CalculateRMS(byte[] buffer)
        {
            float sum = 0;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                short sample = BitConverter.ToInt16(buffer, i);
                float sampleFloat = sample / 32768f;
                sum += sampleFloat * sampleFloat;
            }
            return (float)Math.Sqrt(sum / (buffer.Length / 2));
        }
    }

    public class IgnoreDisposeStream : Stream
    {
        private readonly Stream _innerStream;
        public IgnoreDisposeStream(Stream innerStream) { _innerStream = innerStream; }
        protected override void Dispose(bool disposing) { }
        public override bool CanRead => _innerStream.CanRead;
        public override bool CanSeek => _innerStream.CanSeek;
        public override bool CanWrite => _innerStream.CanWrite;
        public override long Length => _innerStream.Length;
        public override long Position { get => _innerStream.Position; set => _innerStream.Position = value; }
        public override void Flush() => _innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
        public override void SetLength(long value) => _innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
    }
}