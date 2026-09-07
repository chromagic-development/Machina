// GetCommand VM plugin: Get command_p text using voice AI STT
// v1.6.0.15
// Uses Deepgram Nova-3 model or OpenAI Whisper endpoint
// Combines Silero VAD (v5, ONNX), Pre-roll buffering, Silence Detection, and a fixed RMS
// Copyright © 2024-2026 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NAudio.Wave;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace GetCommandPlugin
{
    public static class Interface_Manager
    {
        static Interface_Manager()
        {
            // The host app has no binding redirects for our dependencies, so resolve
            // any version of a DLL we ship from the plugin folder.
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string name = new AssemblyName(args.Name).Name;
                    string candidate = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), name + ".dll");
                    return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
                }
                catch (Exception) { return null; }
            };
        }

        public static VoiceMacro vmInstance = new VoiceMacro();
    }

    public class VoiceMacro : vmInterface
    {
        #region "vmInterface"
        public string DisplayName => "GetCommand";
        public string Description => "Get command_p text using voice AI STT\r\nArgument 1: Deepgram API key or OpenAI Whisper endpoint (http)\r\nArgument 2: Maximum speech duration in seconds (optionally provide language code before seconds)\r\nArgument 3: Silence threshold in seconds (optionally follow by ':' and initial silence timeout in s)";
        public string ID => "f73e6ce3-ea89-484f-9516-1bc9c12d17bd";

        void vmInterface.Init()
        {
            try
            {
                string assemblyPath = Assembly.GetExecutingAssembly().Location;
                string pluginDir = Path.GetDirectoryName(assemblyPath);

                // Warm up the model so the first command doesn't pay the load cost.
                // EnsureSession also preloads the right-arch native onnxruntime, so the
                // plugin works even in hosts that never call Init().
                SileroVad.EnsureSession(GetModelPath(pluginDir));
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
            SileroVad.DisposeSession();

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

        // RMS level (0..1) a frame must exceed to count as the user speaking into the mic.
        // Everything below it - room tone, instrumental music, vocal music - is ignored on
        // level alone, so no background estimate is needed. Tied to microphone gain: if the
        // mic or its input level changes, retune this constant.
        private const float SpeechEnergyFloor = 0.04f;

        // How long to wait for speech to start before giving up, when argument 3 carries
        // no ":<seconds>" suffix
        private const int DefaultInitialSilenceTimeoutMs = 5000;

        private static string GetModelPath(string pluginDir)
        {
            string path = Path.Combine(pluginDir, "silero_vad.onnx");
            if (!File.Exists(path)) path = Path.Combine(pluginDir, "SileroVad", "silero_vad.onnx");
            return path;
        }

        // Get command_p text using voice AI STT
        // Argument 1: DEEPGRAM_API_KEY or OpenAI Whisper endpoint (http)
        // Argument 2: Maximum speech duration in seconds (optionally provide language code before seconds)
        // Argument 3: Silence threshold in seconds (optionally follow by ':' and initial silence timeout in s)
        async Task GetCommand(string param1, string param2, string param3)
        {
            if (string.IsNullOrEmpty(param3)) param3 = "2";
            param1 = param1.Replace("\"", "");
            param2 = (param2 ?? "").Trim();

            // Argument 2 may lead with an alphabetic BCP-47 / ISO language code before the
            // number, e.g. "IT2" = Italian, 2 seconds. A bare number leaves the language
            // unset and the STT service uses its default (English).
            int digitsStart = 0;
            while (digitsStart < param2.Length && (char.IsLetter(param2[digitsStart]) || param2[digitsStart] == '-'))
                digitsStart++;

            string language = "";
            string langCode = param2.Substring(0, digitsStart).Trim('-');
            if (langCode.Length > 0)
            {
                // Normalize casing to canonical BCP-47: lowercase language, uppercase region
                string[] parts = langCode.Split('-');
                parts[0] = parts[0].ToLowerInvariant();
                for (int i = 1; i < parts.Length; i++)
                    parts[i] = parts[i].Length == 2 ? parts[i].ToUpperInvariant() : parts[i].ToLowerInvariant();
                language = string.Join("-", parts);
            }

            int intParam2;
            int.TryParse(param2.Substring(digitsStart), out intParam2);

            // Argument 3 may carry an optional ":<seconds>" suffix overriding how long the
            // recorder waits for speech to start, e.g. "2:10", so the initial wait can be
            // retuned per macro without a rebuild.
            string[] param3Parts = param3.Split(':');

            int initialSilenceTimeoutMs = DefaultInitialSilenceTimeoutMs;
            if (param3Parts.Length > 1)
            {
                int parsedTimeout;
                // Invariant culture: the suffix is authored as a plain integer regardless of locale
                if (int.TryParse(param3Parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedTimeout)
                    && parsedTimeout > 0)
                    initialSilenceTimeoutMs = parsedTimeout * 1000;
            }

            int intParam3;
            int.TryParse(param3Parts[0].Trim(), out intParam3);

            if (intParam2 == 0 || intParam2 > 45) intParam2 = 5;
            // Unparseable, zero, or negative silence thresholds would end capture on the
            // first quiet frame, so fall back to the 2-second default
            if (intParam3 <= 0) intParam3 = 2;

            string transcription;

            if (param1.StartsWith("http"))
                transcription = await GetSTTWhisper(param1, intParam2, intParam3, language, initialSilenceTimeoutMs);
            else
                transcription = await GetSTTDeepgram(param1, intParam2, intParam3, language, initialSilenceTimeoutMs);

            vmCommand.SetVariable("command_p", transcription);
            vmCommand.AddLogEntry(transcription, Color.Blue, ID, "V", "STT for command received");
        }

        async Task<string> GetSTTDeepgram(string apiKey, int maxDurationSeconds, int silenceThreshold, string language, int initialSilenceTimeoutMs)
        {
            string url = "https://api.deepgram.com/v1/listen?model=nova-3"
                + (string.IsNullOrEmpty(language) ? "" : "&language=" + Uri.EscapeDataString(language))
                + "&smart_format=true";
            byte[] audioData = RecordAudioWithSilero(maxDurationSeconds, silenceThreshold, initialSilenceTimeoutMs);

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

        async Task<string> GetSTTWhisper(string apiUrl, int maxDurationSeconds, int silenceThreshold, string language, int initialSilenceTimeoutMs)
        {
            byte[] audioData = RecordAudioWithSilero(maxDurationSeconds, silenceThreshold, initialSilenceTimeoutMs);
            if (audioData == null || audioData.Length == 0) return "";

            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    using (var content = new MultipartFormDataContent())
                    {
                        content.Add(new ByteArrayContent(audioData), "file", "audio.wav");
                        content.Add(new StringContent("whisper-1"), "model");
                        // Whisper expects a bare ISO-639-1 code, so send only the primary subtag
                        if (!string.IsNullOrEmpty(language))
                            content.Add(new StringContent(language.Split('-')[0]), "language");
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

        byte[] RecordAudioWithSilero(int maxDurationSeconds, int silenceThreshold, int initialSilenceTimeoutMs)
        {
            const int SampleRateHz = 16000;
            const int FrameSize = SileroVad.ChunkSamples;                  // 512 samples, fixed by the model
            const int FrameBytes = FrameSize * 2;
            const int FrameDurationMs = FrameSize * 1000 / SampleRateHz;   // 32 ms

            // Roll back this amount of time to capture any words clipped before speech detection
            const int PreRollMilliseconds = 1000;

            using (var memoryStream = new MemoryStream())
            using (var waveIn = new WaveInEvent())
            using (var stopSignal = new ManualResetEvent(false))
            {
                try
                {
                    // Shared ONNX session, fresh recurrent state for this recording
                    string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var vad = new SileroVad(GetModelPath(pluginDir));

                    waveIn.WaveFormat = new WaveFormat(SampleRateHz, 16, 1);

                    List<byte> audioBuffer = new List<byte>();
                    Queue<byte[]> preRollQueue = new Queue<byte[]>();
                    int maxPreRollFrames = PreRollMilliseconds / FrameDurationMs;

                    bool speechStarted = false;
                    DateTime lastVoiceTime = DateTime.Now;
                    DateTime startTime = DateTime.Now;

                    // Silero speech-probability thresholds, with hysteresis: a higher bar
                    // to start speech than to keep it (Silero's own recommended pattern).
                    const float OnsetProbability = 0.5f;
                    const float ContinueProbability = 0.35f;

                    // The level gate (SpeechEnergyFloor) and the model answer different
                    // questions, and each covers the other's blind spot: the floor rejects
                    // anything quieter than the user - including vocal music, which Silero
                    // scores as speech - while Silero rejects loud non-speech such as door
                    // slams, coughs and keystrokes, which the floor alone would let through.

                    // Sliding window so sparse frames misflagged by the VAD don't keep refreshing.
                    Queue<bool> voiceWindow = new Queue<bool>();
                    int voiceCount = 0;
                    const int VoiceWindowFrames = 10;          // ~320 ms at 32 ms/frame
                    const float VoiceRatioThreshold = 0.5f;    // majority of recent frames must be voice

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

                            // Convert once to float samples for both RMS and the model
                            float[] samples = new float[FrameSize];
                            for (int i = 0; i < FrameSize; i++)
                                samples[i] = BitConverter.ToInt16(frame, i * 2) / 32768f;

                            // Loud enough to be the user rather than background, whatever the background is
                            float currentEnergy = CalculateRMS(samples);
                            bool loudEnough = currentEnergy > SpeechEnergyFloor;

                            // Run Silero on every frame to keep its recurrent state continuous
                            float speechProbability = vad.Process(samples);

                            if (!speechStarted)
                            {
                                bool onsetSpeech = loudEnough && speechProbability >= OnsetProbability;
                                if (onsetSpeech)
                                {
                                    speechStarted = true;
                                    lastVoiceTime = DateTime.Now;

                                    // Dump pre-roll, then the triggering frame.
                                    while (preRollQueue.Count > 0)
                                    {
                                        byte[] preFrame = preRollQueue.Dequeue();
                                        memoryStream.Write(preFrame, 0, preFrame.Length);
                                    }
                                    memoryStream.Write(frame, 0, frame.Length);
                                }
                                else
                                {
                                    // Buffering Pre-Roll
                                    preRollQueue.Enqueue(frame);
                                    if (preRollQueue.Count > maxPreRollFrames)
                                        preRollQueue.Dequeue();

                                    if ((DateTime.Now - startTime).TotalMilliseconds >= initialSilenceTimeoutMs)
                                    {
                                        stopSignal.Set();
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                // DURING SPEECH: always record so the audio stays continuous.
                                memoryStream.Write(frame, 0, frame.Length);

                                // Continuation gate: same level floor, lower model bar. Quiet
                                // trailing syllables dropping under the floor is intentional - it
                                // ends the utterance sooner, and every frame is recorded regardless.
                                bool frameVoice = loudEnough && speechProbability >= ContinueProbability;

                                // Smooth over a short window: a few stray misflagged frames won't refresh.
                                voiceWindow.Enqueue(frameVoice);
                                if (frameVoice) voiceCount++;
                                if (voiceWindow.Count > VoiceWindowFrames && voiceWindow.Dequeue()) voiceCount--;

                                bool voiceActive = ((float)voiceCount / voiceWindow.Count) >= VoiceRatioThreshold;

                                if (voiceActive)
                                {
                                    lastVoiceTime = DateTime.Now;
                                }
                                else if ((DateTime.Now - lastVoiceTime).TotalSeconds >= silenceThreshold)
                                {
                                    stopSignal.Set();
                                    return;
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
                catch (Exception ex)
                {
                    // Include the inner-exception chain: for load failures the outer message
                    // (e.g. a type-initializer error) hides the actual cause
                    string message = ex.Message;
                    for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                        message += " -> " + inner.Message;
                    vmCommand.AddLogEntry("Silero VAD Error: " + message, Color.Red, ID, "E", "Init Fail");
                    return null;
                }
            }
        }

        private float CalculateRMS(float[] samples)
        {
            float sum = 0;
            for (int i = 0; i < samples.Length; i++)
                sum += samples[i] * samples[i];
            return (float)Math.Sqrt(sum / samples.Length);
        }
    }

    // Thin wrapper around the Silero VAD v5 ONNX model: returns a speech probability
    // (0..1) per 512-sample chunk. The InferenceSession is shared process-wide; each
    // instance carries its own recurrent state, so create one per recording session.
    public class SileroVad
    {
        public const int ChunkSamples = 512;    // 32 ms at 16 kHz, fixed by the v5 model
        private const int ContextSamples = 64;  // the model wants the previous chunk's tail prepended

        private static InferenceSession session;
        private static DenseTensor<long> srTensor;
        private static readonly object sessionLock = new object();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        // Resolve the folder the plugin actually runs from. Location can be empty
        // when a host loads the assembly from memory, so fall back to CodeBase.
        private static string PluginDir()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string path = asm.Location;
            if (string.IsNullOrEmpty(path))
            {
                try { path = new Uri(asm.CodeBase).LocalPath; } catch (Exception) { }
            }
            return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path);
        }

        // Load the native onnxruntime matching the process bitness before the managed
        // layer needs it: the P/Invoke probe never searches the plugin folder, so
        // without this the load fails in hosts whose app dir isn't the plugin dir.
        // Returns a description of what happened, for error reporting.
        private static string PreloadNative()
        {
            string dir = PluginDir();
            if (dir == null) return "plugin folder could not be resolved";
            string arch = Environment.Is64BitProcess ? "x64" : "x86";
            string path = Path.Combine(dir, "onnx", arch, "onnxruntime.dll");
            if (!File.Exists(path)) path = Path.Combine(dir, "onnxruntime.dll");
            if (!File.Exists(path)) return "onnxruntime.dll (" + arch + ") not found under " + dir;
            if (LoadLibrary(path) != IntPtr.Zero) return path;
            return path + " failed to load, Win32 error " + Marshal.GetLastWin32Error();
        }

        private float[] state = new float[2 * 1 * 128];
        private readonly float[] context = new float[ContextSamples];

        public SileroVad(string modelPath)
        {
            EnsureSession(modelPath);
        }

        public static void EnsureSession(string modelPath)
        {
            lock (sessionLock)
            {
                if (session != null) return;

                // Works regardless of whether the host ever called the plugin's Init()
                string nativeResult = PreloadNative();

                if (!File.Exists(modelPath))
                    throw new FileNotFoundException("Silero VAD model not found: " + modelPath);

                try
                {
                    var options = new SessionOptions();
                    // The model is tiny; a thread pool costs more than it saves
                    options.IntraOpNumThreads = 1;
                    options.InterOpNumThreads = 1;
                    var s = new InferenceSession(modelPath, options);

                    if (!s.InputMetadata.ContainsKey("state"))
                    {
                        s.Dispose();
                        throw new InvalidOperationException("Model is not Silero VAD v5 (missing 'state' input) - use silero_vad.onnx from the v5 release");
                    }

                    // "sr" is a scalar in v5; build the tensor to whatever rank the model declares
                    srTensor = s.InputMetadata["sr"].Dimensions.Length == 0
                        ? new DenseTensor<long>(new long[] { 16000 }, new int[0])
                        : new DenseTensor<long>(new long[] { 16000 }, new[] { 1 });

                    session = s;
                }
                catch (TypeInitializationException ex)
                {
                    // The managed layer couldn't bind the native onnxruntime; say exactly
                    // what the preload attempted so the log pinpoints the cause
                    throw new InvalidOperationException("ONNX Runtime native load failed. Native preload: " + nativeResult, ex);
                }
            }
        }

        public static void DisposeSession()
        {
            lock (sessionLock)
            {
                if (session != null)
                {
                    session.Dispose();
                    session = null;
                }
            }
        }

        // samples: exactly ChunkSamples mono 16 kHz samples in -1..1
        public float Process(float[] samples)
        {
            float[] input = new float[ContextSamples + ChunkSamples];
            Array.Copy(context, 0, input, 0, ContextSamples);
            Array.Copy(samples, 0, input, ContextSamples, ChunkSamples);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", new DenseTensor<float>(input, new[] { 1, ContextSamples + ChunkSamples })),
                NamedOnnxValue.CreateFromTensor("state", new DenseTensor<float>(state, new[] { 2, 1, 128 })),
                NamedOnnxValue.CreateFromTensor("sr", srTensor)
            };

            float probability = 0f;
            using (var results = session.Run(inputs))
            {
                foreach (var result in results)
                {
                    if (result.Name == "output") probability = result.AsEnumerable<float>().First();
                    else if (result.Name == "stateN") state = result.AsEnumerable<float>().ToArray();
                }
            }

            // Carry the tail of this chunk as context for the next one
            Array.Copy(input, input.Length - ContextSamples, context, 0, ContextSamples);
            return probability;
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
