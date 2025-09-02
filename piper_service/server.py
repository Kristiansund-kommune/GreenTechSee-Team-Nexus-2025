from fastapi import FastAPI, Body
from fastapi.responses import Response
import os, subprocess, tempfile
from pathlib import Path

# ---- config via env (works with your compose mount) ----
DATA_DIR  = os.getenv("PIPER_DATA", "/models/piper")
VOICE     = os.getenv("PIPER_VOICE", "en_US-libritts-high")  # canonical id
PIPER_BIN = os.getenv("PIPER_BIN", "piper")                   # path to piper binary

# Piper helpers: find + download voices
import piper.download as pdl
# docs/code-reading for these helpers exist in the piper package. :contentReference[oaicite:0]{index=0}

app = FastAPI()

def ensure_voice(voice_name: str = VOICE, data_dir: str = DATA_DIR) -> tuple[str, str]:
    """Make sure {voice}.onnx + .onnx.json exist under data_dir; download if missing."""
    Path(data_dir).mkdir(parents=True, exist_ok=True)

    # Try to resolve files using Piper’s resolver
    model_path, conf_path = pdl.find_voice(voice_name, [data_dir])

    # If missing, pull them (also fetches/refreshes voices.json)
    if not (model_path and Path(model_path).exists() and conf_path and Path(conf_path).exists()):
        pdl.download_voices([voice_name], download_dir=data_dir, update_voices=True)
        model_path, conf_path = pdl.find_voice(voice_name, [data_dir])

    if not (model_path and conf_path):
        raise RuntimeError(f"Voice '{voice_name}' not available in {data_dir}")

    return str(model_path), str(conf_path)

@app.on_event("startup")
def _startup():
    # Resolve once and stash paths in env so your shell-out stays unchanged
    model, conf = ensure_voice()
    os.environ["PIPER_MODEL"]  = model
    os.environ["PIPER_CONFIG"] = conf

@app.get("/health")
def health():
    return {"ok": True, "voice": VOICE, "data_dir": DATA_DIR}

@app.get("/voices")
def list_installed():
    # List .onnx basenames under data dir (quick visibility)
    return {"installed": sorted(p.stem for p in Path(DATA_DIR).glob("*.onnx"))}

@app.post("/tts")
async def tts(text: str = Body(..., media_type="text/plain")):
    model = os.environ.get("PIPER_MODEL")
    conf  = os.environ.get("PIPER_CONFIG")
    if not (model and conf):
        return Response("Piper model/config unresolved", status_code=500)

    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        out = tmp.name

    try:
        # shell-out exactly as you had it
        args = [
            PIPER_BIN, "-m", model, "-c", conf,
            "-f", out, "-t", text
        ]
        r = subprocess.run(args, capture_output=True, text=True, timeout=90)
        if r.returncode != 0:
            return Response(f"piper failed: {r.stderr}", status_code=500)
        data = open(out, "rb").read()
        return Response(content=data, media_type="audio/wav")
    finally:
        try: os.remove(out)
        except: pass
