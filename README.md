Machina
==

Use case is a personal AI voice assistant with [VoiceMacro](https://www.voicemacro.net). I developed several C# VM plugins as well as macros to give it life. They are combined with a Picovoice Porcupine AI wake word program running *locally* in the background to activate the command macro that uses the GetCommand AI STT plugin. VM then conditionally responds to voice commands copied to the command_p speech transcription variable and can reply with a SpeakText TTS plugin natural AI voice to have an easily customizable, private "Alexa". Make it "smarter" by adding the AskChatGPT and GetWeather plugins. It requires creating accounts with Deepgram, OpenAI, and Open Cage for API keys set in the "Initialize Machina" macro.

- Responds in a natural voice to a question with GPT response or have a conversation
- Seeded with an origin story, and context "memory" with current date/time
- Can give you local weather forecast or for any city from the National Weather Service
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
Get command_p text using Deepgram STT API  
Argument 1: Deepgram API key  
Argument 2: Speech duration in seconds  

**SpeakText**  
Speak text using Deepgram TTS API  
Argument 1: Deepgram API key  
Argument 2: Aura voice model  
Argument 3: Spoken text  
speaking_p: TRUE when speaking  

**AskChatGPT**  
Get response_p from prompt  
Argument 1: OpenAI API key  
Argument 2: ChatGPT model  
Argument 3: Prompt text  

**GetWeather**  
Get forecast_p for City, State  
Argument 1: Open Cage API key  
Argument 2: City  
Argument 3: State  

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

Installation Steps
--
1. Copy the entire Machina folder from Machina.zip to the root of C: so it appears as C:\Machina.
2. Install VoiceMacro_1.4_Setup.msi from https://www.voicemacro.net/download and accept all defaults.
3. Install winamp210.exe and accept all defaults.
4. Copy all the files in \Machina\Plugins to overwrite files in your VM Plugins folder at \VoiceMacro\Plugins.
5. Start VoiceMacro and click Edit and import the Machina.xml file.
6. Make sure Machina is selected as the Profile and click Edit.
7. Select and double-click on the Initialize Machina macro and provide your API keys for the web services.
8. Restart VoiceMacro

2024 Bruce Alexander
