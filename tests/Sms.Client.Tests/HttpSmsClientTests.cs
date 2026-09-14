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
            "Command": "GetMenu",
            "Success": true,
            "ErrorMessage": "",
            "Data": {
                "MenuItems": [
                    {
                        "Id": "5979224",
                        "Article": "A1004292",
                        "Name": "Каша гречневая",
                        "Price": 50,
                        "IsWeighted": false,
                        "FullPath": "ПРОИЗВОДСТВО\\Гарниры",
                        "Barcodes": ["57890975627974236429"]
                    },
                    {
                        "Id": "9084246",
                        "Article": "A1004293",
                        "Name": "Конфеты Коровка",
                        "Price": 300,
                        "IsWeighted": true,
                        "FullPath": "ДЕСЕРТЫ\\Развес",
                        "Barcodes": []
                    }
                ]
            }
        }
        """;

        var handler = new TestHttpMessageHandler(_ => JsonResponse(json));
        var client = CreateClient(handler);

        var menu = await client.GetMenuAsync();

        Assert.Equal(2, menu.Count);
        Assert.Equal("5979224", menu[0].Id);
        Assert.Equal("A1004292", menu[0].Article);
        Assert.Equal("Каша гречневая", menu[0].Name);
        Assert.Equal(50m, menu[0].Price);
        Assert.False(menu[0].IsWeighted);
        Assert.Equal("ПРОИЗВОДСТВО\\Гарниры", menu[0].FullPath);
        Assert.Single(menu[0].Barcodes);
        Assert.Equal(300m, menu[1].Price);
        Assert.True(menu[1].IsWeighted);
        Assert.Empty(menu[1].Barcodes);
    }

    [Fact]
    public async Task GetMenu_Request_HasCorrectUrlMethodAndAuth()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"Command": "GetMenu", "Success": true, "ErrorMessage": "", "Data": {"MenuItems": []}}"""));
        var client = CreateClient(handler);

        await client.GetMenuAsync();

        var request = handler.LastRequest!;

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://test-server/api/sms", request.RequestUri!.ToString());
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        Assert.Equal(Base64("user:pass"), request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task GetMenu_Request_BodyContainsCommandAndWithPrice()
    {
        string? sentBody = null;
        var handler = new TestHttpMessageHandler(request =>
        {
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(
                """{"Command": "GetMenu", "Success": true, "ErrorMessage": "", "Data": {"MenuItems": []}}""");
        });
        var client = CreateClient(handler);

        await client.GetMenuAsync();

        Assert.NotNull(sentBody);
        using var doc = JsonDocument.Parse(sentBody!);
        Assert.Equal("GetMenu", doc.RootElement.GetProperty("Command").GetString());
        var parameters = doc.RootElement.GetProperty("CommandParameters");
        Assert.True(parameters.GetProperty("WithPrice").GetBoolean());
    }

    [Fact]
    public async Task GetMenu_ServerRejects_ThrowsSmsException()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"Command": "GetMenu", "Success": false, "ErrorMessage": "Access denied"}"""));
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
            """{"Command": "GetMenu", "Success": true, "ErrorMessage": ""}"""));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<SmsException>(() => client.GetMenuAsync());
    }

    [Fact]
    public async Task SendOrder_SuccessfulResponse_ReturnsSuccessResult()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"Command": "SendOrder", "Success": true, "ErrorMessage": ""}"""));
        var client = CreateClient(handler);
        var order = new Order(
            "62137983-1117-4D10-87C1-EF40A4348250",
            new[] { new OrderItem("5979224", 1m) });

        var result = await client.SendOrderAsync(order);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task SendOrder_ServerRejects_ReturnsFailureResult()
    {
        var handler = new TestHttpMessageHandler(_ => JsonResponse(
            """{"Command": "SendOrder", "Success": false, "ErrorMessage": "Out of stock"}"""));
        var client = CreateClient(handler);
        var order = new Order(
            "62137983-1117-4D10-87C1-EF40A4348250",
            new[] { new OrderItem("9084246", 100m) });

        var result = await client.SendOrderAsync(order);

        Assert.False(result.Success);
        Assert.Equal("Out of stock", result.ErrorMessage);
    }

    [Fact]
    public async Task SendOrder_Request_BodyContainsOrderIdAndStringQuantities()
    {
        string? sentBody = null;
        var handler = new TestHttpMessageHandler(request =>
        {
            sentBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonResponse(
                """{"Command": "SendOrder", "Success": true, "ErrorMessage": ""}""");
        });
        var client = CreateClient(handler);
        var order = new Order(
            "62137983-1117-4D10-87C1-EF40A4348250",
            new[]
            {
                new OrderItem("5979224", 1m),
                new OrderItem("9084246", 0.408m),
            });

        await client.SendOrderAsync(order);

        using var doc = JsonDocument.Parse(sentBody!);
        var parameters = doc.RootElement.GetProperty("CommandParameters");
        Assert.Equal(
            "62137983-1117-4D10-87C1-EF40A4348250",
            parameters.GetProperty("OrderId").GetString());

        var items = parameters.GetProperty("MenuItems");
        Assert.Equal(2, items.GetArrayLength());

        Assert.Equal("5979224", items[0].GetProperty("Id").GetString());
        Assert.Equal("1", items[0].GetProperty("Quantity").GetString());

        Assert.Equal("9084246", items[1].GetProperty("Id").GetString());
        Assert.Equal("0.408", items[1].GetProperty("Quantity").GetString());
    }
}
