using System.Net.Http.Json;

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

	private static Uri BuildUri(string remoteUrl, string relativePath)
	{
		string baseUrl = remoteUrl.Trim().TrimEnd('/');
		string path = relativePath.TrimStart('/');
		return new Uri($"{baseUrl}/{path}", UriKind.Absolute);
	}
}
