from fastapi import FastAPI, Body
from fastapi.responses import Response
import os, sys, subprocess, tempfile
from pathlib import Path

DATA_DIR = os.getenv("PIPER_DATA", "/models/piper")
DEFAULT_VOICE = os.getenv("PIPER_DEFAULT_VOICE", "en_US-libritts-high")
PIPER_BIN = os.getenv("PIPER_BIN", "piper")

app = FastAPI()

def ensure_voice(voice_name: str) -> tuple[str, str]:
    """Ensure <voice>.onnx + .onnx.json exist under DATA_DIR; download via CLI if missing."""
    data = Path(DATA_DIR)
    data.mkdir(parents=True, exist_ok=True)
    model = data / f"{voice_name}.onnx"
    conf  = data / f"{voice_name}.onnx.json"

    if not (model.exists() and conf.exists()):
        # Use Piper’s official downloader CLI
        # (accepts canonical ids like en_US-libritts-high; also handles aliases)
        subprocess.run(
            [sys.executable, "-m", "piper.download_voices",
             voice_name, "--data-dir", str(data), "--download-dir", str(data)],
            check=True
        )
        if not (model.exists() and conf.exists()):
            raise RuntimeError(f"Downloaded files not found for '{voice_name}'")

    return str(model), str(conf)

@app.get("/health")
def health():
    return {"ok": True, "installed": sorted(p.stem for p in Path(DATA_DIR).glob("*.onnx")),
            "default": DEFAULT_VOICE}

@app.post("/tts")
async def tts(payload: dict = Body(...)):
    text  = (payload.get("text") or "").strip()
    voice = (payload.get("voice") or DEFAULT_VOICE).strip()
    if not text:
        return Response("missing text", status_code=400)

    try:
        model, conf = ensure_voice(voice)
    except Exception as e:
        return Response(f"voice error: {e}", status_code=500)

    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        out = tmp.name
    try:
        r = subprocess.run([PIPER_BIN, "-m", model, "-c", conf, "-f", out, "-t", text],
                           capture_output=True, text=True, timeout=120)
        if r.returncode != 0:
            return Response(f"piper failed: {r.stderr}", status_code=500)
        data = open(out, "rb").read()
        return Response(content=data, media_type="audio/wav")
    finally:
        try: os.remove(out)
        except: pass
