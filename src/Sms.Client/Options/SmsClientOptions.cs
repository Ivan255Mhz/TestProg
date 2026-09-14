namespace Sms.Client.Options;

public sealed class SmsClientOptions
{
    public const string SectionName = "SmsClient";

    public string BaseUrl { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public string TransportType { get; set; } = "Http";
}