using System.Text;

namespace hello_http;

public class HttpClientHandler
{
    private readonly BufferedStream _writer;
    private readonly BufferedStream _reader;
    private readonly SearchBytes _crlfSb = new("\r\n"u8.ToArray());
    private readonly SearchBytes _headerSeparatorSb = new(new[] { (byte)':' });
    private readonly CancellationToken _cancellationToken;

    public HttpClientHandler(Stream stream, CancellationToken cancellationToken = default)
    {
        _writer = new BufferedStream(stream);
        _reader = new BufferedStream(stream);
        _cancellationToken = cancellationToken;
    }

    public async Task<(MemoryStream lh, string requestLine, int contentLength)> ParseLineAndBodyAsync()
    {
        var lh = new MemoryStream(1024);
        var buf = new byte[1];
        var requestLine = "";
        var requestLineOk = false;
        var contentLength = 0;
        do
        {
            _crlfSb.Reset();
            _headerSeparatorSb.Reset();
            while (true)
            {
                if (await _reader.ReadAsync(buf, _cancellationToken) < 0)
                {
                    throw new IOException("EOF");
                }

                var b = buf[0];
                lh.WriteByte(b);

                if (_crlfSb.Search(b) > -1)
                {
                    break;
                }

                if (requestLineOk)
                {
                    _headerSeparatorSb.Search(b);
                }
            }

            var buffer = lh.GetBuffer();
            var lineEnd = (int)lh.Length - _crlfSb.Length();
            var lineStart = lineEnd - _crlfSb.Result();
            if (!requestLineOk)
            {
                requestLine = Encoding.ASCII.GetString(buffer[lineStart..lineEnd]);
                requestLineOk = true;
                continue;
            }

            var headerSeparatorIndex = _headerSeparatorSb.Result();
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
                Encoding.ASCII.GetString(buffer[(headerSeparatorIndex + _headerSeparatorSb.Length())..lineEnd]).Trim();
            // Console.WriteLine("'{0}': '{1}'", headerName, headerValue);
            contentLength = int.Parse(headerValue);
            break;
        } while (_crlfSb.Result() != 0);

        while (_crlfSb.Result() != 0)
        {
            _crlfSb.Reset();
            while (await _reader.ReadAsync(buf, _cancellationToken) > -1)
            {
                var b = buf[0];
                lh.WriteByte(b);
                if (_crlfSb.Search(b) > -1)
                {
                    break;
                }
            }
        }

        return (lh, requestLine, contentLength);
    }
}