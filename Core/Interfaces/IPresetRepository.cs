using System.Collections.Generic;
using System.Threading.Tasks;
using EasyDNS.Core.Common;
using EasyDNS.Core.Models;

namespace EasyDNS.Core.Interfaces
{
    public interface IPresetRepository
    {
        IReadOnlyList<DnsPreset> GetAllPresets();
        IReadOnlyList<string> GetCategories();
        Task AddCustomPresetAsync(string name, string primaryDns, string secondaryDns, string description, string? primaryIpv6 = null, string? secondaryIpv6 = null);
        Task DeleteCustomPresetAsync(string presetId);
        Task<OperationResult> ExportCustomPresetsAsync(string filePath);
        Task<(OperationResult Result, int ImportedCount)> ImportCustomPresetsAsync(string filePath);
    }
}
