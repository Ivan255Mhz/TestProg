using Sms.Contracts.Models;

namespace Sms.Client.Interfaces;

public interface ISmsClient : IDisposable
{
    Task<IReadOnlyList<MenuItem>> GetMenuAsync(CancellationToken cancellationToken = default);

    Task<OrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default);
}
