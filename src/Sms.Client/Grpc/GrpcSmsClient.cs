using Grpc.Core;
using Grpc.Net.Client;
using Sms.Client.Interfaces;
using Sms.Contracts.Models;
using GeneratedMenuItem = Sms.Test.MenuItem;
using GeneratedOrder = Sms.Test.Order;
using GeneratedOrderItem = Sms.Test.OrderItem;
using GeneratedBoolValue = Google.Protobuf.WellKnownTypes.BoolValue;

namespace Sms.Client.Grpc;

public sealed class GrpcSmsClient : ISmsClient
{
    private readonly Sms.Test.SmsTestService.SmsTestServiceClient _client;

    public GrpcSmsClient(GrpcChannel channel)
    {
        _client = new Sms.Test.SmsTestService.SmsTestServiceClient(channel);
    }

    public async Task<IReadOnlyList<MenuItem>> GetMenuAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetMenuAsync(
                new GeneratedBoolValue { Value = true },
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                throw new SmsException(
                    NonEmpty(response.ErrorMessage) ?? "Server rejected GetMenu request");
            }

            return response.MenuItems.Select(Map).ToList();
        }
        catch (RpcException ex)
        {
            throw new SmsException($"gRPC error: {ex.Status}", ex);
        }
    }

    public async Task<OrderResult> SendOrderAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        var request = new GeneratedOrder
        {
            Id = order.Id,
            OrderItems = {
                order.Items.Select(i => new GeneratedOrderItem
                {
                    Id = i.Id,
                    Quantity = (double)i.Quantity,
                }),
            },
        };

        try
        {
            var response = await _client.SendOrderAsync(
                request,
                cancellationToken: cancellationToken);

            if (response.Success)
            {
                return new OrderResult(Success: true, ErrorMessage: null);
            }

            return new OrderResult(
                Success: false,
                ErrorMessage: NonEmpty(response.ErrorMessage) ?? "Server rejected the order");
        }
        catch (RpcException ex)
        {
            throw new SmsException($"gRPC error: {ex.Status}", ex);
        }
    }

    private static MenuItem Map(GeneratedMenuItem item) => new(
        Id: item.Id,
        Article: item.Article,
        Name: item.Name,
        Price: Convert.ToDecimal(item.Price),
        IsWeighted: item.IsWeighted,
        FullPath: item.FullPath,
        Barcodes: item.Barcodes.ToList());

    private static string? NonEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
