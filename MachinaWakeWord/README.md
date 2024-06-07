# Machina Wake Word in C#

## Porcupine Library

Made in Vancouver, Canada by [Picovoice](https://picovoice.ai)

Porcupine is a highly-accurate and lightweight wake word engine. It enables building always-listening voice-enabled
applications.

Porcupine is:

- using deep neural networks trained in real-world environments.
- compact and computationally-efficient making it perfect for IoT.
- scalable. It can detect multiple always-listening voice commands with no added CPU/memory footprint.
- self-service. Developers can train custom wake phrases using [Picovoice Console](https://console.picovoice.ai/).

## Requirements

- .NET 6.0

## Compatibility

- Windows (x86_64)

Build with the dotnet CLI:

```console
dotnet add package Porcupine
dotnet build -c Machina.Release
```

### Machina wake word program

This wake word program opens an audio stream from a microphone and detects utterances of a given wake word to trigger a virtual Alt-Z key combination. The following opens the default microphone and detects occurrences of "Machina".

## Machina execution 

C:\Machina\Wakeword\Machina.exe --access_key `AccessKey` --keyword_paths C:\Machina\Machina\Machina_en_windows_v3_0_0.ppn

## AccessKey

Porcupine requires a valid Picovoice `AccessKey` at initialization. `AccessKey` acts as your credentials when using Porcupine SDKs.
You can get your `AccessKey` for free. Make sure to keep your `AccessKey` secret.
Signup or Login to [Picovoice Console](https://console.picovoice.ai/) to get your `AccessKey`.

## Usage

NOTE: File path arguments must be absolute paths. The working directory for the following dotnet commands is:

```console
Machina/MachinaWakeWord
```

```console
dotnet run -c Machina.Release -- \
--access_key ${ACCESS_KEY} \
--keywords picovoice
```

`keywords` is a shorthand for using default keyword files shipped with the package. The list of default keyword files
can be seen in the usage string:

```console
dotnet run -c Machina.Release -- --help
```

To detect multiple phrases concurrently provide them as separate arguments. If the wake word is more than a single word, surround the argument in quotation marks:

```console
dotnet run -c Machina.Release -- \
--access_key ${ACCESS_KEY} \
--keywords picovoice "hey siri"
```

To detect custom keywords (e.g. models created using [Picovoice Console](https://console.picovoice.ai/)) use `keyword_paths` argument:

```console
dotnet run -c Machina.Release -- \
--access_key ${ACCESS_KEY} \
--keyword_paths ${KEYWORD_PATH_ONE} ${KEYWORD_PATH_TWO}
```

It is possible that the default audio input device is not the one you wish to use. There are a couple
of debugging facilities baked into the demo application to solve this. First, type the following into the console:

```console
dotnet run -c Machina.Release -- --show_audio_devices
```

It provides information about various audio input devices on the box. Here is an example output:

```
index: 0, device name: USB Audio Device
index: 1, device name: MacBook Air Microphone
```

You can use the device index to specify which microphone to use for the application. For instance, if you want to use the USB Audio Device in the above example, you can invoke the demo application as below:

```console
dotnet run -c Machina.Release -- \
--access_key ${ACCESS_KEY} \
--keywords picovoice
--audio_device_index 0
```

If the problem persists we suggest storing the recorded audio into a file for inspection. This can be achieved with:

```console
dotnet run -c Machina.Release -- \
--access_key ${ACCESS_KEY} \
--keywords picovoice \
--audio_device_index 0 \
--output_path ./test.wav
```

If after listening to stored file there is no apparent problem detected please open an issue.
