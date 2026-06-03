# Faster Whisper Worker

VoiceReady calls `transcribe.py` for completed speech snippets.

Expected local layout:

```text
tools/faster-whisper/
  transcribe.py
  requirements.txt
  models/base.en/
```

Install dependencies into your preferred local Python environment:

```powershell
python -m pip install -r tools\faster-whisper\requirements.txt
```

Place a CTranslate2 faster-whisper model under `tools/faster-whisper/models/base.en`, or update `config/voice_ready.json` to point at another local model path.
