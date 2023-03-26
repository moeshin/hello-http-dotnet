using System.Net;
using System.Net.Sockets;
using System.Text;

namespace hello_http;

public static class Program
{
    private const string FormatMethods = "<method>[,<method>...]";
    private static string _host = "127.0.0.1";
    private static int _port = 8080;
    private static ISet<string>? _allowedMethods;
    private static ISet<string>? _disallowedMethods;

    private static void PrintHelp()
    {
        var file = AppDomain.CurrentDomain.FriendlyName;
        if (file.Contains(' '))
        {
            file = '"' + file + '"';
        }
        Console.Write(@"
Usage: {0} [options]

Options:
  -h <host>
        Listen host IP.
        If 0.0.0.0 will only listen all IPv4.
        If [::] will only listen all IPv6.
        If :: will listen all IPv4 and IPv6.
        (default ""{1}"")
  -p <port>
        Listen port.
        If 0 is random.
        (default {2})
  -m {3}
        Disallowed methods.
  -d {3}
        Allowed methods.
  --help
        Print help.
", file, _host, _port, FormatMethods);
    }

    private static ISet<string>? ParseMethods(string str)
    {
        var set = new HashSet<string>();
        foreach (var s in str.Split(','))
        {
            var method = s.Trim();
            if (method != "")
            {
                set.Add(method.ToUpper());
            }
        }
        return set.Count > 0 ? set : null;
    }

    private static void ParseArgs(IReadOnlyList<string> args)
    { 
        for (var i = 0; i < args.Count; ++i)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                case "-h":
                    _host = args[++i];
                    break;
                case "-p":
                    _port = int.Parse(args[++i]);
                    break;
                case "-m":
                    _allowedMethods = ParseMethods(args[++i]);
                    break;
                case "-d":
                    _disallowedMethods = ParseMethods(args[++i]);
                    break;
                default:
                    Console.WriteLine("Unknown arg: \"{0}\"", arg);
                    PrintHelp();
                    Environment.Exit(1);
                    break;
            }
        }
    }

    private static async Task HandleClientIeAsync(TcpClient client ,CancellationToken cancellationToken)
    {
        try
        {
            await HandleClientAsync(client, cancellationToken);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            await client.GetStream().DisposeAsync();
            client.Dispose();
        }
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        Console.WriteLine("Receive");
        var handler = new HttpClientHandler(client.GetStream(), cancellationToken);
        var (_, requestLine, contentLength) = await handler.ParseLineAndBodyAsync();
        Console.WriteLine("requestLine:" + requestLine);
        Console.WriteLine("contentLength:" + contentLength);

        var writer = new StreamWriter(client.GetStream());
        writer.NewLine = "\r\n";
        writer.WriteLine("HTTP/1.0 200 OK");
        writer.WriteLine("Content-Type: text/plain; charset=UTF-8");
        writer.WriteLine();
        writer.WriteLine("Hello, world!");
        writer.Flush();
    }

    private static async Task ListenAsync(CancellationToken cancellationToken)
    {
        IPAddress addr;
        switch (_host)
        {
            case "0.0.0.0":
                addr = IPAddress.Any;
                break;
            case "::":
            case "[::]":
                addr = IPAddress.IPv6Any;
                break;
            default:
                addr = IPAddress.Parse(_host);
                break;
        }
        var listener = new TcpListener(addr, _port);
        if (_host == "::")
        {
            listener.Server.DualMode = true;
        }
        Console.WriteLine("Listening: {0}", listener.LocalEndpoint);
        listener.Start();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientIeAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) {}
    }

    private static async Task Main(string[] args)
    {
        ParseArgs(args);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            Console.WriteLine("Exiting...");
            e.Cancel = true;
            cts.Cancel();
        };
        
        Console.WriteLine("Press Ctrl+C to exit.");
        await ListenAsync(cts.Token);
    }
}
