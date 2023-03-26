namespace hello_http;

public class SearchBytes
{
    private readonly byte[] _bytes;
    private readonly int _length;
    private int _index;
    private int _count;

    public SearchBytes(byte[] bytes)
    {
        _bytes = bytes;
        _length = _bytes.Length;
        if (_length == 0)
        {
            throw new Exception("The length of pattern is 0");
        }
    }
    
    // public SearchBytes(string str, Encoding? encoding = null)
    //     : this((encoding ?? Encoding.ASCII).GetBytes(str))
    // {
    // }

    // public IReadOnlyCollection<byte> Bytes()
    // {
    //     return _bytes;
    // }

    public int Count()
    {
        return _count;
    }

    public int Length()
    {
        return _length;
    }

    public int Result()
    {
        return _index >= _length ? _count - _length : -1;
    }

    private int _search(byte b)
    {
        if (b != _bytes[_index++] && _index != 0)
        {
            _index = 0;
        }
        ++_count;
        return Result();
    }

    public int Search(byte b)
    {
        var r = Result();
        return r > -1 ? r : _search(b);
    }

    // public int Search(IEnumerable<byte> bytes)
    // {
    //     var r = Result();
    //     if (r > -1)
    //     {
    //         return r;
    //     }
    //     foreach (var b in bytes)
    //     {
    //         r = _search(b);
    //         if (r > -1)
    //         {
    //             return r;
    //         }
    //     }
    //     return -1;
    // }

    public int Reset()
    {
        var count = _count;
        _index = 0;
        _count = 0;
        return count;
    }

    public async Task<(int count, int result)> ReadAsync(
        Stream src, MemoryStream dest, int count = -1, CancellationToken cancellationToken = default)
    {
        var r = Reset();
        var i = 0;
        if (r < 0)
        {
            var buf = new byte[1];
            while (count < 0 || i < count)
            {
                var n = await src.ReadAsync(buf, cancellationToken);
                if (n <= 0)
                {
                    return (i, -1);
                }
                dest.Write(buf);
                ++i;
                r = Search(buf[0]);
                if (r > -1)
                {
                    break;
                }
            } 
        }
        return (i, r);
    }
}