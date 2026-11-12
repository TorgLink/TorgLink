using ShortP2P.Auth;
using ShortP2P.LoRa;
using ShortP2P.LoRa.Jurisdiction;

namespace TorgLink.LoRa;

/// <summary>Persists <see cref="LoRaTransportSettings"/> in <see cref="ISessionStorage"/>.</summary>
public sealed class LoRaTransportSettingsStore(ISessionStorage storage)
{
    private const string KEnabled = "lora_enabled";
    private const string KComPort = "lora_com_port";
    private const string KBaudRate = "lora_baud_rate";
    private const string KJurisdiction = "lora_jurisdiction";
    private const string KTxPowerDbm = "lora_tx_power_dbm";
    private const string KModemPreset = "lora_modem_preset";
    private const string KUseBoardPresets = "lora_use_board_presets";
    private const string KAutoSnapshotPortPrefix = "lora_board_autosnapshot_port:";

    private readonly ISessionStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public async Task<LoRaTransportSettings> LoadAsync()
    {
        var s = new LoRaTransportSettings();
        if (bool.TryParse(await _storage.GetAsync(KEnabled).ConfigureAwait(false), out var enabled))
            s.Enabled = enabled;
        s.ComPort = (await _storage.GetAsync(KComPort).ConfigureAwait(false))?.Trim() ?? "";
        if (int.TryParse(await _storage.GetAsync(KBaudRate).ConfigureAwait(false), out var baud) && baud > 0)
            s.BaudRate = baud;
        var jur = await _storage.GetAsync(KJurisdiction).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(jur))
            s.JurisdictionCode = jur.Trim();
        var profile = JurisdictionCatalog.Find(s.JurisdictionCode) ?? JurisdictionCatalog.Unset;
        if (int.TryParse(await _storage.GetAsync(KTxPowerDbm).ConfigureAwait(false), out var dbm))
            s.TxPowerDbm = LoRaTxPower.ClampUi(dbm);
        else
            s.TxPowerDbm = LoRaTxPower.DefaultForJurisdiction(profile);
        s.ModemPreset = LoRaModemPresets.NormalizeName(
            await _storage.GetAsync(KModemPreset).ConfigureAwait(false));
        if (bool.TryParse(await _storage.GetAsync(KUseBoardPresets).ConfigureAwait(false), out var useBoard))
            s.UseBoardPresets = useBoard;
        return s;
    }

    public async Task SaveAsync(LoRaTransportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await _storage.SetAsync(KEnabled, settings.Enabled.ToString()).ConfigureAwait(false);
        await _storage.SetAsync(KComPort, settings.ComPort?.Trim() ?? "").ConfigureAwait(false);
        await _storage.SetAsync(KBaudRate, settings.BaudRate.ToString()).ConfigureAwait(false);
        await _storage.SetAsync(KJurisdiction, string.IsNullOrWhiteSpace(settings.JurisdictionCode)
            ? "UNSET"
            : settings.JurisdictionCode.Trim()).ConfigureAwait(false);
        await _storage.SetAsync(KTxPowerDbm, LoRaTxPower.ClampUi(settings.TxPowerDbm).ToString())
            .ConfigureAwait(false);
        await _storage.SetAsync(KModemPreset, LoRaModemPresets.NormalizeName(settings.ModemPreset))
            .ConfigureAwait(false);
        await _storage.SetAsync(KUseBoardPresets, settings.UseBoardPresets.ToString())
            .ConfigureAwait(false);
    }

    /// <summary>True when this COM port already has an auto-saved board snapshot file.</summary>
    public async Task<bool> HasAutoSnapshotForPortAsync(string? comPort)
    {
        var key = AutoSnapshotKey(comPort);
        if (key == null)
            return false;
        var path = await _storage.GetAsync(key).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
    }

    public async Task RememberAutoSnapshotPathAsync(string? comPort, string filePath)
    {
        var key = AutoSnapshotKey(comPort);
        if (key == null || string.IsNullOrWhiteSpace(filePath))
            return;
        await _storage.SetAsync(key, filePath).ConfigureAwait(false);
    }

    public async Task<string?> GetAutoSnapshotPathAsync(string? comPort)
    {
        var key = AutoSnapshotKey(comPort);
        if (key == null)
            return null;
        var path = await _storage.GetAsync(key).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static string? AutoSnapshotKey(string? comPort)
    {
        var port = comPort?.Trim();
        if (string.IsNullOrWhiteSpace(port))
            return null;
        return KAutoSnapshotPortPrefix + port.ToUpperInvariant();
    }
}
