using System;
using System.IO;
using System.Text;

namespace Copse.SimpleSerializer
{
  // Forward-only IPreorderStream over the terse dft payload grammar ("a(b(d,e),c)"): an
  // incremental version of TreeSerializer's eager parse. A value followed by '(' is a parent
  // (depth increases behind it); ',' separates siblings; ')' closes a subtree. Each committed
  // value is delivered with the depth in effect while its characters were read.
  //
  // TrySkipToDepth honors the skip contract: characters belonging to deeper values are consumed
  // WITHOUT being accumulated or mapped -- a skip costs I/O only.
  //
  // Owns its reader; disposing the stream disposes the reader.
  internal sealed class PreorderTextStream<TValue> : IPreorderStream<TValue>
  {
    public PreorderTextStream(TextReader reader, Func<string, TValue> map)
    {
      _Reader = reader;
      _Map = map;
    }

    private readonly TextReader _Reader;
    private readonly Func<string, TValue> _Map;
    private readonly StringBuilder _ValueBuilder = new StringBuilder();

    private int _Depth;
    private bool _Exhausted;

    public bool TryReadNext(out TValue value, out int depth)
      => TryScan(int.MaxValue, out value, out depth);

    public bool TrySkipToDepth(int maxDepth, out TValue value, out int depth)
      => TryScan(maxDepth, out value, out depth);

    // Scan to the next value committing at depth <= maxDepth; deeper values are structural
    // noise for the caller and their characters are discarded unaccumulated.
    private bool TryScan(int maxDepth, out TValue value, out int depth)
    {
      value = default;
      depth = default;

      if (_Exhausted)
        return false;

      _ValueBuilder.Clear();
      var hasChars = false;

      while (true)
      {
        var read = _Reader.Read();

        if (read < 0)
        {
          _Exhausted = true;

          if (hasChars && _Depth <= maxDepth)
          {
            value = _Map(_ValueBuilder.ToString());
            depth = _Depth;
            return true;
          }

          return false;
        }

        var character = (char)read;

        switch (character)
        {
          case '(':
            if (hasChars && _Depth <= maxDepth)
            {
              value = _Map(_ValueBuilder.ToString());
              depth = _Depth;
              _Depth++;
              return true;
            }

            _Depth++;
            hasChars = false;
            _ValueBuilder.Clear();
            break;

          case ',':
            if (hasChars && _Depth <= maxDepth)
            {
              value = _Map(_ValueBuilder.ToString());
              depth = _Depth;
              return true;
            }

            hasChars = false;
            _ValueBuilder.Clear();
            break;

          case ')':
            if (hasChars && _Depth <= maxDepth)
            {
              value = _Map(_ValueBuilder.ToString());
              depth = _Depth;
              _Depth--;
              return true;
            }

            _Depth--;
            hasChars = false;
            _ValueBuilder.Clear();
            break;

          case '\n':
          case '\r':
            break;

          default:
            if (_Depth <= maxDepth)
              _ValueBuilder.Append(character);

            hasChars = true;
            break;
        }
      }
    }

    public void Dispose() => _Reader.Dispose();
  }
}
