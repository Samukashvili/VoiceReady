# VoiceReady

VoiceReady is a read-only companion foundation for detecting Ready or Not command-menu state through module-relative pointer paths.

This scaffold does not modify game files, write process memory, inject code, or hook game functions. It only opens the game process with read/query permissions and reads configured pointer paths.

## Current Pieces

- `src/VoiceReady.Core/Memory`: read-only Windows process reader and Cheat Engine pointer-path resolver.
- `src/VoiceReady.Core/Detection`: redundant menu-state reader that votes across many pointer paths.
- `src/VoiceReady.Core/Configuration`: JSON memory-map loader.
- `src/VoiceReady.Cli`: polling/debug CLI for mapping observed command context values.
- `config/memory_map.json`: current 4-byte menu-state pointer paths.
- `config/command_menus.json`: command context values and key mappings discovered so far.

## Run

Start Ready or Not first, then run:

```powershell
dotnet run --project src\VoiceReady.Cli
```

The CLI prints a new line whenever the voted menu-state value changes.

Voice mode:

```powershell
dotnet run --project src\VoiceReady.Cli -- --voice
```

or:

```bat
run-voice.bat
```

For first-time setup from source, run:

```bat
install-dependencies.bat
```

Voice mode captures microphone audio, segments speech locally using an RMS/decibel threshold, recognizes completed speech segments in-process with a fully local Vosk model, parses recognized command phrases, and executes state-gated key sequences.

Commands may target a team by including `red team`, `blue team`, or `gold team`, for example:

```text
red team breach using c2 and clear with flashbang
blue team search and secure
gold team fall in
red team get behind me
on me
```

`fall in`, `on me`, and `get behind me` default to the Single File formation. You can request
another formation explicitly with `double file`, `diamond formation`, or `wedge formation`.

Saying only a team name selects that team and closes the command menu. Team selection is performed with mouse-wheel input only while a command menu is open, and the result is verified using the voted `teamSelection` pointers before command keys are sent.

Trap-aware door commands use the menu-state value `TrappedDoorCommandMenu`, which appears when the door command menu has inserted `DisarmTrap` at key `6`.

Vosk is configured in `config/voice_ready.json`. The default expected model layout is:

```text
tools/vosk/
  download-model.ps1
  models/vosk-model-small-en-us-0.15/
```

The installer restores the Vosk C# package and downloads the lightweight English model into the repo. Recognition is local at runtime and does not call a remote transcription API.

```powershell
dotnet restore VoiceReady.slnx
powershell -ExecutionPolicy Bypass -File tools\vosk\download-model.ps1
```

The runtime grammar is generated from command phrases, team prefixes, equipment names, alternate phrasings, and entries in `vosk.additionalGrammarPhrases`. Add unusual phrases there without changing code.

Known menu-state values currently include gameplay/no menu, escape menu, blank menu, interaction prompt, and the first door command/submenu states.

Known team-selection values are `0` for an NPC command context, `1` for Red, `2` for Blue, and `5` for Gold. Values `3` and `4` remain intentionally unmapped until observed.

## Mapping Values

Update `config/memory_map.json` as you identify stable values:

```json
{
  "name": "Door",
  "value": 123
}
```

The value reader intentionally uses `Int32` / 4-byte reads because that matched the most reliable Cheat Engine observations.

## Pointer Root Relocation

Each distinct module-relative pointer root can define one wildcard byte signature in `config/memory_map.json`.
At startup, VoiceReady scans the configured module, requires the signature to match exactly once, calculates
the referenced root from its RIP-relative displacement, and caches the result for the lifetime of the process.

The existing `baseOffset` remains a fallback when a signature is missing, malformed, or no longer unique.
Startup diagnostics report `source=signature` or `source=fallback` for every root. A fallback after a game
update means that root's signature should be regenerated and validated.

New pointer paths that use an existing `baseOffset` automatically reuse its root signature. A pointer path
with a new root should also add one entry to that pointer group's `rootSignatures` array.

## Pointer Offset Order

The current config uses `Listed`, which matched the provided Cheat Engine pointer rows during a live smoke test:

```json
"offsetOrder": "Listed"
```

If future pointer exports fail to resolve, the alternative supported value is `CheatEnginePointerScanner`, which resolves offsets from the last pointer-scanner column back to `Offset 0`.
