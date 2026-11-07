using System.IO.Ports;

namespace TorgLink.LoRa;

/// <summary>COM names shown in LoRa settings. COM1–COM4 are always listed.</summary>
public static class SerialComPorts
{
    public static readonly string[] Standard = ["COM1", "COM2", "COM3", "COM4"];

    public static IReadOnlyList<string> List(string? selected = null)
    {
        var names = new List<string>(Standard);
        try
        {
            foreach (var raw in SerialPort.GetPortNames())
                Add(names, raw);
        }
        catch
        {
            // Serial stack is missing (for example Android). Keep COM1–COM4.
        }

        Add(names, selected);
        names.Sort(Compare);
        return names;
    }

    private static void Add(List<string> names, string? raw)
    {
        var port = raw?.Trim() ?? "";
        if (port.Length == 0)
            return;
        if (names.Exists(n => string.Equals(n, port, StringComparison.OrdinalIgnoreCase)))
            return;
        names.Add(port);
    }

    private static int Compare(string a, string b)
    {
        var order = PortNumber(a).CompareTo(PortNumber(b));
        return order != 0 ? order : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static int PortNumber(string name)
    {
        if (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(name.AsSpan(3), out var n))
            return n;
        return int.MaxValue;
    }
}
