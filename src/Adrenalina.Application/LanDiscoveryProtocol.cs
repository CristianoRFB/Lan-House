using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Adrenalina.Application;

public sealed class LanDiscoveryAnnouncement
{
    public string Service { get; init; } = "";
    public int Version { get; init; }
    public int Port { get; init; }
    public bool UseHttps { get; init; }
}

public static class LanDiscoveryProtocol
{
    public const int Port = 5075;
    public const int Version = 1;
    private const string Request = "ADRENALINA_DISCOVER_V1";

    public static async Task<Uri?> DiscoverServerAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork)
        {
            EnableBroadcast = true
        };
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        await udp.SendAsync(Encoding.ASCII.GetBytes(Request), new IPEndPoint(IPAddress.Broadcast, Port));
        while (!timeoutSource.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await udp.ReceiveAsync(timeoutSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            LanDiscoveryAnnouncement? announcement;
            try
            {
                announcement = JsonSerializer.Deserialize<LanDiscoveryAnnouncement>(received.Buffer, JsonDefaults.Options);
            }
            catch (JsonException)
            {
                continue;
            }

            if (announcement is null || announcement.Service != "Adrenalina.Admin" ||
                announcement.Version != Version || announcement.Port is < 1 or > 65535)
            {
                continue;
            }

            var scheme = announcement.UseHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
            return new Uri($"{scheme}://{received.RemoteEndPoint.Address}:{announcement.Port}/");
        }

        return null;
    }

    public static bool IsDiscoveryRequest(ReadOnlySpan<byte> payload) =>
        payload.SequenceEqual(Encoding.ASCII.GetBytes(Request));

    public static byte[] CreateAnnouncement(int serverPort, bool useHttps) =>
        JsonSerializer.SerializeToUtf8Bytes(new LanDiscoveryAnnouncement
        {
            Service = "Adrenalina.Admin",
            Version = Version,
            Port = serverPort,
            UseHttps = useHttps
        }, JsonDefaults.Options);
}
