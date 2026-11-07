namespace TorgLink.LoRa;

/// <summary>UI-facing MeshDirectory binding.</summary>
public sealed record LoRaMeshPeerInfo(
    string NetworkIdShort,
    string MeshIdHex,
    string Subject,
    bool IsActive,
    bool Confirmed,
    DateTime LastSeenUtc);

/// <summary>TorgLink peer that answered a Meshtastic presence ping (network id + nickname).</summary>
public sealed record LoRaDiscoveredPeerInfo(
    string NetworkIdShort,
    string Nickname,
    string MeshIdHex,
    DateTime LastSeenUtc);

/// <summary>A node from the Meshtastic radio database (not necessarily a TorgLink peer).</summary>
public sealed record LoRaRadioNodeInfo(
    uint NodeNumber,
    string NodeId,
    string LongName,
    string ShortName,
    string DisplayName);
