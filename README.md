# VoiceReady

VoiceReady is a local voice-command assistant for Ready or Not. It listens to your microphone, recognizes supported spoken commands with a local Vosk speech model, checks the current in-game command-menu state, and sends the matching keyboard or mouse input.

VoiceReady does not modify game files, write process memory, inject code, or use a remote transcription service.

## How It Was Created

VoiceReady was created by reverse engineering Ready or Not with Cheat Engine. The project uses pointer paths extracted from that research to read specific in-game state values, including command-menu state, selected team, and context-sensitive door information.

Those values are used to decide which normal Ready or Not command-menu inputs should be sent after a voice command is recognized. VoiceReady executes commands through emulated keyboard and mouse input. It does not patch, hook, inject, or write to Ready or Not memory.

## How It Works

At runtime, VoiceReady:

- Captures microphone audio locally.
- Uses a local Vosk speech-recognition model to turn supported phrases into command intents.
- Reads Ready or Not process memory with read-only access to understand the command context the player is currently in.
- Checks values such as the active command menu, selected team, and whether the game currently exposes a known trapped-door command state.
- Sends the matching command sequence using normal keyboard and mouse input.

Default Ready or Not keybinds are currently required because VoiceReady sends the same keys the player would normally press. Custom keybind support may be added in a future update.

VoiceReady only receives information after the player has obtained it in-game. For example, a door trap is only known to VoiceReady after Ready or Not exposes that state because the player can currently see the trap or has already discovered it by peeking or using the mirror wand. VoiceReady cannot know that a door is booby trapped before the player observes it, and it does not give the player hidden information they would not already have.

## What You Need

- Windows
- Ready or Not
- A working microphone
- .NET 10 SDK installed and available on PATH

You do not need Python, Whisper, PyTorch, or an OpenAI API key. Earlier prototypes used Whisper, but the current app uses Vosk through .NET.

## Why There Is No Prebuilt EXE

VoiceReady is distributed as source code instead of a prebuilt `.exe` because the project is intended to stay open and inspectable. A compiled executable is much harder for users to read and verify, while this repository lets you see the code, configuration, setup scripts, and exact commands being run.

## First-Time Setup

Download or clone the full repository. The required Vosk runtime files and English speech model are included locally in the repo:

```text
tools\vendor\vosk\
tools\vosk\models\vosk-model-small-en-us-0.15\
```

There is no dependency installer script anymore. The only external requirement is the .NET 10 SDK. Check that .NET is available with:

```powershell
dotnet --version
```

## Run The App

Start Ready or Not first, then run:

```bat
run-ui.bat
```

Use the UI to select your microphone, calibrate or adjust the speech threshold, start VoiceReady, and watch status/debug output.

For the console voice runner, use:

```bat
run-voice.bat
```

The UI runner is recommended for normal use.

If Windows blocks the `.bat` files, run the app manually from PowerShell:

```powershell
dotnet run --project src\VoiceReady.App
```

For console voice mode:

```powershell
dotnet run --project src\VoiceReady.Cli -- --voice
```

## Basic Voice Commands

Commands can target a team by saying `red team`, `blue team`, or `gold team` before the command:

```text
red team breach using c2 and clear with flashbang
blue team search and secure
gold team fall in
red team get behind me
on me
```

`fall in`, `on me`, and `get behind me` default to Single File formation. You can also say `double file`, `diamond formation`, or `wedge formation`.

For best results, look at the door, ground, teammate, suspect, or civilian you want to command before speaking.

## Current Limitations

- Doorway and other-doorway command-menu voice support is still incomplete.
- Pick-the-lock voice support is not complete yet because the locked-door state address has not been identified. This is actively being worked on.
- Queued commands are not supported yet.
- Default Ready or Not keybinds are required for command execution.

## Common Issues

### .NET SDK was not found

If `dotnet` is not recognized, install the .NET 10 SDK, then open a new terminal and try again.

Check that .NET is visible with:

```bat
dotnet --version
```

### Vosk model was not found

If VoiceReady reports that the Vosk model was not found, the repository is incomplete or the model folder was moved. The expected model path is:

```text
tools\vosk\models\vosk-model-small-en-us-0.15
```

The folder should contain files such as:

```text
conf\model.conf
am\final.mdl
graph\HCLr.fst
graph\Gr.fst
```

Download or clone the repository again if those files are missing.

### Vosk runtime DLLs were not found

VoiceReady also needs the local Vosk runtime files in `tools\vendor\vosk`. If build or startup errors mention `Vosk.dll`, `libvosk.dll`, `libstdc++-6.dll`, `libgcc_s_seh-1.dll`, or `libwinpthread-1.dll`, download or clone the full repository again.

### VoiceReady does not hear you

Open `run-ui.bat`, select the correct microphone, then use calibration or lower the speech-start threshold. If your mic is very quiet, Windows input gain may also need adjustment.

The audio settings are stored in:

```text
config\voice_ready.json
```

### Commands are recognized but nothing happens in game

Make sure Ready or Not is running and focused. VoiceReady sends normal keyboard and mouse input, so another focused window can receive the input instead of the game.

Also make sure you are looking at a valid in-game command target. Some commands only work when the matching Ready or Not command menu is available.

### Commands work for some menus but not others

VoiceReady depends on configured Ready or Not command-menu state values. If the game updates and changes those values, some menu detection can stop working until the configuration is updated.

The relevant configuration files are:

```text
config\memory_map.json
config\command_menus.json
```

### Speech recognition is inaccurate

Try speaking shorter commands, pausing after each command, and keeping background noise low. You can add unusual phrases to:

```text
config\voice_ready.json
```

Look for `vosk.additionalGrammarPhrases`.

### Windows blocks the batch files

Windows may block downloaded `.bat` files depending on your security settings. If that happens, use the manual PowerShell commands in the setup and run sections above.

## Configuration

Main user-facing settings live in:

```text
config\voice_ready.json
```

Important values include:

- `audio.deviceNumber`: selected microphone number.
- `audio.speechStartDb`: how loud speech must be before capture starts.
- `audio.speechEndDb`: how quiet input must be before a phrase ends.
- `vosk.modelPath`: path to the local Vosk model.
- `vosk.additionalGrammarPhrases`: extra phrases the recognizer should expect.

## Third-Party Notices

Third-party dependency and license information is listed in:

```text
THIRD-PARTY-LICENSES.md
```
