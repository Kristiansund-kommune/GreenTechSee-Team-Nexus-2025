from fastapi import FastAPI, UploadFile, Form
from fastapi.responses import JSONResponse
from faster_whisper import WhisperModel
import uvicorn, tempfile, shutil, os

app = FastAPI()
model_path = os.environ.get("WHISPER_MODEL", "large-v3")
device = os.environ.get("WHISPER_DEVICE", "cuda")
compute = os.environ.get("WHISPER_COMPUTE", "float16")

wmodel = WhisperModel(model_path, device=device, compute_type=compute)

@app.post("/transcribe")
async def transcribe(audio: UploadFile, sourceLang: str = Form("auto")):
    with tempfile.NamedTemporaryFile(delete=False, suffix=os.path.splitext(audio.filename)[1]) as tmp:
        shutil.copyfileobj(audio.file, tmp)
        tmp_path = tmp.name
    try:
        segments, info = wmodel.transcribe(tmp_path, language=None if sourceLang=="auto" else sourceLang)
        text = "".join([s.text for s in segments]).strip()
        return JSONResponse({"text": text, "language": info.language})
    finally:
        os.remove(tmp_path)

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=9000)
