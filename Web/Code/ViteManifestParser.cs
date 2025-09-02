using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Web.Code;

public class ViteManifestParser
{
	private readonly string manifestJson;

	public ViteManifestParser(string manifestJson)
	{
		this.manifestJson = manifestJson;
	}

	public static ViteManifestParser FromFile(string filePath)
	{
		var fileInfo = new FileInfo(filePath);
		if (fileInfo.Exists == false)
		{
			throw new FileNotFoundException($"File not found: {fileInfo.FullName}");
		}
		var manifestJson = File.ReadAllText(filePath);
		return new ViteManifestParser(manifestJson);
	}

	public static async Task<ViteManifestParser> FromFileAsync(string filePath)
	{
		var fileInfo = new FileInfo(filePath);
		if (fileInfo.Exists == false)
		{
			throw new FileNotFoundException($"File not found: {fileInfo.FullName}");
		}
		var manifestJson = await File.ReadAllTextAsync(filePath);
		return new ViteManifestParser(manifestJson);
	}

	public string? GetScript(string entrypointName)
	{
		var jObject = JsonConvert.DeserializeObject<JObject>(manifestJson) ?? throw new Exception("Could not parse manifest");
		var result = jObject[entrypointName]?["file"]?.Value<string>();
		return result;
	}

	public List<string> GetStyles(string entrypointName)
	{
		var jObject = JsonConvert.DeserializeObject<JObject>(manifestJson) ?? throw new Exception("Could not parse manifest");
		var results = jObject[entrypointName]?["css"]?.Values<string>();
		return results?.Where(str => !string.IsNullOrWhiteSpace(str)).Select(str => str!).ToList() ?? new();
	}
}
