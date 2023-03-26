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

    // public int Count()
    // {
    //     return _count;
    // }

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

    public void Reset()
    {
        _index = 0;
        _count = 0;
    }
}