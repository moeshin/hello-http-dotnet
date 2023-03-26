using System.Text;

namespace hello_http;

public class HttpClientHandler
{
    private readonly BufferedStream _writer;
    private readonly BufferedStream _reader;
    private readonly SearchBytes _crlfSb = new("\r\n"u8.ToArray());
    private readonly SearchBytes _contentLengthSb = new("content-length:"u8.ToArray());
    // private readonly SearchBytes _headerSeparatorSb = new(new[] { (byte)':' });
    private readonly CancellationToken _cancellationToken;

    public HttpClientHandler(Stream stream, CancellationToken cancellationToken = default)
    {
        _writer = new BufferedStream(stream);
        _reader = new BufferedStream(stream);
        _cancellationToken = cancellationToken;
    }

    public async Task<(MemoryStream lh, string requestLine, int contentLength)> ParseLineAndBodyAsync()
    {
        var lh = new MemoryStream();
        var buf = new byte[1];
        var requestLine = "";
        var requestLineOk = false;
        var contentLength = 0;
        do
        {
            _crlfSb.Reset();
            _contentLengthSb.Reset();
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
                    _contentLengthSb.Search(b >= 'A' && b <= 'Z' ? (byte)(32 + b) : b);
                }
            }

            if (requestLineOk)
            {
                var i = _contentLengthSb.Result();
                Console.WriteLine(i);
                if (i < 0)
                {
                    continue;
                }
                i += _contentLengthSb.Length();
                var buffer = lh.GetBuffer();
                var length = (int)lh.Length - _crlfSb.Length();
                contentLength = int.Parse(
                    Encoding.ASCII.GetString(buffer[(length - _crlfSb.Result() + i)..length]).Trim());
                break;
            }

            var length2 = (int)lh.Length - _crlfSb.Length();
            requestLine = Encoding.ASCII.GetString(lh.GetBuffer()[(length2 - _crlfSb.Result())..length2]);
            requestLineOk = true;
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