Machina
==

Use case is a personal AI voice assistant with [VoiceMacro](https://www.voicemacro.net). I developed several C# VM plugins as well as macros to give it life. They are combined with a Picovoice Porcupine AI wake word program running *locally* in the background to activate the command macro that uses the GetCommand AI STT plugin. VM then conditionally responds to voice commands copied to the command_p speech transcription variable and can reply with a SpeakText TTS plugin natural AI voice to have an easily customizable, private "Alexa". Make it "smarter" by adding the AskChatGPT, GetWeather, GetStockQuote, and GetHeadlines plugins. It requires creating accounts with Picovoice, Deepgram, OpenAI, Open Cage, Alpha Vantage, and News API for API keys set in the "Initialize Machina" macro.

- Responds in a natural voice after a question answered by GPT or have a conversation
- Seeded with an origin story, and context "memory" with current date/time for reference
- Can give you local weather forecast or for any city from the National Weather Service
- Can give you stock quote information for a publicly listed company
- Can give you news headlines from the previous day
- Can start your Roomba
- Remote control web app when you're out of audio range
- Also can play music, set timers, check email and summarize threads, create poems, etc, anything Alexa can do, but smarter, more private, and easily customizable with VoiceMacro scripting
- You can keep incrementally customizing it with fun stuff on the fly!

<p align="center">
  <img src="https://repository-images.githubusercontent.com/811629505/ba9e6961-bbdc-488c-8760-97e0d3ad67d7" />
</p>

System Requirements
--
Windows PC with microphone and speaker

Plugin Descriptions
--

**GetCommand**  
Get command_p text using voice AI STT  
Argument 1: Deepgram API key  
Argument 2: Maximum speech duration in seconds 
Argument 3: Silence threshold in seconds (optional, default is 2s)

**SpeakText**  
Speak text using voice AI TTS  
Argument 1: Deepgram API key  
Argument 2: Aura voice model  
Argument 3: Spoken text  
speaking_p: TRUE when speaking  

**AskChatGPT**  
Get response_p from prompt using LLM AI  
Argument 1: OpenAI API key  
Argument 2: ChatGPT model  
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
SetVariable	        command_p = ""
SendToPlugin	        GetCommand, {deepgram_api_key_p}, 4
Loop	        1_Start (450x)
Condition	            If command_p <> ""
ExitLoop	            ---------- exit loop here ----------
Condition	            EndIf
Pause	            0.100 sec
Loop	        1_End
```

```VoiceMacro
SendToPlugin	        SpeakText, {deepgram_api_key_p}, {aivoice_p}, Forecast for {city} {state}
Pause	        3.000 sec
Loop	        1_Start (600x)
Condition	            If speaking_p = FALSE
ExitLoop	            ---------- exit loop here ----------
Condition	            EndIf
Pause	            0.100 sec
Loop	        1_End
```

```VoiceMacro
SetVariable	    response_p = ""
SendToPlugin	    AskChatGTP, {openai_api_key_p}, {chatgpt_model_p}, {prompt}
Pause	    3.000 sec
Loop	    1_Start (300x)
Condition	        If response_p <> ""
ExitLoop	        ---------- exit loop here ----------
Condition	        EndIf
Pause	        0.100 sec
Loop	    1_End
Condition	EndIf
```

```VoiceMacro
SetVariable	    forecast_p = ""
SendToPlugin	    GetWeather, {opencage_api_key_p}, {city}, {state}
Pause	    3.000 sec
Loop	    1_Start (300x)
Condition	        If forecast_p <> ""
ExitLoop	        ---------- exit loop here ----------
Condition	        EndIf
Pause	        0.100 sec
Loop	    1_End
```

```VoiceMacro
SetVariable	    price_p = ""
SendToPlugin	    GetStockQuote, {alphavantage_api_key_p}, {response_p}
Pause	    3.000 sec
Loop	    1_Start (300x)
Condition	        If price_p <> ""
ExitLoop	        ---------- exit loop here ----------
Condition	        EndIf
Pause	        0.100 sec
Loop	    1_End
```

```VoiceMacro
SetVariable	headlines_p = ""
SendToPlugin	GetHeadlines, {news_api_key_p}
Pause	3.000 sec
Loop	1_Start (300x)
Condition	    If headlines_p <> ""
ExitLoop	    ---------- exit loop here ----------
Condition	    EndIf
Pause	    0.100 sec
Loop	1_End
```

Installation Steps
--
1. Copy the entire Machina folder from Machina.zip to the root of C: so it appears as C:\Machina.
2. Install VoiceMacro_1.4_Setup.msi from https://www.voicemacro.net/download and accept all defaults.
3. Install winamp210.exe and accept all defaults to stream music.
4. Copy all the files in \Machina\Plugins to overwrite files in your VM Plugins folder at \VoiceMacro\Plugins.
5. Start VoiceMacro and click Edit and import the Machina.xml file.
6. Make sure Machina is selected as the Profile and click Edit.
7. Select and double-click on the Initialize Machina macro and provide your API keys for web services.
8. Restart VoiceMacro

2024 Bruce Alexander
