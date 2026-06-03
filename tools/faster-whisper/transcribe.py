import argparse
import json
import sys

from faster_whisper import WhisperModel


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True)
    parser.add_argument("--audio")
    parser.add_argument("--language", default="en")
    parser.add_argument("--device", default="cpu")
    parser.add_argument("--compute-type", default="int8")
    parser.add_argument("--server", action="store_true")
    args = parser.parse_args()

    model = WhisperModel(args.model, device=args.device, compute_type=args.compute_type)
    if args.server:
        print(json.dumps({"ready": True}), flush=True)
        for line in sys.stdin:
            audio_path = line.strip()
            if not audio_path:
                continue
            if audio_path == "__quit__":
                break
            print(json.dumps(transcribe(model, audio_path, args.language)), flush=True)
    else:
        if not args.audio:
            raise ValueError("--audio is required unless --server is used")
        print(json.dumps(transcribe(model, args.audio, args.language)), flush=True)

    return 0


def transcribe(model: WhisperModel, audio_path: str, language: str) -> dict:
    initial_prompt = (
        "Ready or Not SWAT voice commands. Words include breach, kick, shotgun, C2, C4, charge, "
        "ram, clear, flashbang, stinger, CS gas, launcher, stack up, move, cover, fall in."
    )
    segments, info = model.transcribe(
        audio_path,
        language=language,
        vad_filter=False,
        beam_size=1,
        best_of=1,
        temperature=0,
        initial_prompt=initial_prompt,
        condition_on_previous_text=False)

    text = " ".join(segment.text.strip() for segment in segments).strip()
    return {
        "text": text,
        "language": info.language,
        "languageProbability": info.language_probability
    }


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as ex:
        print(json.dumps({"error": str(ex)}), file=sys.stderr)
        raise
