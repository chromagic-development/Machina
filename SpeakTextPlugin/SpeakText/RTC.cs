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
using WebRtcVadSharp;

namespace GetCommandPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();

        //The rest of interface code here.
    }

    public class VoiceMacro : vmInterface
    {
        // ... [Previous code remains unchanged]

        static async Task<string> GetSTT(string apiKey, int maxDurationSeconds)
        {
            // ... [Previous code remains unchanged]
        }

        static byte[] RecordAudioFromMicrophone(int maxDurationSeconds)
        {
            using (var memoryStream = new MemoryStream())
            using (var waveIn = new WaveInEvent())
            using (var vad = new WebRtcVad())
            {
                waveIn.WaveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono

                vad.SampleRate = WebRtcVadSharp.SampleRate.Is16kHz;
                vad.FrameLength = WebRtcVadSharp.FrameLength.Is10ms;
                vad.OperatingMode = WebRtcVadSharp.OperatingMode.HighQuality;

                object lockObject = new object();
                bool voiceDetected = false;
                int silenceCounter = 0;
                const int silenceThreshold = 200; // 2 seconds of silence (200 * 10ms frames)
                byte[] frameBuffer = new byte[320]; // 10ms of 16kHz 16-bit mono audio

                waveIn.DataAvailable += (sender, e) =>
                {
                    lock (lockObject)
                    {
                        memoryStream.Write(e.Buffer, 0, e.BytesRecorded);

                        // Process audio in 10ms frames
                        for (int i = 0; i < e.BytesRecorded; i += frameBuffer.Length)
                        {
                            int bytesToCopy = Math.Min(frameBuffer.Length, e.BytesRecorded - i);
                            Array.Copy(e.Buffer, i, frameBuffer, 0, bytesToCopy);

                            if (vad.HasSpeech(frameBuffer))
                            {
                                voiceDetected = true;
                                silenceCounter = 0;
                            }
                            else if (voiceDetected)
                            {
                                silenceCounter++;
                            }
                        }
                    }
                };

                waveIn.StartRecording();

                DateTime recordingStartTime = DateTime.Now;
                while (true)
                {
                    Thread.Sleep(10); // Check every 10ms
                    lock (lockObject)
                    {
                        if ((DateTime.Now - recordingStartTime).TotalSeconds >= maxDurationSeconds)
                        {
                            break;
                        }

                        if (voiceDetected && silenceCounter >= silenceThreshold)
                        {
                            break;
                        }
                    }
                }

                waveIn.StopRecording();

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

        // ... [Rest of the code remains unchanged]
    }

    // ... [IgnoreDisposeStream class remains unchanged]
}