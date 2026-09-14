using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sms.Contracts.Models
{
    public record Order(IReadOnlyList<OrderItem> Items);
}
