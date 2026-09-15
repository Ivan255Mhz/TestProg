using Grpc.Core;
using Grpc.Net.Client;
using Sms.Client.Interfaces;
using Sms.Client.Options;
using Sms.Contracts.Models;
using GeneratedMenuItem = Sms.Test.MenuItem;
using GeneratedOrder = Sms.Test.Order;
using GeneratedOrderItem = Sms.Test.OrderItem;
using GeneratedBoolValue = Google.Protobuf.WellKnownTypes.BoolValue;

namespace Sms.Client.Grpc;

public sealed class GrpcSmsClient : ISmsClient
{
    private readonly Sms.Test.SmsTestService.SmsTestServiceClient _client;
    private readonly GrpcChannel _channel;
    private readonly SmsClientOptions _options;

    public GrpcSmsClient(GrpcChannel channel, SmsClientOptions options)
    {
        _channel = channel;
        _options = options;
        _client = new Sms.Test.SmsTestService.SmsTestServiceClient(channel);
    }

    public async Task<IReadOnlyList<MenuItem>> GetMenuAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetMenuAsync(
                new GeneratedBoolValue { Value = true },
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);

            if (!response.Success)
            {
                throw new SmsException(
                    NonEmpty(response.ErrorMessage) ?? "Server rejected GetMenu request");
            }

            return response.MenuItems.Select(Map).ToList();
        }
        catch (RpcException ex) when (IsUserCancellation(ex, cancellationToken))
        {
            throw new OperationCanceledException(ex.Status.ToString(), ex, cancellationToken);
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
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);

            if (response.Success)
            {
                return new OrderResult(Success: true, ErrorMessage: null);
            }

            return new OrderResult(
                Success: false,
                ErrorMessage: NonEmpty(response.ErrorMessage) ?? "Server rejected the order");
        }
        catch (RpcException ex) when (IsUserCancellation(ex, cancellationToken))
        {
            throw new OperationCanceledException(ex.Status.ToString(), ex, cancellationToken);
        }
        catch (RpcException ex)
        {
            throw new SmsException($"gRPC error: {ex.Status}", ex);
        }
    }

    private DateTime? CreateDeadline()
    {
        return _options.TimeoutSeconds > 0
            ? DateTime.UtcNow.AddSeconds(_options.TimeoutSeconds)
            : null;
    }

    private static bool IsUserCancellation(RpcException ex, CancellationToken cancellationToken) =>
        ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested;

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

    public void Dispose()
    {
        _channel.Dispose();
    }
}
