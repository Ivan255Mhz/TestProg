using System.Globalization;
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
        new()
        {
            PropertyNameCaseInsensitive = true,
        };

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
            new ApiRequest("GetMenu", new GetMenuParameters(WithPrice: true)),
            cancellationToken);

        if (!envelope.Success)
        {
            throw new SmsException(
                NonEmpty(envelope.ErrorMessage) ?? "Server rejected GetMenu request");
        }

        var data = DeserializeData<MenuData>(envelope.Data);
        return data.MenuItems
            ?? throw new SmsException("Server response does not contain menu items");
    }

    public async Task<OrderResult> SendOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var items = order.Items
            .Select(i => new SendOrderItem(
                Id: i.Id,
                Quantity: i.Quantity.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        var parameters = new SendOrderParameters(OrderId: order.Id, MenuItems: items);

        var envelope = await SendEnvelopeAsync(
            new ApiRequest("SendOrder", parameters),
            cancellationToken);

        if (envelope.Success)
        {
            return new OrderResult(Success: true, ErrorMessage: null);
        }

        return new OrderResult(
            Success: false,
            ErrorMessage: NonEmpty(envelope.ErrorMessage) ?? "Server rejected the order");
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

    private T DeserializeData<T>(JsonElement? data) where T : class
    {
        if (data is not { ValueKind: JsonValueKind.Object } element)
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
            throw new SmsException(
                $"Server Data has unexpected format for {typeof(T).Name}", ex);
        }
    }

    private static string? NonEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Dispose()
    {
    }
}
