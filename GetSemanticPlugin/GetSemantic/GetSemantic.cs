﻿// GetSemantic VM plugin: Get results_p from semantic search of vector database
// v1.0.3.3
// Uses OpenAI to generate embeddings when key is provided
// Argument 1: Period delimited text for embeddings initialization or updates and subsequent calls performs search and returns results
// Argument 2: Initialize or Update (leave blank for search)
// Copyright © 2025 Bruce Alexander
// vmAPI Library Copyright © 2018-2019 FSC-SOFT
// This software is licensed under the MIT License. See LICENSE file for details.

using vmAPI;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Diagnostics;
using NAudio.Utils;

namespace GetSemanticPlugin
{
    public static class Interface_Manager
    {
        public static VoiceMacro vmInstance = new VoiceMacro();
    }

    public class VoiceMacro : vmInterface
    {
        public string DisplayName => "GetSemantic";
        public string Description => "Get results_p from semantic search of vector database\r\nArgument 1: Initialization text, update text, or search text\r\nArgument 2: Initialize or Update";
        public string ID => "bcf4600a-6c63-4182-b77d-59c39b473936";

        private static NamedPipeClientStream pipeClient;
        private static StreamWriter writer;
        private static StreamReader reader;
        private static bool initialized = false;

        public void Init()
        {
            // No-op
        }

        public void ReceiveParams(string Param1, string Param2, string Param3, bool Synchron)
        {
            Param1 = Param1.Replace("\"", "");
            Param2 = Param2.Replace("\"", "");
            Param3 = Param3.Replace("\"", "");

            // Check if Argument 2 has the value "initialize" (case-insensitive)
            if (!string.IsNullOrEmpty(Param2) && Param2.Trim().ToLower() == "initialize")
            {
                // Force reinitialization
                initialized = false;

                // Dispose and reset pipe connection to ensure clean restart
                if (writer != null) { writer.Dispose(); writer = null; }
                if (reader != null) { reader.Dispose(); reader = null; }
                if (pipeClient != null) { pipeClient.Dispose(); pipeClient = null; }
            }

            Task.Run(async () =>
            {
                // Pass Param2 as a command to GetSemantic
                string results = await GetSemantic(Param1, Param2);

                vmCommand.SetVariable("results_p", results);
                vmCommand.AddLogEntry(results, Color.Blue, ID, "S", "Vector database return");
            });
        }

        public void ProfileSwitched(string ProfileGUID, string ProfileName)
        {
            // No-op
        }

        public void Dispose()
        {
            if (writer != null) writer.Dispose();
            if (reader != null) reader.Dispose();
            if (pipeClient != null) pipeClient.Dispose();

            Process[] processes = Process.GetProcessesByName("VectorDBServer");
            foreach (var proc in processes)
            {
                if (!proc.HasExited)
                {
                    proc.CloseMainWindow();
                    if (!proc.WaitForExit(5000)) proc.Kill();
                }
            }
        }
        // Get results_p from semantic search of vector database
        // Argument 1: Initialization sentences or search text
        // Argument 2: Command to execute (e.g., "Update")
        private static async Task<string> GetSemantic(string text, string command)
        {
            if (pipeClient == null)
            {
                pipeClient = new NamedPipeClientStream(".", "VectorPipe", PipeDirection.InOut, PipeOptions.Asynchronous);
                await pipeClient.ConnectAsync();
                writer = new StreamWriter(pipeClient) { AutoFlush = true };
                reader = new StreamReader(pipeClient);
            }

            // Handle "Update" command
            if (!string.IsNullOrEmpty(command) && command.Trim().ToLower() == "update")
            {
                await writer.WriteLineAsync("Update");
                await writer.WriteLineAsync(text);
                string updateResponse = await reader.ReadLineAsync();
                if (updateResponse != null && updateResponse.Contains("Vector database updated"))
                {
                    updateResponse = "Vector database updated";
                }
                initialized = true;
                return updateResponse ?? "Error: No response from server on update.";
            }
            // Initialize vector database by generating embeddings from Argument 1
            else if (!initialized)
            {
                await writer.WriteLineAsync(text);
                string initResponse = await reader.ReadLineAsync();
                if (initResponse != null && initResponse.Contains("Vector database initialized"))
                {
                    initResponse = "Vector database initialized";
                }
                initialized = true;
                return initResponse ?? "Error: No initialize database response from server.";
            }
            // Then perform a semantic search using Argument 1 and return results for subsequent calls
            else
            {
                await writer.WriteLineAsync(text);
                string results = await reader.ReadLineAsync();
                return results ?? "Error: No results from server.";
            }
        }
    }
}
