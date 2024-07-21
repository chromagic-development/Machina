// Wake Word Engine: Trigger virtual Alt-Z key combination for keyword model match
// Argument 1: Wake word model path (default is Machina.table)
// Bruce Alexander 2024 v1

using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Runtime.InteropServices;

namespace KeywordRecognition
{
    class Machina
    {
        static async Task Main(string[] args)
        {
            // Path to the keyword recognition model file
            var wakewordModelPath = args.Length > 0 ? args[0] : @"Machina.table";

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
                        // Trigger virtual Alt-Z key combination
                        const int VK_Z = 0x5A;
                        const int VK_MENU = 0x12;
                        const int KEYEVENTF_KEYDOWN = 0x0000;
                        const int KEYEVENTF_KEYUP = 0x0002;

                        [DllImport("user32.dll")]
                        static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

                        keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYDOWN, 0);
                        keybd_event((byte)VK_Z, 0, KEYEVENTF_KEYDOWN, 0);
                        keybd_event((byte)VK_Z, 0, KEYEVENTF_KEYUP, 0);
                        keybd_event((byte)VK_MENU, 0, KEYEVENTF_KEYUP, 0);
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