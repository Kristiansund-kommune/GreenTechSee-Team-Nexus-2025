<template>
	<div class="p-4 max-w-xl mx-auto space-y-4">
		<div class="flex items-center gap-3">
			<label class="text-sm">From</label>
			<select v-model="sourceLang" class="border px-2 py-1 rounded">
				<option value="auto">Auto</option>
				<option value="en">English</option>
				<option value="no">Norwegian</option>
				<option value="es">Spanish</option>
				<!-- add more -->
			</select>

			<label class="text-sm">→ To</label>
			<select v-model="targetLang" class="border px-2 py-1 rounded">
				<option value="en">English</option>
				<option value="no">Norwegian</option>
				<option value="es">Spanish</option>
			</select>
		</div>

		<button :class="['w-full py-4 rounded-2xl btn btn-primary',
			isRecording ? 'bg-red-600' : 'bg-indigo-600']" @pointerdown="startRecording" @pointerup="stopRecording" @pointerleave="stopRecording" @keydown.space.prevent="startRecording" @keyup.space.prevent="stopRecording">
			{{ isRecording ? 'Release to stop…' : 'Press & hold to record' }}
		</button>

		<div v-if="status" class="text-sm text-gray-600">{{ status }}</div>

		<div v-if="transcript" class="text-sm">
			<strong>Heard:</strong> {{ transcript }}
		</div>
		<div v-if="translation" class="text-sm">
			<strong>Translation:</strong> {{ translation }}
		</div>

		<audio v-if="audioUrl" :src="audioUrl" controls class="w-full"></audio>
	</div>
</template>

<style scoped>
button {
	touch-action: none;
}

/* better press/hold on mobile */
</style>

<script lang="ts" setup>
import { ref, onBeforeUnmount } from 'vue';

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

function pickMime(): string {
	const candidates = [
		'audio/webm;codecs=opus',
		'audio/webm',
		'audio/mp4', // Safari
	];
	for (const c of candidates) {
		if (MediaRecorder.isTypeSupported(c)) {return c;}
	}
	return ''; // let browser choose
}

async function startRecording() {
	if (isRecording.value) {return;}
	transcript.value = '';
	translation.value = '';
	audioUrl.value = null;
	status.value = 'Requesting microphone…';

	const stream = await navigator.mediaDevices.getUserMedia({
		audio: {
			echoCancellation: true,
			noiseSuppression: true,
			channelCount: 1,
			sampleRate: 48000,
		},
	});
	streamRef.value = stream;

	const mimeType = pickMime();
	const mr = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
	mediaRecorder.value = mr;
	chunks.length = 0;

	mr.ondataavailable = (e: BlobEvent) => {
		if (e.data && e.data.size > 0) {chunks.push(e.data);}
	};
	mr.onstart = () => {
		isRecording.value = true;
		status.value = 'Recording…';
	};
	mr.onstop = async () => {
		isRecording.value = false;
		status.value = 'Uploading…';
		const blob = new Blob(chunks, { type: mr.mimeType || 'audio/webm' });
		await sendForTranslation(blob);
		cleanupStream();
	};

	mr.start(); // non-streaming (we send after release)
}

function stopRecording() {
	if (!isRecording.value) {return;}
	mediaRecorder.value?.stop();
}

function cleanupStream() {
	streamRef.value?.getTracks().forEach(t => t.stop());
	streamRef.value = null;
	mediaRecorder.value = null;
}

async function sendForTranslation(blob: Blob) {
	try {
		const fd = new FormData();
		fd.append('audio', blob, 'recording.webm');
		fd.append('sourceLang', sourceLang.value);
		fd.append('targetLang', targetLang.value);
		fd.append('prompt', 'Translate this speech faithfully. Respond only with the translation.');

		const res = await fetch('/api/translate', {
			method: 'POST',
			body: fd,
		});

		if (!res.ok) {
			const t = await res.text();
			throw new Error(t || `HTTP ${res.status}`);
		}
		const data = await res.json() as {
			transcript: string;
			translation: string;
			audioUrl: string; // /api/translate/audio/{id}
		};

		transcript.value = data.transcript;
		translation.value = data.translation;
		audioUrl.value = data.audioUrl;
		status.value = 'Done.';
	} catch (err: any) {
		console.error(err);
		status.value = `Error: ${err.message || err}`;
	}
}

onBeforeUnmount(cleanupStream);
</script>
