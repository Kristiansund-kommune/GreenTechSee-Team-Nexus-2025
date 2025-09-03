<template>
	<div class="container">
		<div class="mx-auto mb-3">
			<img :src="`/images/nexus.png`" style="max-height: 100px;" />
		</div>
		<div class="mb-3">
			<div class="flex items-center mb-3">
				<label class="text-sm">From</label>
				<select v-model="sourceLang" class="border px-2 py-1 rounded">
					<option value="auto">Auto (detect)</option>
					<option v-for="l in LANG_OPTIONS" :key="l.code" :value="l.code">
						{{ l.label }}
					</option>
				</select>

				<label class="text-sm">→ To</label>
				<select v-model="targetLang" class="border px-2 py-1 rounded">
					<option value="en">English</option>
					<option value="no">Norwegian</option>
					<option value="es">Spanish</option>
					<option value="uk">Ukrainian</option>
				</select>
			</div>

			<button type="button" :class="['btn text-white mb-3', isRecording ? 'bg-danger' : 'bg-primary']" style="min-width: 300px" @pointerdown="startRecording" @pointerup="stopRecording" @pointerleave="stopRecording" @keydown.space.prevent="startRecording" @keyup.space.prevent="stopRecording">
				{{ isRecording ? 'Release to stop…' : 'Press & hold to record' }}
			</button>

			<div v-if="status" class="text-sm text-gray-600 mb-3">{{ status }}</div>

			<div class="mb-3">
				<div v-if="transcript" class="text-sm">
					<strong>Heard:</strong> {{ transcript }}
				</div>
				<div v-if="translation" class="text-sm">
					<strong>Translation:</strong> {{ translation }}
				</div>
			</div>

			<audio v-if="audioUrl" ref="audioEl" :src="audioUrl" controls playsinline preload="auto" class="w-full"></audio>

			<button v-if="showPlayPrompt" @click="manualPlay" class="mt-2 px-3 py-2 rounded bg-emerald-600 text-white">
				Play translation
			</button>
		</div>
	</div>
</template>

<style scoped>
button {
	/* better press/hold on mobile */
	touch-action: none;
	border: 3px solid transparent;
}
button:focus,
button:active {
	border: 3px solid black;
}
</style>

<script lang="ts" setup>
import { ref, onBeforeUnmount, nextTick } from 'vue';
import * as _ from "lodash-es";

const isRecording = ref(false);
const status = ref('');
const mediaRecorder = ref<MediaRecorder | null>(null);
const chunks: Blob[] = [];
const streamRef = ref<MediaStream | null>(null);

const sourceLang = ref<'auto' | string>('auto');
const targetLang = ref<string>('en');

const transcript = ref('');
const translation = ref('');
const audioUrl = ref<string | null>(null);

// --- audio playback (Web Audio first, <audio> fallback) ---
const audioEl = ref<HTMLAudioElement | null>(null);
const showPlayPrompt = ref(false);
const audioCtx = ref<AudioContext | null>(null);
let currentNode: AudioBufferSourceNode | null = null;

type Lang = { code: string; label: string };

const LANG_OPTIONS: Lang[] = _.sortBy([
	{ code: 'en', label: 'English' },
	{ code: 'zh', label: 'Chinese' },
	{ code: 'hi', label: 'Hindi' },
	{ code: 'es', label: 'Spanish' },
	{ code: 'fr', label: 'French' },
	{ code: 'ar', label: 'Arabic' },
	{ code: 'bn', label: 'Bengali' },
	{ code: 'pt', label: 'Portuguese' },
	{ code: 'ru', label: 'Russian' },
	{ code: 'ur', label: 'Urdu' },
	{ code: 'id', label: 'Indonesian' },
	{ code: 'de', label: 'German' },
	{ code: 'ja', label: 'Japanese' },
	{ code: 'sw', label: 'Swahili' },
	{ code: 'mr', label: 'Marathi' },
	{ code: 'te', label: 'Telugu' },
	{ code: 'tr', label: 'Turkish' },
	{ code: 'ta', label: 'Tamil' },
	{ code: 'vi', label: 'Vietnamese' },
	{ code: 'ko', label: 'Korean' },
	{ code: 'it', label: 'Italian' },
	{ code: 'ha', label: 'Hausa' },
	{ code: 'th', label: 'Thai' },
	{ code: 'gu', label: 'Gujarati' },
	{ code: 'kn', label: 'Kannada' },
	{ code: 'fa', label: 'Persian (Farsi)' },
	{ code: 'ml', label: 'Malayalam' },
	{ code: 'or', label: 'Odia (Oriya)' },
	{ code: 'my', label: 'Burmese (Myanmar)' },
	{ code: 'nl', label: 'Dutch' },
	{ code: 'yo', label: 'Yoruba' },
	{ code: 'pl', label: 'Polish' },
	{ code: 'am', label: 'Amharic' },
	{ code: 'az', label: 'Azerbaijani' },
	{ code: 'uk', label: 'Ukrainian' },
	{ code: 'ig', label: 'Igbo' },
	{ code: 'uz', label: 'Uzbek' },
	{ code: 'ne', label: 'Nepali' },
	{ code: 'si', label: 'Sinhala' },
	{ code: 'ro', label: 'Romanian' },
	{ code: 'km', label: 'Khmer' },
	{ code: 'el', label: 'Greek' },
	{ code: 'cs', label: 'Czech' },
	{ code: 'sv', label: 'Swedish' },
	{ code: 'hu', label: 'Hungarian' },
	{ code: 'he', label: 'Hebrew' },
	{ code: 'pa', label: 'Punjabi' },
	{ code: 'sr', label: 'Serbian' },
	{ code: 'bg', label: 'Bulgarian' },
	{ code: 'tl', label: 'Tagalog (Filipino)' },
], e => e.label);

function ensureAudioUnlocked() {
	if (!audioCtx.value) {
		// @ts-ignore webkit prefix for older iOS
		const Ctx = window.AudioContext || (window as any).webkitAudioContext;
		audioCtx.value = new Ctx();
	}
	if (audioCtx.value.state === 'suspended') {
		void audioCtx.value.resume();
	}
}

function stopWebAudio() {
	try { currentNode?.stop(0); } catch { }
	currentNode?.disconnect();
	currentNode = null;
}

async function playViaWebAudio(url: string) {
	if (!audioCtx.value) { throw new Error('AudioContext not ready'); }
	await audioCtx.value.resume(); // in case it got suspended
	const ab = await fetch(url, { cache: 'no-store' }).then(r => r.arrayBuffer());
	const buf = await audioCtx.value.decodeAudioData(ab);
	stopWebAudio(); // avoid overlap
	const src = audioCtx.value.createBufferSource();
	src.buffer = buf;
	src.connect(audioCtx.value.destination);
	src.start(0);
	currentNode = src;
}

async function autoplayNow(url: string) {
	showPlayPrompt.value = false;

	// try Web Audio (most reliable after a user gesture)
	try {
		await playViaWebAudio(url);
		return;
	} catch {
		// fall back to <audio>.play()
	}

	await nextTick();
	try {
		await audioEl.value?.play();
	} catch {
		showPlayPrompt.value = true; // user must tap
	}
}

async function manualPlay() {
	ensureAudioUnlocked();
	if (audioUrl.value) {
		try {
			await playViaWebAudio(audioUrl.value);
			showPlayPrompt.value = false;
		} catch {
			try { await audioEl.value?.play(); showPlayPrompt.value = false; } catch { }
		}
	}
}

// --- recording ---
function pickMime(): string {
	const candidates = ['audio/webm;codecs=opus', 'audio/webm', 'audio/mp4'];
	for (const c of candidates) { if (MediaRecorder.isTypeSupported(c)) { return c; } }
	return '';
}

async function startRecording() {
	if (isRecording.value) { return; }
	ensureAudioUnlocked();
	stopWebAudio();
	transcript.value = '';
	translation.value = '';
	audioUrl.value = null;
	status.value = 'Requesting microphone…';

	const stream = await navigator.mediaDevices.getUserMedia({
		audio: { echoCancellation: true, noiseSuppression: true, channelCount: 1, sampleRate: 48000 },
	});
	streamRef.value = stream;

	const mimeType = pickMime();
	const mr = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
	mediaRecorder.value = mr;
	chunks.length = 0;

	mr.ondataavailable = (e: BlobEvent) => { if (e.data && e.data.size > 0) { chunks.push(e.data); } };
	mr.onstart = () => { isRecording.value = true; status.value = 'Recording…'; };
	mr.onstop = async () => {
		isRecording.value = false;
		status.value = 'Uploading…';
		const blob = new Blob(chunks, { type: mr.mimeType || 'audio/webm' });
		await sendForTranslation(blob);
		cleanupStream();
	};

	mr.start();
}

function stopRecording() {
	if (!isRecording.value) { return; }
	mediaRecorder.value?.stop();
}

function cleanupStream() {
	streamRef.value?.getTracks().forEach(t => t.stop());
	streamRef.value = null;
	mediaRecorder.value = null;
}

// --- server call ---
async function sendForTranslation(blob: Blob) {
	try {
		const fd = new FormData();
		fd.append('audio', blob, 'recording.webm');
		fd.append('sourceLang', sourceLang.value);
		fd.append('targetLang', targetLang.value);
		fd.append('prompt', 'Translate this speech faithfully. Respond only with the translation.');

		const res = await fetch('/api/translate', { method: 'POST', body: fd });
		if (!res.ok) { throw new Error((await res.text()) || `HTTP ${res.status}`); }

		const data = await res.json() as { transcript: string; translation: string; audioUrl: string };
		transcript.value = data.transcript;
		translation.value = data.translation;
		audioUrl.value = data.audioUrl;
		status.value = 'Done.';

		// immediate autoplay using unlocked Web Audio; fallback is handled inside
		await autoplayNow(data.audioUrl);
	} catch (err: any) {
		console.error(err);
		status.value = `Error: ${err.message || err}`;
		showPlayPrompt.value = true;
	}
}

onBeforeUnmount(() => {
	cleanupStream();
	stopWebAudio();
});
</script>
