# piper_service/Dockerfile
FROM python:3.12-slim

# Piper with HTTP extras (brings Flask), then download the voice
RUN pip install --no-cache-dir "piper-tts[http]"

# Where voices live *inside the image*
ENV PIPER_DATA=/opt/piper/voices
RUN mkdir -p "$PIPER_DATA"

# Bake the voice files into the image layer
RUN ["python", "-m", "piper.download_voices", "en_US-libritts-high", "--data-dir", "/opt/piper/voices", "--download-dir", "/opt/piper/voices"]

EXPOSE 5002
CMD ["python", "-m", "piper.http_server", "--host", "0.0.0.0", "--port", "5002", "--model", "en_US-libritts-high", "--data-dir", "/opt/piper/voices"]
