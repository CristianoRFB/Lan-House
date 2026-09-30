using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Adrenalina.Admin;

public static class AdminNetworkLocator
{
    public static IReadOnlyList<string> GetReachableBaseUrls(int port, string scheme = "http")
    {
        try
        {
            return FindReachableBaseUrls(port);
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Nao foi possivel localizar o IP do ADMIN: {exception}");
            return [];
        }
    }

    private static IReadOnlyList<string> FindReachableBaseUrls(int port)
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses
                .Select(address => new { address.Address, networkInterface.NetworkInterfaceType }))
            .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(item.Address))
            .Where(item => !item.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .OrderByDescending(item => item.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .ThenByDescending(item => IsPrivateAddress(item.Address))
            .Select(item => item.Address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(address => $"{scheme}://{address}:{port}/")
            .ToList();
    }

    public static string GetPreferredBaseUrl(int port, string scheme = "http")
    {
        return GetReachableBaseUrls(port, scheme).FirstOrDefault() ?? $"{scheme}://127.0.0.1:{port}/";
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
