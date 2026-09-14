using System.Net;
using System.Text;
using System.Text.Json;
using Sms.Client.Http;
using Sms.Client.Options;
using Sms.Contracts.Models;

namespace Sms.Client.Tests;

public sealed class HttpSmsClientTests
{
    private static SmsClientOptions CreateOptions() => new()
    {
        BaseUrl = "http://test-server",
        Endpoint = "/api/sms",
        Username = "user",
        Password = "pass",
    };

    private static HttpSmsClient CreateClient(TestHttpMessageHandler handler) =>
        new(new HttpClient(handler), CreateOptions());

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static string Base64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task GetMenu_SuccessfulResponse_ReturnsMenu()
    {
        var json = """
        {
            "success": true,
            "errorMessage": null,
            "data": [
                { "code": "P001", "name": "Пицца", "price": 550.00 },
                { "code": "B001", "name": "Бургер", "price": 420.50 }
            ]
        }
        """;

        var handler = new TestHttpMessageHandler(_ => JsonResponse(json));
        var client = CreateClient(handler);

        var menu = await client.GetMenuAsync();

        Assert.Equal(2, menu.Count);
        Assert.Equal("P001", menu[0].Code);
        Assert.Equal("Пицца", menu[0].Name);
        Assert.Equal(550.00m, menu[0].Price);
        Assert.Equal(420.50m, menu[1].Price);
    }

    [Fact]
    public async Task GetMenu_Request_HasCorrectUrlMethodAndAuth()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"success": true, "data": []}"""));
        var client = CreateClient(handler);

        await client.GetMenuAsync();

        var request = handler.LastRequest!;

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://test-server/api/sms", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(Base64("user:pass"), request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetMenu_Request_BodyContainsCommand()
    {
        string? sentBody = null;
        var handler = new TestHttpMessageHandler(request =>
        {
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse("""{"success": true, "data": []}""");
        });
        var client = CreateClient(handler);

        await client.GetMenuAsync();

        Assert.NotNull(sentBody);
        using var doc = JsonDocument.Parse(sentBody!);
        Assert.Equal("GetMenu", doc.RootElement.GetProperty("command").GetString());
        Assert.True(doc.RootElement.TryGetProperty("commandParameters", out _));
    }

    [Fact]
    public async Task GetMenu_ServerRejects_ThrowsSmsException()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"success": false, "errorMessage": "Access denied", "data": null}"""));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SmsException>(() => client.GetMenuAsync());

        Assert.Contains("Access denied", ex.Message);
    }

    [Fact]
    public async Task GetMenu_Http401_ThrowsSmsException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<SmsException>(() => client.GetMenuAsync());

        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public async Task GetMenu_Http500_ThrowsSmsException()
    {
        var handler = new TestHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SmsException>(() => client.GetMenuAsync());
    }

    [Fact]
    public async Task GetMenu_InvalidJson_ThrowsSmsException()
    {
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("this is not json", Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SmsException>(() => client.GetMenuAsync());
    }

    [Fact]
    public async Task GetMenu_MissingData_ThrowsSmsException()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"success": true}"""));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SmsException>(() => client.GetMenuAsync());
    }

    [Fact]
    public async Task SendOrder_SuccessfulResponse_ReturnsSuccessResult()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"success": true, "data": "ORD-12345"}"""));
        var client = CreateClient(handler);
        var order = new Order(new[] { new OrderItem("P001", 2) });

        var result = await client.SendOrderAsync(order);

        Assert.True(result.Success);
        Assert.Equal("ORD-12345", result.OrderNumber);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendOrder_ServerRejects_ReturnsFailureResult()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"success": false, "errorMessage": "Out of stock"}"""));
        var client = CreateClient(handler);
        var order = new Order(new[] { new OrderItem("P001", 100) });

        var result = await client.SendOrderAsync(order);

        Assert.False(result.Success);
        Assert.Equal("Out of stock", result.ErrorMessage);
    }

    [Fact]
    public async Task SendOrder_Request_BodyContainsOrder()
    {
        string? sentBody = null;
        var handler = new TestHttpMessageHandler(request =>
        {
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse("""{"success": true}""");
        });
        var client = CreateClient(handler);
        var order = new Order(new[] { new OrderItem("P001", 2), new OrderItem("B001", 1) });

        await client.SendOrderAsync(order);

        using var doc = JsonDocument.Parse(sentBody!);
        var parameters = doc.RootElement.GetProperty("commandParameters");
        var firstItem = parameters.GetProperty("items")[0];
        Assert.Equal("P001", firstItem.GetProperty("menuCode").GetString());
        Assert.Equal(2, firstItem.GetProperty("quantity").GetInt32());
    }
}

