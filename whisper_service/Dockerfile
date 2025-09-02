FROM nvidia/cuda:12.6.3-cudnn-runtime-ubuntu24.04

# Python + venv + ffmpeg (handy for non-WAV uploads)
RUN apt-get update && apt-get install -y python3 python3-pip python3-venv ffmpeg \
 && rm -rf /var/lib/apt/lists/*

# Avoid PEP 668 by installing into a venv
ENV VIRTUAL_ENV=/opt/venv
RUN python3 -m venv $VIRTUAL_ENV
ENV PATH="$VIRTUAL_ENV/bin:$PATH"

# Install runtime deps
RUN pip install --no-cache-dir --upgrade pip setuptools wheel python-multipart \
 && pip install --no-cache-dir fastapi "uvicorn[standard]" faster-whisper

WORKDIR /app
COPY main.py /app/main.py

ENV WHISPER_MODEL=large-v3
ENV WHISPER_DEVICE=cuda
ENV WHISPER_COMPUTE=float16

EXPOSE 9000
CMD ["python", "main.py"]
