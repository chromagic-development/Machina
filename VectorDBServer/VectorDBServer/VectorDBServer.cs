// VectorDBServer: Creates and initializes an in-memory vector database using named pipes for initialization and semantic search
// v1.0.3.3
// Argument 1: pageCount returned (default is 5)
// Argument 2: OpenAI API key (optional) - if provided, uses OpenAI embeddings; otherwise, defaults to basic in-memory vector database.
// Copyright © 2025 Bruce Alexander
// This software is licensed under the MIT License. See LICENSE file for details.

using System.IO.Pipes;
using System.Runtime.InteropServices;
using Build5Nines.SharpVector;
using OpenAI;
using Build5Nines.SharpVector.OpenAI;

namespace VectorDBServer
{
    class VectorDBServer
    {
        static BasicMemoryVectorDatabase? basicVdb;
        static BasicOpenAIMemoryVectorDatabase? openAIVdb;
        static bool isInitialized = false;
        static bool useOpenAI = false;

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
                    await writer.WriteLineAsync("Missing OpenAI API key as second argument.");
                    return;
                }

                try
                {
                    var openAIClient = new OpenAIClient(openAIKey);
                    var embeddingClient = openAIClient.GetEmbeddingClient("text-embedding-3-small");
                    openAIVdb = new BasicOpenAIMemoryVectorDatabase(embeddingClient);
                    Console.WriteLine("Using OpenAI for embeddings.");
                }
                catch (Exception ex)
                {
                    await writer.WriteLineAsync($"Error initializing OpenAI client: {ex.Message}");
                    return;
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
                InitializeDatabase(initText);
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

                // Check if the request is an update command
                if (request.Equals("Update", StringComparison.OrdinalIgnoreCase))
                {
                    // Read the next line which contains the text to add
                    string? updateText = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(updateText))
                    {
                        InitializeDatabase(updateText); // Use the same method to add more text
                        await writer.WriteLineAsync("Vector database updated.");
                    }
                    else
                    {
                        await writer.WriteLineAsync("Update command received, but no text was provided.");
                    }
                }
                else
                {
                    // Otherwise, perform a search
                    string response = SearchDatabase(request, pageCount);
                    await writer.WriteLineAsync(response);
                }
            }
        }

        static void InitializeDatabase(string input)
        {
            // This method now handles both initial setup and subsequent updates
            string[] parts = input.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string text = part.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    if (useOpenAI && openAIVdb != null)
                    {
                        openAIVdb.AddText(text);
                    }
                    else if (!useOpenAI && basicVdb != null)
                    {
                        basicVdb.AddText(text);
                    }
                }
            }
        }

        static string SearchDatabase(string prompt, int pageCount)
        {
            if (!isInitialized) return "Database not initialized.";

            if (useOpenAI && openAIVdb != null)
            {
                var result = openAIVdb.Search(prompt, pageCount: pageCount);
                if (result.IsEmpty) return "no results";
                return string.Join(" ", result.Texts.Select(t => t.Text.TrimEnd('.') + "."));
            }
            else if (!useOpenAI && basicVdb != null)
            {
                var result = basicVdb.Search(prompt, pageCount: pageCount);
                if (result.IsEmpty) return "no results.";
                return string.Join("", result.Texts.Select(t => t.Text.TrimEnd('.') + ". "));
            }
            return "Error: Database not properly set up.";
        }
    }
}