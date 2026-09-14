using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sms.Contracts.Transport
{
    public record ApiResponse(
     string Command,
     bool Success,
     string ErrorMessage,
     JsonElement? Data);
}
