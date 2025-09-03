FROM python:3.12-slim

# Nuke the unrelated 'piper' package if it ever sneaks in
RUN pip uninstall -y piper || true

# Install the right one (brings the http extras too if you want Flask)
RUN pip install --no-cache-dir "piper-tts[http]==1.3.0" fastapi "uvicorn[standard]"

WORKDIR /app
COPY server.py /app/server.py

ENV PIPER_DATA=/models/piper
ENV PIPER_DEFAULT_VOICE=en_US-libritts-high

EXPOSE 5002
CMD ["uvicorn","server:app","--host","0.0.0.0","--port","5002"]
