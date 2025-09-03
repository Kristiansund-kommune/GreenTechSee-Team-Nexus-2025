
<img width="3040" height="666" alt="image" src="https://github.com/user-attachments/assets/6c8fcd18-279f-4142-97a5-952b967ab9e4" />

# GreenTechSee-Team-Nexus-2025

An end-to-end, local-first speech translation pipeline:
- Record/upload audio → transcribe with Whisper
- Translate using a local `llama.cpp` server (OpenAI-compatible API)
- Synthesize speech with Piper TTS (HTTP service)

The ASP.NET Core Web app orchestrates the flow and exposes a simple `/api/translate` endpoint that returns transcript, translation, and a URL to the generated speech.

This repository supports both CPU-only (macOS/Apple Silicon) and CUDA (Windows/NVIDIA) setups via Docker Compose profiles.

---

## Contents

- Overview and architecture
- Prerequisites
- One-time setup (models dir and model download)
- Run the stack (CPU and CUDA)
- Running the Web app (dev and prod)
- Configuration reference
- API reference
- Frontend (Vite + Vue) dev workflow
- Testing & CI
- Troubleshooting
- FAQ

---

## Overview and architecture

Services (Docker):
- Whisper STT HTTP API on port 9000 (`/transcribe`)
- Llama.cpp server on port 18080 (OpenAI-compatible `/v1/chat/completions`)
- Piper TTS HTTP server on port 5002 (`/api/tts`)

Flow in the Web backend:
1. Accept audio upload at `/api/translate`
2. Transcode to 16 kHz mono WAV via `ffmpeg`
3. Whisper transcribes text
4. Llama translates text via chat-completions
5. Piper synthesizes translated text to WAV
6. Result JSON includes transcript, translation, and `audioUrl`

---

## Prerequisites

- Docker Desktop (latest)
- .NET 9 SDK
- Node.js 22 (for frontend dev/build)
- A Hugging Face account (accept Meta Llama 3.1 license)

Optional:
- `huggingface_hub` CLI for convenient model downloads

---

## One-time setup: models directory (.env)

Create a models directory and point Compose to it via `.env`.

macOS:
```
echo "MODELS_DIR=$HOME/models" > .env
mkdir -p "$HOME/models/piper"
```

Windows (PowerShell):
```
Set-Content -Path .env -Value "MODELS_DIR=$env:USERPROFILE\models"
New-Item -ItemType Directory -Force -Path "$env:USERPROFILE\models\piper" | Out-Null
```

Compose mounts `${MODELS_DIR}` into containers at `/models`.

---

## Download the Llama model (GGUF)

Recommended: `Meta-Llama-3.1-8B-Instruct-Q5_K_M.gguf` (balanced quality/RAM). Q4_K_M uses less RAM.

Using `huggingface-cli`:
```
pipx install huggingface_hub || pip install --user huggingface_hub
huggingface-cli login
huggingface-cli download bartowski/Meta-Llama-3.1-8B-Instruct-GGUF \
  Meta-Llama-3.1-8B-Instruct-Q5_K_M.gguf \
  --local-dir "$MODELS_DIR" --local-dir-use-symlinks False
```

Result path: `${MODELS_DIR}/Meta-Llama-3.1-8B-Instruct-Q5_K_M.gguf`

---

## Run the stack (Docker Compose)

CPU (macOS/Apple Silicon):
```
docker compose --profile cpu up -d --build
```

CUDA (Windows/NVIDIA):
```
docker compose --profile cuda up -d --build
```

Exposed services:
- Llama: http://localhost:18080 (health: `/health`)
- Whisper: http://localhost:9000 (POST `/transcribe`)
- Piper: http://localhost:5002 (POST `/api/tts` JSON: `{ "text": "hello" }`)

Quick checks:
```
curl -s http://localhost:18080/health
curl -s -X POST http://localhost:5002/api/tts -H "Content-Type: application/json" -d '{"text":"Hello from Piper"}' -o /tmp/tts.wav
```

Profiles summary:
- `--profile cpu`: `llama-cpu`, `whisper-cpu`, `piper`
- `--profile cuda`: `llama` (CUDA), `whisper` (CUDA), `piper`

---

## Run the Web app

Development (hot-reload frontend via Vite + ASP.NET backend):
1) Start containers (CPU or CUDA profile as above)
2) Start Vite dev server with HTTPS (certs in `cert/`):
```
npm install
npm run dev
```
This launches Vite on https://localhost:5174 with CORS allowed to the ASP.NET dev port.

3) Run the backend:
```
dotnet run --project Web
```
Open https://localhost:7039

Production build:
```
npm run build
dotnet publish Web/Web.csproj -c Release -o ./publish
```
The Razor layout uses `ViteManifestParser` to load built assets from `Web/wwwroot/dist`.

---

## Configuration reference

App settings (defaults in `Web/appsettings.json`):
- `LlamaUrl`: `http://localhost:18080/`
- `WhisperUrl`: `http://localhost:9000/`
- `PiperUrl`: `http://localhost:5002/`
- `LLAMA_MODEL`: string model name forwarded to llama.cpp (default `local`)
- `PIPER_MODEL`, `PIPER_CONFIG`: only used by the legacy Python `server.py` (the Dockerized Piper HTTP service does not need these on the host)

These can be overridden via environment variables or `appsettings.Development.json`.

Frontend dev configuration (`vite.config.ts`):
- Root: `Web/wwwroot`
- HTTPS with local certs `cert/localhost.crt` and `cert/localhost.key`
- CORS origin: `https://localhost:7039`

---

## API reference

Backend endpoint (`Web/Program.cs`):

POST `/api/translate`
- Form fields: `audio` (file), `sourceLang` (optional, default auto), `targetLang` (required), `prompt` (optional)
- Response JSON:
```
{
  "transcript": string,
  "translation": string,
  "audioUrl": "/api/translate/audio/{id}"
}
```

GET `/api/translate/audio/{id}` → returns generated WAV.

Upstream services:
- Whisper: POST `http://localhost:9000/transcribe` (multipart: `audio`, `sourceLang`)
- Llama.cpp: POST `http://localhost:18080/v1/chat/completions`
  - Minimal payload: `{ model, messages: [{role:"system",...},{role:"user",...}], temperature }`
- Piper HTTP: POST `http://localhost:5002/api/tts` with JSON `{ text }` → `audio/wav`

---

## Frontend dev workflow (Vite + Vue 3)

- Source root: `Web/wwwroot`
- Entrypoint is emitted via `Pages/Shared/VueEntrypointPartial.cshtml`
- During development, Razor injects the Vite client and loads `scripts/main.ts` directly from the dev server
- In production, assets are loaded from the Vite manifest under `Web/wwwroot/dist/.vite/manifest.json`

Useful npm scripts:
- `npm run dev`: Starts Vite and a watcher that runs lint + type-check + tests on changes
- `npm run build`: Lints, audits, type-checks, and builds via Vite
- `npm run test` / `npm run test:watch`: Vitest unit tests

---

## Testing & CI

Unit tests (Vitest) live under `Web/wwwroot/scripts/tests/`. CI (GitHub Actions) runs on every push and PR:
- Install Node deps, lint, type-check, and run tests
- Build frontend (Vite) with audits
- Restore, build, and publish the .NET Web project
- Upload the publish artifact

---

## Troubleshooting

- Llama health: `curl http://localhost:18080/health`
- Whisper logs: `docker logs <whisper-container>` or `<whisper-cpu-container>`
- Piper voices cache under `${MODELS_DIR}/piper` (auto-downloads on first use)
- CUDA on Windows: ensure Docker Desktop, WSL2, and NVIDIA GPU support are enabled
- Audio upload issues: ensure `ffmpeg` available inside Web container or host if running without containerization (Web uses host `ffmpeg`)
- CORS/HTTPS during dev: ensure `cert/localhost.crt` and `cert/localhost.key` exist; Vite uses them for HTTPS

---

## FAQ

**Can I change the TTS voice?**
Yes. The Piper HTTP service accepts a `--model` alias (e.g., `en_US-libritts-high`). Change it in `docker-compose.yml` under the `piper` service, and the model will be downloaded to `${MODELS_DIR}/piper` on first use.

**Can I use a different Llama GGUF?**
Yes. Place the `.gguf` file under `${MODELS_DIR}` and update the `llama`/`llama-cpu` service command in `docker-compose.yml` to point `-m /models/<your-file>.gguf`.

**Where are generated audio files stored?**
Under `Web/data/audio` with a unique id. The API returns an `audioUrl` you can fetch.

**Do I need the local Piper binary?**
No. The repository uses a Dockerized Piper HTTP service. The legacy `piper_service/server.py` is kept for reference.

---

## License and credits

- Llama.cpp Docker images by ggml-org
- Whisper model usage via faster-whisper
- Piper TTS via piper-tts
- Frontend tooling: Vite + Vue 3 + Vitest

