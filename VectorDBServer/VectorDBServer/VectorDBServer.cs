// VectorDBServer: Creates and initializes an in-memory vector database using named pipes for initialization and semantic search
// v1.0.1.0
// Argument 1: pageCount returned (default is 5)
// Copyright © 2025 Bruce Alexander
// This software is licensed under the MIT License. See LICENSE file for details.

using System.IO.Pipes;
using System.Runtime.InteropServices;
using Build5Nines.SharpVector;

namespace VectorDBServer
{
    class VectorDBServer
    {
        static BasicMemoryVectorDatabase vdb = new();
        static bool isInitialized = false;

        static async Task Main(string[] args)
        {
            int pageCount = 5; // Default value

            if (args.Length > 0 && int.TryParse(args[0], out int parsedPageCount))
            {
                pageCount = parsedPageCount;
            }

            Console.WriteLine($"Vector database server is running with pageCount = {pageCount}...");

            // Use PipeTransmissionMode.Byte if not on Windows
            PipeTransmissionMode mode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? PipeTransmissionMode.Message
                : PipeTransmissionMode.Byte;

            using var server = new NamedPipeServerStream("VectorPipe", PipeDirection.InOut, 1, mode);
            await server.WaitForConnectionAsync();
            using var reader = new StreamReader(server);
            using var writer = new StreamWriter(server) { AutoFlush = true };

            while (true)
            {
                string? request = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(request)) continue;

                if (!isInitialized)
                {
                    InitializeDatabase(request);
                    isInitialized = true;
                    await writer.WriteLineAsync("Vector database initialized.");
                }
                else
                {
                    string response = SearchDatabase(request, pageCount);
                    await writer.WriteLineAsync(response);
                }
            }
        }

        static void InitializeDatabase(string input)
        {
            string[] parts = input.Split('.', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i += 1)
            {
                string text = parts[i].Trim();
                vdb.AddText(text);
            }
        }

        static string SearchDatabase(string prompt, int pageCount)
        {
            var result = vdb.Search(prompt, pageCount: pageCount);
            if (result.IsEmpty) return "no results.";
            return string.Join("", result.Texts.Select(t => t.Text.TrimEnd('.') + ". "));
        }
    }
}
