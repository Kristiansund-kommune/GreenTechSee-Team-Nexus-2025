<template>
	<div class="mobile-root">
		<!-- Landing view -->
		<div v-if="currentView === 'landing'" class="landing">
			<div class="landing-header px-4 mx-4">
				<img :src="`/images/nexus.png`" class="logo p-4 m-2" />
			</div>
			<div class="language pb-4">
				<div class="lang-label">{{ currentLang.label?.toUpperCase?.() || currentLang.label }}</div>
				<div class="flag-wrap">
					<button class="flag" @click="openPicker">
						<span class="flag-emoji">{{ currentLang.flag }}</span>
					</button>
					<button class="pencil" @click="openPicker" aria-label="Edit language">
						<img src="/scripts/pencel.svg" alt="Edit" />
					</button>
				</div>
			</div>
			<div class="cards">
				<button class="card" @click="goParticipant">
					<div class="card-image"><img src="/images/deltager.png" alt="Participant" /></div>
					<div class="card-title">{{ currentLang.participant }}</div>
				</button>
				<button class="card" @click="goCoach">
					<div class="card-image"><img src="/images/veilder.png" alt="Coach" /></div>
					<div class="card-title">{{ currentLang.coach }}</div>
				</button>
			</div>
		</div>

		<!-- Language picker overlay -->
		<div v-if="showPicker" class="picker" @click.self="closePicker">
			<div class="picker-panel">
				<div class="picker-header">{{ t('common.selectLanguage') }}</div>
				<div class="picker-list">
					<button v-for="(meta, code) in supportedLangsSorted" :key="code" class="picker-item" @click="chooseLanguage(code as string)">
						<span class="picker-flag">{{ meta.flag || '🏳️' }}</span>
						<span class="picker-label">{{ meta.label }}</span>
					</button>
				</div>
				<button class="picker-close" @click="closePicker">{{ t('common.close') }}</button>
			</div>
		</div>

		<!-- Existing app view (unchanged logic) -->
		<div v-else-if="currentView === 'app'" class="container">
			<div class="landing-header px-4 ps-0 ms-3 mx-4">
				<img :src="`/images/nexus.png`" class="logo p-4 m-2" />
			</div>
			<div class="app-surface">
				<div class="my-3 app-actions">
					<button class="change-lang rounded" @click="goToLanguage">
						<img src="/scripts/pencel.svg" alt="Edit" />
						<span>{{ t('common.changeLanguage') }}</span>
					</button>
				</div>
				<div class="mb-3">
					<div class="flex items-center mb-3 form-row">
						<label class="text-sm px-2">{{ t('common.from') }}</label>
						<select v-model="sourceLang" class="border px-2 py-1 rounded">
							<option value="auto">{{ t('common.autoDetect') }}</option>
							<option v-for="l in LANG_OPTIONS" :key="l.code" :value="l.code">
								{{ l.label }}
							</option>
						</select>
						<br>
						<div class="p-1"></div>
						<label class="text-sm px-2"> {{ t('common.to') }}</label>
						<select v-model="targetLang" class="border px-2 py-1 rounded">
							<option v-for="l in LANG_OPTIONS" :key="l.code" :value="l.code">
								{{ l.label }}
							</option>
						</select>
					</div>

					<button type="button" :class="['btn text-white mb-3 recording-button', isRecording ? 'bg-danger' : 'bg-primary']" style="min-width: 300px" @pointerdown="startRecording" @pointerup="stopRecording" @pointerleave="stopRecording" @keydown.space.prevent="startRecording" @keyup.space.prevent="stopRecording">
						{{ isRecording ? t('common.releaseToStop') : t('common.pressHold') }}
					</button>

					<div v-if="status" class="text-sm text-gray-600 mb-3">{{ status }}</div>

					<div class="mb-3">
						<div v-if="transcript" class="text-sm">
							<strong>{{ t('common.heard') }}</strong> {{ transcript }}
						</div>
						<div v-if="translation" class="text-sm">
							<strong>{{ t('common.translation') }}</strong> {{ translation }}
						</div>
					</div>

					<audio v-if="audioUrl" ref="audioEl" :src="audioUrl" controls playsinline preload="auto" class="w-full"></audio>

					<button v-if="showPlayPrompt" @click="manualPlay" class="mt-2 px-3 py-2 rounded bg-emerald-600 text-white">
						{{ t('common.playTranslation') }}
					</button>
				</div>
			</div>
		</div>
	</div></template>

<style scoped>
.mobile-root {
	min-height: 100dvh;
	background: white;
	;
}
.landing {
	display: flex;
	flex-direction: column;
	align-items: center;
	padding: 28px 16px;
}
.landing-header { width: 100%; display: flex; justify-content: center; margin: 12px 0 28px; }
.logo { width: 100%; max-width: 240px; height: auto; display: block; }
.language { display: flex; flex-direction: column; align-items: center; margin-bottom: 28px; }
.flag-wrap { position: relative; width: 88px; height: 88px; }
.lang-label { color: #6b7280; font-size: 14px; letter-spacing: .08em; margin-bottom: 8px; }
.flag {
	width: 88px; height: 88px; border-radius: 9999px; background: white; box-shadow: 0 3px 10px rgba(0,0,0,.08);
	display: grid; place-items: center; border: none;
}
.pencil { position: absolute; bottom: -6px; right: -6px; width: 36px; height: 36px; border-radius: 10px; border: none; background: #07869A; display: grid; place-items: center; box-shadow: 0 2px 6px rgba(0,0,0,.15); }
.pencil img { width: 18px; height: 18px; }
.flag-emoji { font-size: 42px; }
.cards { width: 100%; display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin-top: 16px; max-width: 520px; margin-left: auto; margin-right: auto; }
.card { background: #07869A; border: none; border-radius: 12px; padding: 20px; color: white; text-align: center; min-height: 180px; display: flex; flex-direction: column; justify-content: center; align-items: center; }
.card-image { width: 120px; height: 120px; display: grid; place-items: center; margin-bottom: 8px; }
.card-image img { max-width: 100%; max-height: 100%; object-fit: contain; }
.card-title { font-size: 16px; letter-spacing: .06em; }

/* picker */
.picker { position: fixed; inset: 0; background: rgba(0,0,0,.35); display: grid; place-items: end center; }
.picker-panel { background: white; width: 100%; max-height: 80vh; border-top-left-radius: 16px; border-top-right-radius: 16px; overflow: hidden; display: flex; flex-direction: column; }
.picker-header { padding: 12px 16px; font-weight: 600; text-align: center; border-bottom: 1px solid #eee; }
.picker-list { overflow: auto; -webkit-overflow-scrolling: touch; }
.picker-item { width: 100%; display: flex; align-items: center; gap: 12px; padding: 12px 16px; border: none; background: white; border-bottom: 1px solid #f2f2f2; text-align: left; }
.picker-flag { font-size: 20px; width: 28px; }
.picker-label { font-size: 16px; }
.picker-close { border: none; background: #07869A; color: white; padding: 12px 16px; }

button {
	border: 3px solid transparent;
}
button:focus,
button:active {
	border: 3px solid black;
}
button.recording-button {
	/* better press/hold on mobile */
	touch-action: none;
}

.top-actions { display: flex; justify-content: flex-end; margin-bottom: 8px; }
.change-lang { display: inline-flex; align-items: center; gap: 8px; border: none; background: #07869A; color: white; padding: 8px 12px; border-radius: 8px; }
.change-lang img { width: 16px; height: 16px; }

/* app surface (old logic view) */
.app-surface {
	background: #E1E9EB;
	border-radius: 16px;
	padding: 24px 18px;
	margin: 0 16px 32px;
	width: 100%;
	max-width: 560px;
	margin-left: auto;
	margin-right: auto;
	display: flex;
	flex-direction: column;
	align-items: center;
	text-align: center;
}
.app-surface > * + * { margin-top: 16px; }
.app-actions { display: flex; justify-content: center; margin-bottom: 16px; width: 100%; }
.form-row { gap: 16px; justify-content: center; flex-wrap: wrap; width: 100%; }
.app-surface .btn { align-self: center; }
.app-surface audio { width: 100%; max-width: 520px; }
</style>

<script lang="ts" setup>
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue';
import * as _ from "lodash-es";
import LANGS from './languages.json';
import TRANSLATIONS from './translations.json';

const isRecording = ref(false);
const status = ref('');
const mediaRecorder = ref<MediaRecorder | null>(null);
const chunks: Blob[] = [];
const streamRef = ref<MediaStream | null>(null);

const sourceLang = ref<'auto' | string>('auto');
const targetLang = ref<string>('no');

// --- simple view switch ---
type View = 'landing' | 'app' | 'coach';
const currentView = ref<View>('landing');

const supportedLangs = LANGS as Record<string, { label: string; flag?: string; participant?: string; coach?: string }>;
const supportedLangsSorted = computed(() => Object.fromEntries(Object.entries(supportedLangs).sort((a, b) => a[1].label.localeCompare(b[1].label))));
const selectedLangCode = ref<string>(localStorage.getItem('selectedLang') || 'no');
const currentLang = computed(() => supportedLangs[selectedLangCode.value] || supportedLangs['no']);

// --- i18n helper (UI language follows selected language) ---
type AnyMap = Record<string, any>;
const translations = TRANSLATIONS as unknown as AnyMap;
const uiLang = computed(() => selectedLangCode.value || 'en');
function t(key: string): string {
	const parts = key.split('.');
	let node: any = translations;
	for (const p of parts) { node = node?.[p]; }
	if (!node || typeof node !== 'object') { return key; }
	return node[uiLang.value] || node['en'] || Object.values(node)[0] || key;
}

const showPicker = ref(false);
function openPicker() { showPicker.value = true; }
function closePicker() { showPicker.value = false; }
function chooseLanguage(code: string) {
	if (supportedLangs[code]) {
		selectedLangCode.value = code;
		localStorage.setItem('selectedLang', selectedLangCode.value);
	}
	showPicker.value = false;
}

function goToLanguage() {
	currentView.value = 'landing';
}

function goParticipant() {
	// Move to existing app with the chosen target language
	targetLang.value = selectedLangCode.value;
	currentView.value = 'app';
}

function goCoach() {
	// For now just use same as app; can be customized later
	currentView.value = 'app';
}

onMounted(() => {
	// Restore language into current app if re-entering
	const saved = localStorage.getItem('selectedLang');
	if (saved) { targetLang.value = saved; }
});

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
	{ code: 'no', label: 'Norwegian' },
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
	try { currentNode?.stop(0); } catch (e) { void e; }
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
			try { await audioEl.value?.play(); showPlayPrompt.value = false; } catch (e) { void e; }
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
	status.value = t('common.status_requestMic');

	const stream = await navigator.mediaDevices.getUserMedia({
		audio: { echoCancellation: true, noiseSuppression: true, channelCount: 1, sampleRate: 48000 },
	});
	streamRef.value = stream;

	const mimeType = pickMime();
	const mr = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
	mediaRecorder.value = mr;
	chunks.length = 0;

	mr.ondataavailable = (e: BlobEvent) => { if (e.data && e.data.size > 0) { chunks.push(e.data); } };
	mr.onstart = () => { isRecording.value = true; status.value = t('common.status_recording'); };
	mr.onstop = async () => {
		isRecording.value = false;
		status.value = t('common.status_uploading');
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
		status.value = t('common.status_done');

		// immediate autoplay using unlocked Web Audio; fallback is handled inside
		await autoplayNow(data.audioUrl);
	} catch (err: any) {
		console.error(err);
		status.value = `${t('common.status_error_prefix')} ${err.message || err}`;
		showPlayPrompt.value = true;
	}
}

onBeforeUnmount(() => {
	cleanupStream();
	stopWebAudio();
});
</script>
