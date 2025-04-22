Machina
==

The use case is a personal AI voice assistant integrated with [VoiceMacro](https://www.voicemacro.net). I developed in C# an AI wake word engine running *locally* in the background and several VM plugins as well as macros contributing to a complete stack to give it life. When "Machina" (/ma.ki.na/) is spoken it activates the command macro that uses the GetCommand AI STT plugin. VM then conditionally responds to voice commands copied to the command_p speech transcription variable and can reply with a SpeakText TTS plugin natural AI voice to have an easily customizable, private "Alexa". Make it "smarter" by adding the AskChatGPT, AskVisionGPT, and GetWeather plugins. It requires using edge servers that can optionally run as a single PC or creating cloud service accounts with Deepgram, OpenAI, and Open Cage for API keys set in the "Initialize Machina" macro.

- VM with UI access and control serves as a flexible, symbolic layer integrating the AI plugins
- Macro scripting development environment easily facilitates design and implementation of AI agents and RAG
- Responds in a natural voice after a question answered by ChatGPT or have a conversation
- Uses multimodal vision with either a local or IP camera so you can ask about what it sees
- Seeded with an origin story and context short-term and long-term "memory" updated by ChatGPT conversations
- Can give you local weather forecast or for any city from the National Weather Service
- Can give you stock quote information for a publicly listed company
- Can give you news headlines
- Can start your Roomba
- Remote control web app when you're out of audio range
- Also can play music, set timers, summarize Google searches, check email and summarize threads, create poems, etc, anything Alexa can do, but smarter, more private, and easily customizable with VoiceMacro scripting
- Macros can be easily created to scrape websites with current information to summarize daily events, tide schedule, and so on, in addition to the plugins which use API calls
- You can keep incrementally customizing it with fun symbolic and AI macro functions on the fly to automate anything a human can do on a Windows PC
- Can now be run entirely local if you also install LM Studio and tiny-openai-whisper-api
- Piper TTS is also provided as a local option for a natural AI voice
- Deprecated Picovoice Porcupine is still an option for the default wake word engine and binaries are included in the zip file

<p align="center">
  <img src="https://repository-images.githubusercontent.com/811629505/aaa9476f-8ee9-49a2-91e2-549b6dbcd110" />
</p>

System Requirements
--
Windows PC with speaker and microphone

Plugin Descriptions
--

**GetCommand**  
Get command_p text using voice AI STT  
Argument 1: Deepgram API key or OpenAI Whisper endpoint (http) 
Argument 2: Maximum speech duration in seconds  
Argument 3: Silence threshold in seconds (optional, default is 2s)  

**SpeakText**  
Speak text using voice AI TTS  
Argument 1: Deepgram API key  
Argument 2: Aura voice model  
Argument 3: Spoken text  
speaking_p: True when speaking  
stopspeak_p: True when user stops speech  

**AskChatGPT**  
Get response_p from prompt using LLM AI  
Argument 1: OpenAI API key or local endpoint (http)  
Argument 2: ChatGPT model  
Argument 3: Prompt text  

**AskVisionGPT**  
Get response_p from prompt using LLM Vision AI  
Argument 1: OpenAI API key or local endpoint (http)  
Argument 2: RTSP URL or leave blank  
Argument 3: Prompt text  

**GetWeather**  
Get forecast_p for City, State using NWS  
Argument 1: Open Cage API key  
Argument 2: City  
Argument 3: State  

**GetStockQuote**  
Get stock price_p from symbol  
Argument 1: Alpha Vantage API key  
Argument 2: Stock symbol  

**GetHeadlines**  
Get top three national news headlines_p  
Argument 1: News API key  

Plugin Example Usage
--

```VoiceMacro
SetVariable	     command_p = ""
SendToPlugin	     GetCommand, {deepgram_api_key_p}, 10, 1
Loop	             1_Start (300x)
Pause	                0.100 sec
Condition	            If command_p <> ""
ExitLoop	            ---------- exit loop here ----------
Condition	            EndIf
Loop	             1_End
```

```VoiceMacro
SendToPlugin	    SpeakText, {deepgram_api_key_p}, {aivoice_p}, {command_p}
Loop	            1_Start (600x)
Pause	              0.100 sec
Condition	          If speaking_p = FALSE
ExitLoop	          ---------- exit loop here ----------
Condition	          EndIf
Loop	            1_End
```

```VoiceMacro
SetVariable	    response_p = ""
SendToPlugin	    AskChatGTP, {openai_api_key_p}, {chatgpt_model_p}, {prompt}
Loop	            1_Start (300x)
Pause	              0.100 sec
Condition	          If response_p <> ""
ExitLoop	          ---------- exit loop here ----------
Condition	          EndIf
Loop	            1_End
```

```VoiceMacro
SetVariable	    response_p = ""
SendToPlugin	    AskVisionGPT, {openai_api_key_p}, {rtspurl_p}, {prompt}
Loop	            1_Start (300x)
Pause	              0.100 sec
Condition	          If response_p <> ""
ExitLoop	          ---------- exit loop here ----------
Condition	          EndIf
Loop	            1_End
```

```VoiceMacro
SetVariable	    forecast_p = ""
SendToPlugin	    GetWeather, {opencage_api_key_p}, {city}, {state}
Loop	            1_Start (300x)
Pause	              0.100 sec
Condition	          If forecast_p <> ""
ExitLoop	          ---------- exit loop here ----------
Condition	          EndIf
Loop	            1_End
```

```VoiceMacro
SetVariable	    price_p = ""
SendToPlugin	    GetStockQuote, {alphavantage_api_key_p}, {response_p}
Loop	            1_Start (300x)
Pause	              0.100 sec
Condition	          If price_p <> ""
ExitLoop	          ---------- exit loop here ----------
Condition	          EndIf
Loop	            1_End
```

```VoiceMacro
SetVariable	    headlines_p = ""
SendToPlugin	    GetHeadlines, {news_api_key_p}
Loop	            1_Start (300x)
Pause	              0.100 sec
Condition	          If headlines_p <> ""
ExitLoop	          ---------- exit loop here ----------
Condition	          EndIf
Loop	            1_End
```

Installation Steps
--
1. Download and install <a href="https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-6.0.33-windows-x64-installer?cid=getdotnetcore">Microsoft .NET 6.0 Runtime</a> and <a href="https://download.visualstudio.microsoft.com/download/pr/571ad766-28d1-4028-9063-0fa32401e78f/5D3D8C6779750F92F3726C70E92F0F8BF92D3AE2ABD43BA28C6306466DE8A144/VC_redist.x64.exe">Microsoft Visual C++ 2015-2022 Redistributable</a> to a Windows PC.
2. Download Machina.zip and Machina.xml files from <a href="https://github.com/chromagic-development/Machina/releases">Releases</a> in this repository.
3. Copy the entire Machina folder from Machina.zip to the root of C: so it appears as C:\Machina (Other drives require editing Machina.xml).
4. Download and install <a href="https://www.voicemacro.net/download">VoiceMacro</a> and accept all defaults.
5. Install Firefox and Winamp with all defaults accepted to use included RAG with web and stream music (optional; all macros can be edited to suit your needs).
6. Copy all the files in C:\Machina\Plugins to overwrite files in the default VM Plugins folder (e.g. C:\Program Files (x86)\VoiceMacro\Plugins).
7. Start VoiceMacro and click Edit and Import the Machina.xml file you downloaded for the latest macros.
8. Make sure Machina is selected as the Profile and click Edit.
9. Select and double-click on the Initialize Machina macro and provide your API keys for web services.
10. Make sure the Initialize Machina macro is set to (Auto).
11. Restart VoiceMacro.
12. To optionally run locally without cloud service API keys required after installing LM Studio and <a href="https://github.com/morioka/tiny-openai-whisper-api">tiny-openai-whisper-api</a>, follow the instructions in the "For local AI servers" comment in the Initialize Machina macro. A document with steps for installing tiny-openai-whisper-api with WSL2 on your Windows PC is in the Installation folder.

2024 Bruce Alexander
