using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sms.Client.Interfaces;
using Sms.Client.Options;
using Sms.Contracts.Models;
using Sms.Contracts.Transport;

namespace Sms.Client.Http;

public sealed class HttpSmsClient : ISmsClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly SmsClientOptions _options;

    public HttpSmsClient(HttpClient httpClient, SmsClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<MenuItem>> GetMenuAsync(
    CancellationToken cancellationToken = default)
    {
        var envelope = await SendEnvelopeAsync(
            new ApiRequest("GetMenu", new { }),
            cancellationToken);

        if (!envelope.Success)
        {
            throw new SmsException(
                envelope.ErrorMessage ?? "Server rejected GetMenu request");
        }

        var menu = DeserializeData<List<MenuItem>>(envelope.Data);
        return menu;
    }

    public async Task<OrderResult> SendOrderAsync(
    Order order,
    CancellationToken cancellationToken = default)
    {
        var envelope = await SendEnvelopeAsync(
            new ApiRequest("SendOrder", order),
            cancellationToken);

        if (envelope.Success)
        {
            return new OrderResult(
                Success: true,
                ErrorMessage: null,
                OrderNumber: ReadOrderNumber(envelope.Data));
        }

        return new OrderResult(
            Success: false,
            ErrorMessage: envelope.ErrorMessage ?? "Server rejected the order",
            OrderNumber: null);
    }

    private async Task<ApiResponse> SendEnvelopeAsync(
    ApiRequest request,
    CancellationToken cancellationToken)
    {
        var url = _options.BaseUrl.TrimEnd('/') + "/" + _options.Endpoint.TrimStart('/');

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var json = JsonSerializer.Serialize(request, JsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.Username}:{_options.Password}"));
        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new SmsException("Network error while calling SMS server", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SmsException("Request timed out", ex);
        }

        using (httpResponse)
        {
            if (!httpResponse.IsSuccessStatusCode)
            {
                throw new SmsException(
                    $"Server returned HTTP {(int)httpResponse.StatusCode} " +
                    $"({httpResponse.StatusCode})");
            }

            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            ApiResponse? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiResponse>(body, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new SmsException("Server returned invalid JSON", ex);
            }

            if (envelope is null)
            {
                throw new SmsException("Server returned an empty response body");
            }

            return envelope;
        }
    }

    private T DeserializeData<T>(object? data) where T : class
    {
        if (data is not JsonElement element)
        {
            throw new SmsException(
                $"Server response does not contain Data for {typeof(T).Name}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions)
                   ?? throw new SmsException($"Server Data is null for {typeof(T).Name}");
        }
        catch (JsonException ex)
        {
            throw new SmsException($"Server Data has unexpected format for {typeof(T).Name}", ex);
        }
    }

    private static string? ReadOrderNumber(object? data)
    {
        if (data is JsonElement { ValueKind: JsonValueKind.String } element)
        {
            return element.GetString();
        }
        return null;
    }

}