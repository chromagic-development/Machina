// VectorDBServer: Creates and initializes an in-memory vector database using named pipes for initialization and semantic search
// v1.0.4.4 - Added direct LM Studio embedding support
// Argument 1: pageCount returned (default is 5)
// Argument 2: OpenAI API key or local endpoint (optional) - if provided, uses OpenAI embeddings; otherwise, defaults to basic in-memory vector database.
// Copyright © 2025 Bruce Alexander
// This software is licensed under the MIT License. See LICENSE file for details.

using System;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Build5Nines.SharpVector;
using Build5Nines.SharpVector.OpenAI;
using OpenAI;  // Ensure this is the OpenAI SDK package
using System.ClientModel; // For ApiKeyCredential
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace VectorDBServer
{
    class VectorDBServer
    {
        static BasicMemoryVectorDatabase? basicVdb;
        static BasicOpenAIMemoryVectorDatabase? openAIVdb;
        static LMStudioVectorDatabase? lmStudioVdb;
        static bool isInitialized = false;
        static bool useOpenAI = false;
        static bool useLMStudio = false;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Vector database server is running...");

            int pageCount = 5; // Default value
            if (args.Length > 0 && int.TryParse(args[0], out int parsedPageCount))
            {
                pageCount = parsedPageCount;
            }

            string? openAIKey = null;
            if (args.Length > 1)
            {
                openAIKey = args[1];
                useOpenAI = true;
            }

            PipeTransmissionMode mode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? PipeTransmissionMode.Message
                : PipeTransmissionMode.Byte;

            using var server = new NamedPipeServerStream("VectorPipe", PipeDirection.InOut, 1, mode);
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            if (useOpenAI)
            {
                if (string.IsNullOrWhiteSpace(openAIKey))
                {
                    await writer.WriteLineAsync("Missing OpenAI API key or endpoint as second argument.");
                    return;
                }

                try
                {
                    if (openAIKey.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        // Local LM Studio / proxy endpoint
                        string embeddingsUrl = openAIKey.Replace("/chat/completions", "/embeddings");
                        lmStudioVdb = new LMStudioVectorDatabase(embeddingsUrl);
                        useLMStudio = true;
                        useOpenAI = false;
                        Console.WriteLine($"Using LM Studio embeddings at: {embeddingsUrl}");
                    }
                    else
                    {
                        // Real OpenAI API
                        var credential = new ApiKeyCredential(openAIKey);
                        var openAIClient = new OpenAIClient(credential);
                        var embeddingClient = openAIClient.GetEmbeddingClient("text-embedding-3-small");
                        openAIVdb = new BasicOpenAIMemoryVectorDatabase(embeddingClient);
                        Console.WriteLine("Using OpenAI for embeddings.");
                    }
                }
                catch (Exception ex)
                {
                    await writer.WriteLineAsync($"Error initializing client: {ex.Message}");
                    Console.WriteLine($"Falling back to basic vector database due to error: {ex.Message}");
                    basicVdb = new BasicMemoryVectorDatabase();
                    useOpenAI = false;
                    useLMStudio = false;
                }
            }
            else
            {
                basicVdb = new BasicMemoryVectorDatabase();
                Console.WriteLine("Using basic in-memory vector database.");
            }

            // Initialization text
            string? initText = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(initText))
            {
                await InitializeDatabaseAsync(initText);
                isInitialized = true;
                await writer.WriteLineAsync("Vector database initialized.");
            }
            else
            {
                await writer.WriteLineAsync("Initialization text was empty.");
            }

            // Enter main loop for search queries and updates
            while (true)
            {
                string? request = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(request)) continue;

                if (request.Equals("Update", StringComparison.OrdinalIgnoreCase))
                {
                    string? updateText = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(updateText))
                    {
                        await InitializeDatabaseAsync(updateText);
                        await writer.WriteLineAsync("Vector database updated.");
                    }
                    else
                    {
                        await writer.WriteLineAsync("Update command received, but no text was provided.");
                    }
                }
                else
                {
                    string response = await SearchDatabaseAsync(request, pageCount);
                    await writer.WriteLineAsync(response);
                }
            }
        }

        static async Task InitializeDatabaseAsync(string input)
        {
            string[] parts = input.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string text = part.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    try
                    {
                        if (useOpenAI && openAIVdb != null)
                        {
                            await Task.Run(() => openAIVdb.AddText(text));
                        }
                        else if (useLMStudio && lmStudioVdb != null)
                        {
                            await lmStudioVdb.AddTextAsync(text);
                        }
                        else if (basicVdb != null)
                        {
                            basicVdb.AddText(text);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to add text '{text.Substring(0, Math.Min(50, text.Length))}...': {ex.Message}");
                        // Continue processing other parts
                    }
                }
            }
        }

        static async Task<string> SearchDatabaseAsync(string prompt, int pageCount)
        {
            if (!isInitialized) return "Database not initialized.";

            try
            {
                if (useOpenAI && openAIVdb != null)
                {
                    var result = openAIVdb.Search(prompt, pageCount: pageCount);
                    if (result.IsEmpty) return "no results";
                    return string.Join(" ", result.Texts.Select(t => t.Text.TrimEnd('.') + "."));
                }
                else if (useLMStudio && lmStudioVdb != null)
                {
                    var results = await lmStudioVdb.SearchAsync(prompt, pageCount);
                    if (!results.Any()) return "no results";
                    return string.Join(" ", results.Select(r => r.Text.TrimEnd('.') + "."));
                }
                else if (basicVdb != null)
                {
                    var result = basicVdb.Search(prompt, pageCount: pageCount);
                    if (result.IsEmpty) return "no results.";
                    return string.Join("", result.Texts.Select(t => t.Text.TrimEnd('.') + ". "));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search error: {ex.Message}");
                return "Error during search.";
            }
            return "Error: Database not properly set up.";
        }
    }

    // Custom vector database that works directly with LM Studio
    public class LMStudioVectorDatabase
    {
        private readonly HttpClient _httpClient;
        private readonly string _embeddingEndpoint;
        private readonly List<VectorEntry> _vectors;

        public LMStudioVectorDatabase(string embeddingEndpoint)
        {
            _httpClient = new HttpClient();
            _embeddingEndpoint = embeddingEndpoint.TrimEnd('/');
            _vectors = new List<VectorEntry>();

            // Set a reasonable timeout for embedding requests
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task AddTextAsync(string text)
        {
            try
            {
                var embedding = await GetEmbeddingAsync(text);
                _vectors.Add(new VectorEntry { Text = text, Vector = embedding });
                Console.WriteLine($"Added vector for text: {text.Substring(0, Math.Min(50, text.Length))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add text '{text.Substring(0, Math.Min(50, text.Length))}...': {ex.Message}");
                throw;
            }
        }

        public async Task<IEnumerable<VectorEntry>> SearchAsync(string query, int topK = 5)
        {
            if (!_vectors.Any()) return Enumerable.Empty<VectorEntry>();

            try
            {
                var queryEmbedding = await GetEmbeddingAsync(query);

                var similarities = _vectors.Select(v => new
                {
                    Entry = v,
                    Similarity = CosineSimilarity(queryEmbedding, v.Vector)
                })
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Select(x => x.Entry);

                return similarities;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Search failed: {ex.Message}");
                return Enumerable.Empty<VectorEntry>();
            }
        }

        private async Task<float[]> GetEmbeddingAsync(string text)
        {
            var request = new
            {
                input = text,
                model = "local-embedding-model" // LM Studio typically ignores this but it's required
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(_embeddingEndpoint, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseJson);
                var embeddings = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");

                var result = new float[embeddings.GetArrayLength()];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = embeddings[i].GetSingle();
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"LM Studio embedding request failed: {ex.Message}");
                throw;
            }
        }

        private static float CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0f;

            float dotProduct = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            if (normA == 0f || normB == 0f) return 0f;

            return dotProduct / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class VectorEntry
    {
        public string Text { get; set; } = "";
        public float[] Vector { get; set; } = Array.Empty<float>();
    }
}