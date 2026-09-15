using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Adrenalina.Admin;

public static class AdminNetworkLocator
{
    public static IReadOnlyList<string> GetReachableBaseUrls(int port, string scheme = "http")
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())
            .Where(address => !address.StartsWith("169.254.", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(address => $"{scheme}://{address}:{port}/")
            .ToList();
    }

    public static string GetPreferredBaseUrl(int port, string scheme = "http")
    {
        return GetReachableBaseUrls(port, scheme).FirstOrDefault() ?? $"{scheme}://127.0.0.1:{port}/";
    }
}
