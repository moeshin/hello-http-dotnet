using System.Net;
using System.Net.Sockets;
using System.Text;

namespace hello_http;

public static class Program
{
    private static readonly CancellationTokenSource Cts = new();
    private static readonly CancellationToken CancellationToken = Cts.Token;

    private static void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        Console.WriteLine("Exiting...");
        e.Cancel = true;
        Cts.Cancel();
    }

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

    private static string GetMethod(string requestLine)
    {
        var i = requestLine.IndexOf(' ') + 1;
        return i > 0 && i <= requestLine.Length ? requestLine[..i].ToLowerInvariant() : "";
    }

    private static async Task HandleClientIeAsync(TcpClient client)
    {
        try
        {
            await HandleClientAsync(client);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        var stream = client.GetStream();
        var crlfSb = new SearchBytes("\r\n"u8.ToArray());
        var headerSeparatorSb = new SearchBytes(new[] { (byte)':' });
        using var lh = new MemoryStream(1024);
        var buf = new byte[1];
        var requestLineOk = false;
        var contentLength = 0;
        do
        {
            crlfSb.Reset();
            headerSeparatorSb.Reset();
            while (true)
            {
                if (await stream.ReadAsync(buf, CancellationToken) < 0)
                {
                    throw new IOException("EOF");
                }

                var b = buf[0];
                lh.WriteByte(b);

                if (crlfSb.Search(b) > -1)
                {
                    break;
                }

                if (requestLineOk)
                {
                    headerSeparatorSb.Search(b);
                }
            }

            var buffer = lh.GetBuffer();
            var lineEnd = (int)lh.Length - crlfSb.Length();
            var lineStart = lineEnd - crlfSb.Result();
            if (!requestLineOk)
            {
                requestLineOk = true;
                var requestLine = Encoding.ASCII.GetString(buffer[lineStart..lineEnd]);
                Console.WriteLine(requestLine);
                var method = GetMethod(requestLine);
                if (method == ""
                    || (_disallowedMethods != null && _disallowedMethods.Contains(method)) 
                    || (_allowedMethods != null && !_allowedMethods.Contains(method)))
                {
                    await stream.WriteResponseCode(HttpStatusCode.MethodNotAllowed, CancellationToken);
                    return;
                }
                await stream.WriteResponseCode(HttpStatusCode.OK, CancellationToken);
                await stream.WriteAsync("Content-Type: text/plain"u8, CancellationToken);
                await stream.WriteNewLineAsync(CancellationToken);
                if (method == "HEAD")
                {
                    await stream.WriteNewLineAsync(CancellationToken);
                    return;
                }
                continue;
            }

            var headerSeparatorIndex = headerSeparatorSb.Result();
            if (headerSeparatorIndex < 0)
            {
                // Bad request header line
                continue;
            }
            headerSeparatorIndex += lineStart;
            var headerName = Encoding.ASCII.GetString(buffer[lineStart..headerSeparatorIndex]).Trim().ToLower();
            if (headerName != "content-length")
            {
                continue;
            }
            var headerValue =
                Encoding.ASCII.GetString(buffer[(headerSeparatorIndex + headerSeparatorSb.Length())..lineEnd]).Trim();
            // Console.WriteLine("'{0}': '{1}'", headerName, headerValue);
            contentLength = int.Parse(headerValue);
            break;
        } while (crlfSb.Result() != 0);

        while (crlfSb.Result() != 0)
        {
            crlfSb.Reset();
            while (await stream.ReadAsync(buf, CancellationToken) > -1)
            {
                var b = buf[0];
                lh.WriteByte(b);
                if (crlfSb.Search(b) > -1)
                {
                    break;
                }
            }
        }

        contentLength = Math.Max(0, contentLength);
        await stream.WriteAsync("Content-Length: "u8, CancellationToken);
        await stream.WriteAsync(((int)lh.Length + contentLength + 12).ToString(), CancellationToken);
        await stream.WriteNewLineAsync(CancellationToken);
        await stream.WriteNewLineAsync(CancellationToken);
        
        await stream.WriteAsync("Hello HTTP\n\n"u8, CancellationToken);

        lh.Seek(0, SeekOrigin.Begin);
        await lh.CopyToAsync(stream);
        await lh.DisposeAsync();

        await stream.CopyFromAsync(stream, contentLength, CancellationToken);
    }

    private static async Task Main(string[] args)
    {
        ParseArgs(args);

        Console.CancelKeyPress += HandleCancelKeyPress;
        
        Console.WriteLine("Press Ctrl+C to exit.");

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
            while (!CancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(CancellationToken);
                _ = HandleClientIeAsync(client);
            }
        }
        catch (OperationCanceledException) {}
    }
}
