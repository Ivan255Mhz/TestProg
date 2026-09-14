using Grpc.Net.Client;
using Sms.Client.Grpc;
using Sms.Client.Http;
using Sms.Client.Interfaces;
using Sms.Client.Options;
using System.Net.Http;

namespace Sms.Client.Factories;

public sealed class SmsClientFactory
{
    private readonly IHttpClientFactory? _httpClientFactory;

    public SmsClientFactory(IHttpClientFactory? httpClientFactory = null)
    {
        _httpClientFactory = httpClientFactory;
    }

    public ISmsClient Create(SmsClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.TransportType switch
        {
            "Http" => CreateHttp(options),
            "Grpc" => CreateGrpc(options),
            _ => throw new SmsException(
                $"Unsupported TransportType '{options.TransportType}'. " +
                "Supported values: Http, Grpc"),
        };
    }

    private HttpSmsClient CreateHttp(SmsClientOptions options)
    {
        var httpClient = _httpClientFactory?.CreateClient("SmsClient")
            ?? new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        return new HttpSmsClient(httpClient, options);
    }

    private GrpcSmsClient CreateGrpc(SmsClientOptions options)
    {
        var channel = GrpcChannel.ForAddress(options.BaseUrl, new GrpcChannelOptions
        {
            DisposeHttpClient = true,
        });
        return new GrpcSmsClient(channel);
    }
}