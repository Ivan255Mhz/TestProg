using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sms.Contracts.Transport
{
    public record ApiResponse(bool Success, string? ErrorMessage, object? Data);
}
