using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sms.Contracts.Models
{
    public record MenuItem(
    string Id,
    string Article,
    string Name,
    decimal Price,
    bool IsWeighted,
    string FullPath,
    IReadOnlyList<string> Barcodes);

}
