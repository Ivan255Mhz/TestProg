using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sms.Contracts.Transport
{
    public record GetMenuParameters(bool WithPrice);

    public record SendOrderParameters(
        string OrderId,
        IReadOnlyList<SendOrderItem> MenuItems);

    public record SendOrderItem(string Id, string Quantity);
}
