using System.Text.Json;

namespace TcpTool;

public static class DeviceStore
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "devices.json");

    public static List<DeviceDefinition> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<DeviceDefinition>();
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<DeviceDefinition>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            }) ?? new List<DeviceDefinition>();
            // Migrate legacy flag
            foreach (var d in list)
            {
                if (d.PayloadFormat == 0 && d.InputIsHexDump)
                    d.PayloadFormat = PayloadFormat.HexDump;
            }
            return list;
        }
        catch { return new List<DeviceDefinition>(); }
    }

    public static void Save(List<DeviceDefinition> defs)
    {
        try
        {
            var json = JsonSerializer.Serialize(defs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { }
    }
}
