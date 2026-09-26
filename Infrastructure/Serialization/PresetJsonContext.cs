using System.Collections.Generic;
using System.Text.Json.Serialization;
using EasyDNS.Core.Models;

namespace EasyDNS.Infrastructure.Serialization
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(List<DnsPreset>))]
    [JsonSerializable(typeof(DnsPreset))]
    public partial class PresetJsonContext : JsonSerializerContext
    {
    }
}
