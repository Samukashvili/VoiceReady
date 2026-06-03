import argparse
from pathlib import Path

from huggingface_hub import snapshot_download


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-id", default="Systran/faster-whisper-base.en")
    parser.add_argument("--output", default="tools/faster-whisper/models/base.en")
    args = parser.parse_args()

    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)

    snapshot_download(
        repo_id=args.repo_id,
        local_dir=str(output),
        local_dir_use_symlinks=False,
        allow_patterns=[
            "config.json",
            "model.bin",
            "tokenizer.json",
            "vocabulary.*"
        ])

    print(f"Downloaded {args.repo_id} to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
