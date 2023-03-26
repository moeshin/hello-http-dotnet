using System.Net;
using System.Net.Sockets;
using System.Text;

namespace hello_http;

public static class NetworkStreamExtensions
{
    public static ValueTask WriteAsync(this NetworkStream stream, ReadOnlySpan<byte> bytes,
        CancellationToken cancellationToken)
    {
        return stream.WriteAsync(bytes.ToArray(), cancellationToken);
    }
    
    public static ValueTask WriteAsync(this NetworkStream stream, string str, CancellationToken cancellationToken)
    {
        return stream.WriteAsync(Encoding.ASCII.GetBytes(str), cancellationToken);
    }

    public static ValueTask WriteNewLineAsync(this NetworkStream stream, CancellationToken cancellationToken)
    {
        return stream.WriteAsync("\r\n"u8, cancellationToken);
    }

    public static async Task WriteResponseCode(this NetworkStream stream, HttpStatusCode code,
        CancellationToken cancellationToken)
    {
        await stream.WriteAsync("HTTP/1.0 "u8, cancellationToken);
        await stream.WriteAsync(((int)code).ToString(), cancellationToken);
        await stream.WriteAsync(" "u8, cancellationToken);
        await stream.WriteAsync(code.ToString(), cancellationToken);
        await stream.WriteNewLineAsync(cancellationToken);
    }

    public static async Task CopyFromAsync(this NetworkStream dest, Stream src, int length,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024;
        var buffer = new byte[bufferSize];
        var totalBytesRead = 0;
        while (totalBytesRead < length)
        {
            var bytesToRead = Math.Min(bufferSize, length - totalBytesRead);
            var bytesRead = await src.ReadAsync(buffer.AsMemory(0, bytesToRead), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }
            await dest.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;
        }
    }
}