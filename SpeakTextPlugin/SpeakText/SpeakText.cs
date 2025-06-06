﻿// SpeakText VM plugin: Speak text using voice AI TTS
// v1.0.0.8
// Copyright © 2024 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text;
using System.Drawing;
using NAudio.Wave;

namespace SpeakTextPlugin
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
                return "SpeakText";
            }
        }

        public string Description // description of plugin
        {
            get
            {
                return "Speak text using voice AI TTS\r\nArgument 1: Deepgram API key\r\nArgument 2: Aura voice model\r\nArgument 3: Spoken text\r\nspeaking_p: True when speaking\r\nstopspeak_p: True when user stops speech";
            }
        }

        public string ID
        {
            // Unique ID for plugin
            get
            {
                return "6bc8ca36-c218-42a2-8243-148c2d6adbb3";
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
            // Remove quotes from arguments if present
            Param1 = Param1.Replace("\"", "");
            Param2 = Param2.Replace("\"", "");

            // Initialize speaking_p variable to True before completion
            vmCommand.SetVariable("speaking_p", "True");

            Task.Run(async () =>
            {
                await SpeakText(Param1, Param2, Param3);

                // Set speaking_p variable to False after completion
                vmCommand.SetVariable("speaking_p", "False");
            });
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

        }
        #endregion

        // Speak text using voice AI TTS
        // Argument 1: Deepgram API key
        // Argument 2: Aura voice model
        // Argument 3: Spoken text
        async Task SpeakText(string apiKey, string model, string text)
        {
            // Remove unpronounceable characters
            text = text.Replace("\"", "")
                .Replace("*", "")
                .Replace("\n", " ... ")
                .Replace("\r", " ... ")
                .Replace("\r\n", " ... ")
                .Replace("\\", " divided by ")
                .Replace("#", " hash tag ")
                .Replace("U.S.", " United States ");

            // Make year ranges pronounceable
            string pattern = @"(?<=\d{4})-?(?=\d{4})";
            text = Regex.Replace(text, pattern, " to ");

            // Create JSON object with the text
            string json = $"{{\"text\": \"{text}\"}}";

            // URL to which to send the request, including the model parameter
            string url = $"https://api.deepgram.com/v1/speak?model={model}";

            // Create an instance of HttpClient
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    // Prepare the HTTP request content
                    HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Add Authorization header
                    httpClient.DefaultRequestHeaders.Add("Authorization", "token " + apiKey);

                    // Send the POST request
                    HttpResponseMessage response = await httpClient.PostAsync(url, content);

                    // Check if the request was successful
                    if (response.IsSuccessStatusCode)
                    {
                        // Read the audio stream directly into a MemoryStream
                        using (MemoryStream audioMemoryStream = new MemoryStream())
                        {
                            await response.Content.CopyToAsync(audioMemoryStream);
                            audioMemoryStream.Position = 0;

                            // Create a WaveStream from the MemoryStream
                            WaveStream waveStream = new Mp3FileReader(audioMemoryStream);

                            // Create a WaveOut device to play the audio
                            using (WaveOutEvent waveOut = new WaveOutEvent())
                            {
                                waveOut.Init(waveStream);
                                waveOut.Volume = 1.0f; // Set volume to maximum
                                waveOut.Play();

                                // Initialize stopspeak variable to False
                                vmCommand.SetVariable("stopspeak_p", "False");

                                // Periodically check if stopspeak_p is set to True
                                while (waveOut.PlaybackState == PlaybackState.Playing)
                                {
                                    await Task.Delay(100);

                                    // If stopspeak_p is TRUE, stop the audio
                                    if (vmCommand.GetVariable("stopspeak_p") == "True")
                                    {
                                        waveOut.Stop();
                                        vmCommand.AddLogEntry("Speech stopped by user", Color.Red, ID, "!", "TTS playback stopped");
                                        break;
                                    }
                                }
                            }
                        }
						if (vmCommand.GetVariable("stopspeak_p") == "False")
                        {
                        // Show TTS completed in VoiceMacro's log in purple as information
                        vmCommand.AddLogEntry("Speech completed", Color.Blue, ID, "S", "TTS completed");
						}
                    }
                    else
                    {
                        // Show TTS request in VoiceMacro's log in red as failure
                        vmCommand.AddLogEntry("TTS playback request failed: " + response.StatusCode, Color.Red, ID, "!", "TTS playback request failed");
                    }
                }
                catch (Exception ex)
                {
                    // Show TTS in VoiceMacro's log in red as failure
                    vmCommand.AddLogEntry("TTS Error: " + ex.Message, Color.Red, ID, "!", "TTS playback failed");
                }
            }
        }
    }
}