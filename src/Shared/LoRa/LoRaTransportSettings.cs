using ShortP2P.LoRa.Jurisdiction;

namespace TorgLink.LoRa;

/// <summary>Persisted LoRa / Meshtastic host settings (TorgLink).</summary>
public sealed class LoRaTransportSettings
{
    /// <summary>When false, the Meshtastic transport is not started.</summary>
    public bool Enabled { get; set; }

    /// <summary>Board COM port, e.g. COM3 or /dev/ttyUSB0.</summary>
    public string ComPort { get; set; } = "";

    public int BaudRate { get; set; } = 115200;

    /// <summary>Jurisdiction code from ShortP2P.LoRa JurisdictionCatalog (default UNSET → no TX).</summary>
    public string JurisdictionCode { get; set; } = "UNSET";

    /// <summary>TX power in dBm for the settings dropdown (REQ-RF-8), range 5…22.</summary>
    public int TxPowerDbm { get; set; } = LoRaTxPower.MaxDbm;
}
