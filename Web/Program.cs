using Microsoft.AspNetCore.StaticFiles;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Legg til tjenester i containeren.

builder.Services.AddControllers();
builder.Services.AddRazorPages();

builder.Services.AddHttpClient("whisper", c => c.BaseAddress = new Uri(
	builder.Configuration["Services:WhisperUrl"] ?? "http://localhost:9000/"));

builder.Services.AddHttpClient("llama", c => c.BaseAddress = new Uri(
	builder.Configuration["Services:LlamaUrl"] ?? "http://localhost:18080/"));

// If using the HTTP piper service instead of local binary:
builder.Services.AddHttpClient("piper", c => c.BaseAddress = new Uri(
	builder.Configuration["Services:PiperUrl"] ?? "http://localhost:5002/"));
var app = builder.Build();

// Konfigurer HTTP-forespørselsrørledningen.
// app.UseHttpsRedirection();

// Sikkerhetsheadere
app.Use(async (context, next) =>
{
	var env = app.Environment;

	// Hindre MIME-type-sniffing
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";

	// Grunnleggende beskyttelse mot clickjacking
	context.Response.Headers["X-Frame-Options"] = "DENY";

	// Reduser lekkasje av referrer
	context.Response.Headers["Referrer-Policy"] = "no-referrer";

	// Aktiver eldre XSS-filtre (ufarlig i moderne nettlesere)
	context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

	// Begrens kraftige API-er som standard (løs og trygg standard)
	context.Response.Headers["Permissions-Policy"] =
		"geolocation=(), camera=(), fullscreen=(self)";

	await next();
});

app.UseAuthorization();
app.UseStaticFiles();

app.MapRazorPages();
app.MapControllers();

var ffmpegPath = app.Environment.IsDevelopment() ? "ffmpeg" : "C:\\home\\site\\deployments\\tools\\ffmpeg.exe";

var useOpenAi = app.Configuration["OpenAi:UseOpenAi"] == "true";
if (useOpenAi)
{
	app.MapPost("/api/translate", async (HttpRequest req, IHttpClientFactory httpFactory) =>
	{
		var openAiKey = app.Configuration["OpenAi:ApiKey"] ?? "";
		var transcribeUrl = app.Configuration["OpenAi:TranscribeEndpoint"] ?? "";
		var translateUrl = app.Configuration["OpenAi:TranslateEndpoint"] ?? "";
		var ttsKey = app.Configuration["OpenAi:TtsKey"] ?? "";
		var ttsUrl = app.Configuration["OpenAi:TtsEndpoint"] ?? "";

		if (string.IsNullOrWhiteSpace(openAiKey) || string.IsNullOrWhiteSpace(transcribeUrl) || string.IsNullOrWhiteSpace(translateUrl))
			return Results.Problem("OpenAI/Azure configuration missing (ApiKey/TranscribeEndpoint/TranslateEndpoint).");

		if (string.IsNullOrWhiteSpace(ttsKey) || string.IsNullOrWhiteSpace(ttsUrl))
			return Results.Problem("TTS configuration missing (TtsKey/TtsEndpoint).");

		// ---- read form & stash temp files ----
		var form = await req.ReadFormAsync();
		var file = form.Files["audio"];
		if (file is null || file.Length == 0) return Results.BadRequest("Missing audio");

		var sourceLang = form["sourceLang"].ToString();
		var targetLang = form["targetLang"].ToString();
		var prompt = form["prompt"].ToString();

		var workDir = Path.Combine(Path.GetTempPath(), "stt", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(workDir);
		var rawPath = Path.Combine(workDir, file.FileName);
		using (var fs = File.Create(rawPath)) await file.CopyToAsync(fs);

		// Normalize to 16 kHz mono PCM WAV — keeps behavior consistent across browsers/codecs
		var wavPath = Path.Combine(workDir, "audio.wav");
		var ffOk = await Run(
			ffmpegPath,
			$"-y -i \"{rawPath}\" -ac 1 -ar 16000 -vn \"{wavPath}\"",
			timeout: TimeSpan.FromSeconds(30)
		);
		if (!ffOk) return Results.Problem("ffmpeg failed");

		// ---- 1) Azure OpenAI STT (transcribe) ----
		var sttClient = httpFactory.CreateClient();
		sttClient.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiKey);
		// Many Azure setups also accept "api-key", so we set both for safety:
		sttClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", openAiKey);

		using var sttForm = new MultipartFormDataContent();
		var audioBytes = await File.ReadAllBytesAsync(wavPath);
		var audioContent = new ByteArrayContent(audioBytes);
		audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
		sttForm.Add(audioContent, "file", "audio.wav");
		// Model is typically optional with "deployments/{name}" URLs, but harmless to include:
		sttForm.Add(new StringContent("gpt-4o-mini-transcribe"), "model");
		if (!string.IsNullOrWhiteSpace(sourceLang) && !sourceLang.Equals("auto", StringComparison.OrdinalIgnoreCase))
			sttForm.Add(new StringContent(sourceLang), "language"); // if the endpoint supports language hint

		var sttRes = await sttClient.PostAsync(transcribeUrl, sttForm);
		if (!sttRes.IsSuccessStatusCode)
			return Results.Problem($"openai transcribe error: {await sttRes.Content.ReadAsStringAsync()}");

		using var sttDoc = JsonDocument.Parse(await sttRes.Content.ReadAsStringAsync());
		var transcript =
			sttDoc.RootElement.TryGetProperty("text", out var textProp) ? (textProp.GetString() ?? "") : "";

		// ---- 2) Azure OpenAI translate (chat completions) ----
		var langnames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		{ "en", "english" },
		{ "no", "norwegian" },
		{ "es", "spanish" },
		{ "uk", "ukrainian" },
	};
		if (!langnames.TryGetValue(targetLang, out var langName))
			langName = targetLang; // fallback to code

		var sys = $"You are a translation engine. Translate the user's message into {langName}. " +
				  $"If the message is already in {langName}, return it unchanged. Keep meaning and tone. No commentary.";
		if (!string.IsNullOrEmpty(prompt)) sys += $" Extra instructions: {prompt}";

		var translateClient = httpFactory.CreateClient();
		translateClient.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAiKey);
		translateClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", openAiKey);

		var chatPayload = new
		{
			messages = new object[]
			{
			new { role = "system", content = sys },
			new { role = "user", content = transcript },
			},
			temperature = 0.2,
			// optional with Azure deployments; include to match your sample
			model = "gpt-4.1-nano"
		};

		var chatRes = await translateClient.PostAsync(
			translateUrl,
			new StringContent(JsonSerializer.Serialize(chatPayload), Encoding.UTF8, "application/json")
		);
		if (!chatRes.IsSuccessStatusCode)
			return Results.Problem($"openai translate error: {await chatRes.Content.ReadAsStringAsync()}");

		using var chatDoc = JsonDocument.Parse(await chatRes.Content.ReadAsStringAsync());
		var translation = chatDoc.RootElement
			.GetProperty("choices")[0]
			.GetProperty("message").GetProperty("content").GetString() ?? "";

		// ---- 3) Azure OpenAI TTS ----
		var voiceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["en"] = "alloy",
			["no"] = "alloy", // replace with a preferred voice if available
			["es"] = "alloy",
			["uk"] = "alloy",
		};
		var voice = voiceMap.TryGetValue(targetLang, out var vv) ? vv : "alloy";

		var ttsClient = httpFactory.CreateClient();
		ttsClient.DefaultRequestHeaders.Authorization =
			new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ttsKey);
		ttsClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", ttsKey);
		ttsClient.DefaultRequestHeaders.Accept.TryParseAdd("audio/wav");

		var ttsPayload = new
		{
			model = "gpt-4o-mini-tts",
			input = translation,
			voice = voice,
			// Many implementations accept "format": "wav" (harmless if ignored)
			format = "wav"
		};

		var ttsRes = await ttsClient.PostAsync(
			ttsUrl,
			new StringContent(JsonSerializer.Serialize(ttsPayload), Encoding.UTF8, "application/json")
		);
		if (!ttsRes.IsSuccessStatusCode)
			return Results.Problem($"openai tts error: {await ttsRes.Content.ReadAsStringAsync()}");

		// Save WAV
		var ttsOut = Path.Combine(workDir, "tts.wav");
		await using (var fs = File.Create(ttsOut))
		{
			await (await ttsRes.Content.ReadAsStreamAsync()).CopyToAsync(fs);
		}

		// ---- 4) Persist & respond with URLs ----
		var id = Guid.NewGuid().ToString("N");
		var storeDir = Path.Combine(app.Environment.ContentRootPath, "data", "audio");
		Directory.CreateDirectory(storeDir);
		var finalPath = Path.Combine(storeDir, $"{id}.wav");
		File.Move(ttsOut, finalPath, overwrite: true);

		// clean up temp input
		try { Directory.Delete(workDir, recursive: true); } catch { /* ignore */ }

		return Results.Json(new
		{
			transcript,
			translation,
			audioUrl = $"/api/translate/audio/{id}"
		});
	});

	app.MapGet("/api/translate/audio/{id}", (string id) =>
	{
		var path = Path.Combine(app.Environment.ContentRootPath, "data", "audio", $"{id}.wav");
		if (!System.IO.File.Exists(path)) return Results.NotFound();
		var provider = new FileExtensionContentTypeProvider();
		provider.TryGetContentType(path, out var ct);
		ct ??= "audio/wav";
		var bytes = System.IO.File.ReadAllBytes(path);
		return Results.File(bytes, ct);
	});
}
else
{
	app.MapPost("/api/translate", async (HttpRequest req, IHttpClientFactory httpFactory) =>
	{
		var form = await req.ReadFormAsync();
		var file = form.Files["audio"];
		if (file is null || file.Length == 0) return Results.BadRequest("Missing audio");

		var sourceLang = form["sourceLang"].ToString();
		var targetLang = form["targetLang"].ToString();
		var prompt = form["prompt"].ToString();

		var workDir = Path.Combine(Path.GetTempPath(), "stt", Guid.NewGuid().ToString());
		Directory.CreateDirectory(workDir);
		var rawPath = Path.Combine(workDir, file.FileName);
		using (var fs = File.Create(rawPath)) await file.CopyToAsync(fs);
		var wavPath = Path.Combine(workDir, "audio.wav");

		// 1) transcode to 16 kHz mono PCM WAV
		var ffOk = await Run(
			ffmpegPath,
			$"-y -i \"{rawPath}\" -ac 1 -ar 16000 -vn \"{wavPath}\"",
			timeout: TimeSpan.FromSeconds(30)
		);
		if (!ffOk) return Results.Problem("ffmpeg failed");

		// 2) whisper → transcript
		var whisperClient = httpFactory.CreateClient("whisper");
		using var mp = new MultipartFormDataContent();
		mp.Add(new StreamContent(File.OpenRead(wavPath)), "audio", "audio.wav");
		if (!string.IsNullOrWhiteSpace(sourceLang)) mp.Add(new StringContent(sourceLang), "sourceLang");

		var wres = await whisperClient.PostAsync("transcribe", mp);
		if (!wres.IsSuccessStatusCode)
			return Results.Problem($"whisper error: {await wres.Content.ReadAsStringAsync()}");

		var wJson = JsonDocument.Parse(await wres.Content.ReadAsStringAsync()).RootElement;
		var transcript = wJson.GetProperty("text").GetString() ?? "";

		var langnames = new Dictionary<string, string>()
	{
		{ "en", "english" },
		{ "no", "norwegian" },
		{ "es", "spanish" },
		{ "uk", "ukrainian" }
	};

		var langName = langnames[targetLang];

		// 3) llama.cpp → translation (OpenAI-compatible /v1/chat/completions)
		var llamaClient = httpFactory.CreateClient("llama");
		var sys = $"You are a translation engine. Translate the user's message into {langName}. If the message is already in {langName}, just return the message as it is." +
				  $"Keep meaning and tone. Do not add commentary.";
		if (!string.IsNullOrEmpty(prompt)) sys += $" Extra instructions: {prompt}";

		var payload = new
		{
			model = Environment.GetEnvironmentVariable("LLAMA_MODEL") ?? "local",
			messages = new object[] {
			new { role = "system", content = sys },
			new { role = "user", content = transcript }
		},
			temperature = 0.2
		};
		var lres = await llamaClient.PostAsync(
			"v1/chat/completions",
			new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
		);
		if (!lres.IsSuccessStatusCode)
			return Results.Problem($"llama error: {await lres.Content.ReadAsStringAsync()}");

		using var ldoc = JsonDocument.Parse(await lres.Content.ReadAsStringAsync());
		var translation = ldoc.RootElement.GetProperty("choices")[0]
			.GetProperty("message").GetProperty("content").GetString() ?? "";

		// 4) Piper TTS → WAV (HTTP with voice per language)
		var ttsOut = Path.Combine(workDir, "tts.wav");
		var piperBaseUrl = Environment.GetEnvironmentVariable("PIPER_URL") ?? "http://localhost:5002/";
		var piperClient = httpFactory.CreateClient("piper");

		// map target language → piper voice id (fill in what you actually want)
		var voiceMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["en"] = "en_US-libritts-high",
			["no"] = "no_NO-talesyntese-medium",   // replace with your preferred NB voice id
			["es"] = "es_ES-carlfm-x_low",         // example; pick your actual Spanish voice
			["uk"] = "uk_UA-lada-x_low",
			// add more as needed; fallbacks below handle unknowns
		};

		var targetVoice = voiceMap.TryGetValue(targetLang, out var v)
			? v
			: Environment.GetEnvironmentVariable("PIPER_DEFAULT_VOICE") ?? "en_US-libritts-high";

		var piperPayload = new
		{
			text = translation,
			voice = targetVoice
		};

		using var piperReq = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(piperBaseUrl), "tts"))
		{
			Content = new StringContent(
				System.Text.Json.JsonSerializer.Serialize(piperPayload),
				Encoding.UTF8,
				"application/json")
		};

		using var resp = await piperClient.SendAsync(piperReq, HttpCompletionOption.ResponseHeadersRead);
		if (!resp.IsSuccessStatusCode)
		{
			var err = await resp.Content.ReadAsStringAsync();
			return Results.Problem($"piper http error: {(int)resp.StatusCode} {resp.ReasonPhrase} - {err}");
		}

		await using (var fs = File.Create(ttsOut))
		{
			await resp.Content.CopyToAsync(fs);
		}


		// 5) Persist & respond with URLs
		var id = Guid.NewGuid().ToString("N");
		var storeDir = Path.Combine(app.Environment.ContentRootPath, "data", "audio");
		Directory.CreateDirectory(storeDir);
		var finalPath = Path.Combine(storeDir, $"{id}.wav");
		File.Move(ttsOut, finalPath);

		return Results.Json(new
		{
			transcript,
			translation,
			audioUrl = $"/api/translate/audio/{id}"
		});
	});

	app.MapGet("/api/translate/audio/{id}", (string id) =>
	{
		var path = Path.Combine(app.Environment.ContentRootPath, "data", "audio", $"{id}.wav");
		if (!System.IO.File.Exists(path)) return Results.NotFound();
		var provider = new FileExtensionContentTypeProvider();
		provider.TryGetContentType(path, out var ct);
		ct ??= "audio/wav";
		var bytes = System.IO.File.ReadAllBytes(path);
		return Results.File(bytes, ct);
	});
}

app.Run();

static async Task<bool> Run(string fileName, string args, TimeSpan? timeout = null)
{
	var psi = new ProcessStartInfo
	{
		FileName = fileName,
		Arguments = args,
		RedirectStandardOutput = true,
		RedirectStandardError = true,
		UseShellExecute = false
	};
	using var p = Process.Start(psi)!;
	using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(60));
	var tcs = new TaskCompletionSource<bool>();
	_ = Task.Run(async () =>
	{
		try { await p.WaitForExitAsync(cts.Token); tcs.TrySetResult(p.ExitCode == 0); }
		catch { try { if (!p.HasExited) p.Kill(true); } catch { } tcs.TrySetResult(false); }
	});
	return await tcs.Task;
}
