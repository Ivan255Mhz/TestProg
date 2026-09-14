using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sms.Client
{
    public record OrderResult(bool Success,string? ErrorMessage,string? OrderNumber);
}
