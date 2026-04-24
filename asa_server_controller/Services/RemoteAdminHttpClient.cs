using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace asa_server_controller.Services;

public sealed class RemoteAdminHttpClient(HttpClient httpClient)
{
	private const string ApiKeyHeaderName = "X-Api-Key";

	public async Task<TResponse?> GetFromJsonAsync<TResponse>(
			string remoteUrl,
			string relativePath,
			string? apiKey = null,
			CancellationToken cancellationToken = default)
	{
		using HttpRequestMessage message = new(HttpMethod.Get, BuildUri(remoteUrl, relativePath));

		if (!string.IsNullOrWhiteSpace(apiKey))
		{
			message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);
		}

		using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
	}

	public async Task<TResponse?> PostAsync<TResponse>(
			string remoteUrl,
			string relativePath,
			string apiKey,
			CancellationToken cancellationToken = default)
	{
		using HttpRequestMessage message = new(HttpMethod.Post, BuildUri(remoteUrl, relativePath));
		message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);

		using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
	}

	public async Task<TResponse?> PostAsJsonAsync<TRequest, TResponse>(
			string remoteUrl,
			string relativePath,
			string apiKey,
			TRequest request,
			CancellationToken cancellationToken = default)
	{
		using HttpRequestMessage message = new(HttpMethod.Post, BuildUri(remoteUrl, relativePath))
		{
			Content = JsonContent.Create(request)
		};

		message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);

		using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
	}

	public async Task<TResponse?> PatchAsJsonAsync<TRequest, TResponse>(
			string remoteUrl,
			string relativePath,
			string apiKey,
			TRequest request,
			CancellationToken cancellationToken = default)
	{
		using HttpRequestMessage message = new(HttpMethod.Patch, BuildUri(remoteUrl, relativePath))
		{
			Content = JsonContent.Create(request)
		};

		message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);

		using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
	}

	public async Task<RemoteApiResult<TResponse>> GetResultAsync<TResponse>(
			string remoteUrl,
			string relativePath,
			string? apiKey = null,
			CancellationToken cancellationToken = default)
	{
		using HttpRequestMessage message = new(HttpMethod.Get, BuildUri(remoteUrl, relativePath));

		if (!string.IsNullOrWhiteSpace(apiKey))
		{
			message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);
		}

		using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
		return await ReadResultAsync<TResponse>(response, cancellationToken);
	}

	public async Task<RemoteApiResult<TResponse>> PostFileAsync<TResponse>(
			string remoteUrl,
			string relativePath,
			string apiKey,
			string fileName,
			string content,
			CancellationToken cancellationToken = default)
	{
		using MultipartFormDataContent form = new();
		using StringContent fileContent = new(content, Encoding.UTF8, "text/plain");
		form.Add(fileContent, "File", fileName);

		using HttpRequestMessage message = new(HttpMethod.Post, BuildUri(remoteUrl, relativePath))
		{
			Content = form
		};

		message.Headers.TryAddWithoutValidation(ApiKeyHeaderName, apiKey);

		using HttpResponseMessage response = await httpClient.SendAsync(message, cancellationToken);
		return await ReadResultAsync<TResponse>(response, cancellationToken);
	}

	private static Uri BuildUri(string remoteUrl, string relativePath)
	{
		string baseUrl = remoteUrl.Trim().TrimEnd('/');
		string path = relativePath.TrimStart('/');
		return new Uri($"{baseUrl}/{path}", UriKind.Absolute);
	}

	private static async Task<RemoteApiResult<TResponse>> ReadResultAsync<TResponse>(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		string? responseText = response.Content is null
			? null
			: await response.Content.ReadAsStringAsync(cancellationToken);

		if (response.IsSuccessStatusCode)
		{
			if (string.IsNullOrWhiteSpace(responseText))
			{
				return new RemoteApiResult<TResponse>(true, default, null);
			}

			TResponse? data = JsonSerializer.Deserialize<TResponse>(responseText, new JsonSerializerOptions(JsonSerializerDefaults.Web));
			return new RemoteApiResult<TResponse>(true, data, null);
		}

		return new RemoteApiResult<TResponse>(false, default, ExtractErrorMessage(responseText));
	}

	private static string ExtractErrorMessage(string? responseText)
	{
		if (string.IsNullOrWhiteSpace(responseText))
		{
			return "Remote server request failed.";
		}

		try
		{
			using JsonDocument doc = JsonDocument.Parse(responseText);
			if (doc.RootElement.TryGetProperty("message", out JsonElement messageElement))
			{
				return messageElement.GetString() ?? responseText;
			}
		}
		catch (JsonException)
		{
		}

		return responseText;
	}
}
