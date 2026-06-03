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

Known menu-state values currently include gameplay/no menu, escape menu, blank menu, interaction prompt, and the first door command/submenu states.

## Mapping Values

Update `config/memory_map.json` as you identify stable values:

```json
{
  "name": "Door",
  "value": 123
}
```

The value reader intentionally uses `Int32` / 4-byte reads because that matched the most reliable Cheat Engine observations.

## Pointer Offset Order

The current config uses `Listed`, which matched the provided Cheat Engine pointer rows during a live smoke test:

```json
"offsetOrder": "Listed"
```

If future pointer exports fail to resolve, the alternative supported value is `CheatEnginePointerScanner`, which resolves offsets from the last pointer-scanner column back to `Offset 0`.
