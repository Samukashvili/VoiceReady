# Third-Party Notices

This document summarizes the third-party software, assets, and services used or referenced by VoiceReady.

## Vosk

VoiceReady includes runtime files from the Vosk C# package:

- Package: `Vosk`
- Version: `0.3.38`
- Project: https://github.com/alphacep/vosk-api
- License: Apache License 2.0
- License text: https://www.apache.org/licenses/LICENSE-2.0

The repo-local Vosk files are stored under `tools/vendor/vosk/`. This repository contains code and bundled runtime files from the Vosk project, which is licensed under the Apache License 2.0. A copy of this license can be found at the Apache Software Foundation license URL above.

## Vosk English Model

VoiceReady includes the local Vosk English speech-recognition model:

- Model: `vosk-model-small-en-us-0.15`
- Download source: https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip
- Copyright notice from the model README: Copyright 2020 Alpha Cephei Inc
- Model information: https://alphacephei.com/vosk/models

The bundled model is stored under `tools/vosk/models/`.

## Microsoft .NET and Windows Forms

VoiceReady is built with Microsoft .NET and includes a Windows Forms application target:

- .NET: https://github.com/dotnet/runtime
- Windows Forms: https://github.com/dotnet/winforms
- License: MIT License

VoiceReady also calls standard Windows platform APIs such as `user32.dll`, `kernel32.dll`, and `winmm.dll`. These are operating-system APIs and are not redistributed by this repository.

## Ready or Not

VoiceReady interoperates with Ready or Not by reading process memory and sending keyboard/mouse input selected by the user. This repository does not include Ready or Not code, assets, or data files.

- Ready or Not: https://voidinteractive.net/

VoiceReady is not affiliated with, endorsed by, or sponsored by VOID Interactive.

## Runtime Services

VoiceReady performs speech recognition locally at runtime and does not call a remote transcription service. The current repository bundles its Vosk runtime files and default Vosk model locally, so there is no setup-time model download script.
