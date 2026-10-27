using System.Net.Sockets;
using System.Text;

namespace BrimDeck.Mac.Hosting;

// A second launch asks the running copy to show itself, then leaves. The socket lives in the temp
// directory because a path under ~/Library can be longer than the macOS sockaddr limit.
public static class InstanceLock
{
    private static Socket? _listener;

    public static string SocketPath => Path.Combine(Path.GetTempPath(), "brimdeck-" + Sanitize(Environment.UserName) + ".sock");

    public static bool Acquire(LaunchOptions options)
    {
        var path = SocketPath;
        if (Ping(path, options)) return false;
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(path));
            socket.Listen(1);
        }
        catch (SocketException)
        {
            socket.Dispose();
            return !Ping(path, options);
        }
        _listener = socket;
        _ = Task.Run(() => Accept(socket));
        return true;
    }

    public static event Action<LaunchOptions>? Signaled;

    private static void Accept(Socket socket)
    {
        while (true)
        {
            try
            {
                using var client = socket.Accept();
                var buffer = new byte[512];
                int read = client.Receive(buffer);
                var text = Encoding.UTF8.GetString(buffer, 0, Math.Max(0, read));
                var options = LaunchOptions.Parse(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
                Signaled?.Invoke(options);
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException) { return; }
        }
    }

    private static bool Ping(string path, LaunchOptions options)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.ReceiveTimeout = 400;
            client.SendTimeout = 400;
            client.Connect(new UnixDomainSocketEndPoint(path));
            var payload = Encoding.UTF8.GetBytes((options.Preview ? "--preview\n" : "") + "--show\n");
            client.Send(payload);
            return true;
        }
        catch (Exception ex) when (ex is SocketException or IOException) { return false; }
    }

    private static string Sanitize(string name)
    {
        var cleaned = new string(name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        return cleaned.Length == 0 ? "user" : cleaned;
    }
}
