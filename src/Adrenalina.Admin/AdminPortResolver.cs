using System.Net.NetworkInformation;

namespace Adrenalina.Admin;

public static class AdminPortResolver
{
    public static IEnumerable<int> GetCandidatePorts(int preferredPort, int maxAdditionalPorts)
    {
        var lastPort = preferredPort + Math.Max(0, maxAdditionalPorts);
        for (var port = preferredPort; port <= lastPort; port++)
        {
            yield return port;
        }
    }

    public static bool IsPortAvailable(int port)
    {
        return IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .All(endpoint => endpoint.Port != port);
    }

    public static bool IsAddressInUse(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase) ||
                current.GetType().Name.Contains("AddressInUse", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
