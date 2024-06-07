Use case is a personal voice assistant using VoiceMacro. I developed several C# VM plugins as well as macros to give it life. They are combined with a Picovoice Porcupine wake word program running locally in the background to activate the command macro that uses the GetCommand AI STT plugin. VM then conditionally responds to voice commands copied to the command_p speech transcription variable and can reply with a SpeakText TTS plugin natural AI voice to have an easily customizable, private "Alexa". Make it "smarter" by adding the AskChatGPT and GetWeather plugins. It requires creating accounts with Deepgram, OpenAI, and Open Cage for API keys set in the "Initialize Machina" macro.

    . Responds to a question with GPT response or have a conversation
    . Seeded with an origin story, and context "memory" with current date/time
    . Can give you local weather forecast or for any city
    . Can start your Roomba
    . Remote control web app when you're out of audio range
    . Also can play music, set timers, check email and summarize threads, create poems, etc, etc, anything Alexa can do, but smarter, more private, and easily customizable with VoiceMacro scripting
    . You can keep incrementally customizing it with fun stuff on the fly!

System Requirements
-------------------------------------------------------------------
Windows PC with microphone and speaker

Installation steps
-------------------------------------------------------------------
1. Copy the entire Machina folder from Machina.zip to the root of C: so it appears as C:\Machina.
2. Install VoiceMacro_1.4_Setup.msi from https://www.voicemacro.net/download and accept all defaults.
3. Install winamp210.exe and accept all defaults.
4. Copy all the files in \Machina\Plugins to overwrite files in your VM Plugins folder at \VoiceMacro\Plugins.
5. Start VoiceMacro and click Edit and import the Machina.xml file.
6. Make sure Machina is selected as the Profile and click Edit.
7. Select and double-click on the Initialize Machina macro and provide your API keys for the web services.
8. Restart VoiceMacro

2024 Bruce Alexander
