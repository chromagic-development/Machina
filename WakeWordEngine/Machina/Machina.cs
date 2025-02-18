// Wake Word Engine: Trigger virtual Alt-Z key combination for keyword model match
// Argument 1: Wake word model path (default is Machina.table)
// Argument 2: Path to VoiceMacro.exe (default is C:\Program Files (x86)\VoiceMacro\VoiceMacro.exe)
// v2.0.0.1
// Triggers VM macro with executable
// Copyright © 2024 Bruce Alexander
// This software is licensed under the MIT License. See LICENSE file for details.

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Diagnostics;

namespace KeywordRecognition
{
    class Machina
    {
        static async Task Main(string[] args)
        {
            // Path to the keyword recognition model file
            var wakewordModelPath = args.Length > 0 ? args[0] : @"Machina.table";

            // Path to VoiceMacro executable
            var voiceMacroPath = args.Length > 1 ? args[1] : @"C:\Program Files (x86)\VoiceMacro\VoiceMacro.exe";

            // Load the keyword recognition model
            var wakewordModel = KeywordRecognitionModel.FromFile(wakewordModelPath);

            // Configure the audio input from the default microphone
            using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            using var keywordRecognizer = new KeywordRecognizer(audioConfig);

            Console.WriteLine("Listening for Machina wake word...");

            // Subscribe to the Canceled event to get cancellation details
            keywordRecognizer.Canceled += (s, e) =>
            {
                Console.WriteLine($"Recognition canceled: {e.Reason}");
                if (e.Reason == CancellationReason.Error)
                {
                    Console.WriteLine($"Error details: {e.ErrorDetails}");
                }
            };

            // Continuous listening loop
            while (true)
            {
                try
                {
                    // Start keyword recognition
                    var result = await keywordRecognizer.RecognizeOnceAsync(wakewordModel);

                    // Process the recognition result
                    if (result.Reason == ResultReason.RecognizedKeyword)
                    {
                        // Console.WriteLine("Wake word detected!");
                        // Execute VM Command macro
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = voiceMacroPath,
                            Arguments = "/ExecuteMacro=\"Machina/Command\"",
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };

                        Process process = new Process
                        {
                            StartInfo = startInfo
                        };

                        process.Start();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }
    }
}