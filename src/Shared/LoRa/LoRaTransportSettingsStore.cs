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
    }
}
