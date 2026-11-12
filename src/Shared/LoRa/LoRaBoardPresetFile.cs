using System.Globalization;
using System.Text;
using ShortP2P.LoRa;

namespace TorgLink.LoRa;

/// <summary>
///     Board LoRaConfig snapshot on disk: <c>lora_presets/&lt;board&gt;_&lt;yyyyMMdd_HHmmss&gt;.lora_preset</c>,
///     body is <c>key:value</c> lines including duty cycle and the full config as base64.
/// </summary>
public sealed record LoRaBoardPresetFileData(
    string BoardName,
    string ComPort,
    DateTime SavedUtc,
    string ModemPresetName,
    int ModemPresetProto,
    string RegionCode,
    int RegionProto,
    int TxPowerDbm,
    bool OverrideDutyCycle,
    double DutyCyclePercent,
    byte[] LoraConfigBytes)
{
    public bool HasConfig => LoraConfigBytes.Length > 0;

    public LoRaBoardConfigInfo ToBoardInfo() => LoRaBoardConfigInfo.Parse(LoraConfigBytes);
}

/// <summary>Read/write <c>.lora_preset</c> files under an app-data <c>lora_presets</c> folder.</summary>
public static class LoRaBoardPresetFile
{
    public const string FolderName = "lora_presets";
    public const string Extension = ".lora_preset";

    public static string DirectoryFor(string appDataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appDataRoot);
        var dir = Path.Combine(appDataRoot, FolderName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string Write(string appDataRoot, LoRaBoardPresetFileData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.LoraConfigBytes.Length == 0)
            throw new ArgumentException("LoRaConfig blob is empty.", nameof(data));

        var dir = DirectoryFor(appDataRoot);
        var stamp = data.SavedUtc.ToUniversalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var safeBoard = SanitizeFileToken(data.BoardName);
        var path = Path.Combine(dir, $"{safeBoard}_{stamp}{Extension}");
        // Avoid overwrite collisions within the same second.
        if (File.Exists(path))
            path = Path.Combine(dir, $"{safeBoard}_{stamp}_{Guid.NewGuid():N}{Extension}");

        var sb = new StringBuilder();
        Append(sb, "board_name", data.BoardName);
        Append(sb, "com_port", data.ComPort);
        Append(sb, "saved_utc", data.SavedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
        Append(sb, "modem_preset", data.ModemPresetName);
        Append(sb, "modem_preset_proto", data.ModemPresetProto.ToString(CultureInfo.InvariantCulture));
        Append(sb, "region", data.RegionCode);
        Append(sb, "region_proto", data.RegionProto.ToString(CultureInfo.InvariantCulture));
        Append(sb, "tx_power_dbm", data.TxPowerDbm.ToString(CultureInfo.InvariantCulture));
        Append(sb, "override_duty_cycle", data.OverrideDutyCycle ? "true" : "false");
        Append(sb, "duty_cycle_percent",
            data.DutyCyclePercent.ToString("0.###", CultureInfo.InvariantCulture));
        Append(sb, "lora_config_base64", Convert.ToBase64String(data.LoraConfigBytes));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    public static LoRaBoardPresetFileData Read(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException("LoRa preset file not found.", filePath);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(filePath, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            map[key] = value;
        }

        if (!map.TryGetValue("lora_config_base64", out var b64) || string.IsNullOrWhiteSpace(b64))
            throw new InvalidDataException("lora_config_base64 is missing.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(b64);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("lora_config_base64 is not valid base64.", ex);
        }

        if (bytes.Length == 0)
            throw new InvalidDataException("lora_config_base64 decoded to an empty blob.");

        var parsed = LoRaBoardConfigInfo.Parse(bytes);
        var board = map.GetValueOrDefault("board_name") ?? Path.GetFileNameWithoutExtension(filePath);
        var port = map.GetValueOrDefault("com_port") ?? "";
        var saved = DateTime.UtcNow;
        if (map.TryGetValue("saved_utc", out var savedText)
            && DateTime.TryParse(savedText, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsedUtc))
            saved = parsedUtc.ToUniversalTime();

        return new LoRaBoardPresetFileData(
            board,
            port,
            saved,
            map.GetValueOrDefault("modem_preset") ?? parsed.ModemPresetName,
            int.TryParse(map.GetValueOrDefault("modem_preset_proto"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var mp)
                ? mp
                : parsed.ModemPresetProto,
            map.GetValueOrDefault("region") ?? parsed.RegionCode,
            int.TryParse(map.GetValueOrDefault("region_proto"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var rp)
                ? rp
                : parsed.RegionProto,
            int.TryParse(map.GetValueOrDefault("tx_power_dbm"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var dbm)
                ? dbm
                : parsed.TxPowerDbm,
            bool.TryParse(map.GetValueOrDefault("override_duty_cycle"), out var od)
                ? od
                : parsed.OverrideDutyCycle,
            double.TryParse(map.GetValueOrDefault("duty_cycle_percent"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var duty)
                ? duty
                : parsed.DutyCycleMaxPercent,
            bytes);
    }

    public static LoRaBoardPresetFileData FromBoard(
        LoRaBoardConfigInfo info,
        string boardName,
        string comPort,
        DateTime? savedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.IsEmpty)
            throw new ArgumentException("Board LoRaConfig is empty.", nameof(info));

        return new LoRaBoardPresetFileData(
            string.IsNullOrWhiteSpace(boardName) ? comPort : boardName.Trim(),
            comPort?.Trim() ?? "",
            (savedUtc ?? DateTime.UtcNow).ToUniversalTime(),
            info.ModemPresetName,
            info.ModemPresetProto,
            info.RegionCode,
            info.RegionProto,
            info.TxPowerDbm,
            info.OverrideDutyCycle,
            info.DutyCycleMaxPercent,
            info.RawBytes);
    }

    public static string FormatSummary(LoRaBoardPresetFileData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return
            $"{data.BoardName}: {data.ModemPresetName}, {data.RegionCode}, {data.TxPowerDbm} dBm, " +
            $"duty {data.DutyCyclePercent.ToString("0.###", CultureInfo.InvariantCulture)}%" +
            (data.OverrideDutyCycle ? " (override)" : "") +
            $", saved {data.SavedUtc:yyyy-MM-dd HH:mm} UTC";
    }

    private static void Append(StringBuilder sb, string key, string value) =>
        sb.Append(key).Append(':').Append(value).Append('\n');

    private static string SanitizeFileToken(string? name)
    {
        var raw = string.IsNullOrWhiteSpace(name) ? "board" : name.Trim();
        var chars = raw.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        var s = new string(chars).Trim('_');
        if (s.Length == 0)
            s = "board";
        return s.Length <= 48 ? s : s[..48];
    }
}
