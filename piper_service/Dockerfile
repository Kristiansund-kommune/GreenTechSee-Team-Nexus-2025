# Small, no-GPU TTS service using Piper’s HTTP server
FROM python:3.12-slim

# Optional: WAV encoding is handled by Piper; ffmpeg is not required here.
# If you want it anyway, uncomment the next two lines:
# RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*

# Install Piper + HTTP server extras (FastAPI/Flask deps are bundled via extras)
RUN pip install --no-cache-dir "piper-tts[http]"

# Persist voices to a mounted dir; Piper can auto-download by alias
ENV PIPER_DATA=/models/piper

EXPOSE 5002
CMD ["python", "-m", "piper.http_server", \
     "--host", "0.0.0.0", "--port", "5002", \
     "--model", "en_US-libritts-high", \
     "--data-dir", "/models/piper", "--download-dir", "/models/piper"]
