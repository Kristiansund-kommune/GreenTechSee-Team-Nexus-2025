from fastapi import FastAPI
from fastapi.responses import Response
import os, subprocess, tempfile

app = FastAPI()

@app.post("/tts")
async def tts(text: str):
    model = os.environ.get("PIPER_MODEL")
    conf  = os.environ.get("PIPER_CONFIG")
    if not (model and conf):
        return Response("PIPER_MODEL/CONFIG not set", status_code=500)

    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        out = tmp.name
    try:
        cmd = ["piper", "-m", model, "-c", conf, "-f", out, "-t", text]
        r = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
        if r.returncode != 0:
            return Response(f"piper failed: {r.stderr}", status_code=500)
        data = open(out, "rb").read()
        return Response(content=data, media_type="audio/wav")
    finally:
        try: os.remove(out)
        except: pass
