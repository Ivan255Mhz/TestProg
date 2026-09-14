using Sms.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sms.Contracts.Transport
{
    public record MenuData(IReadOnlyList<MenuItem> MenuItems);
}
