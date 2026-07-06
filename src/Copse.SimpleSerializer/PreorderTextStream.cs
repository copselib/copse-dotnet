using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // Forward-only IPreorderStream over the terse dft payload grammar ("a(b(d,e),c)"): an
  // incremental version of TreeSerializer's eager parse. A value followed by '(' is a parent
  // (depth increases behind it); ',' separates siblings; ')' closes a subtree. Each committed
  // value is delivered with the depth in effect while its characters were read. Values ride the
  // shared value-token layer (ValueTokenStreamScanner): quoted values may contain ANY character;
  // unquoted line endings are insignificant.
  //
  // TrySkipToDepth honors the skip contract: tokens belonging to deeper values are consumed
  // WITHOUT being accumulated or mapped -- a skip costs I/O only.
  //
  // Owns its reader; disposing the stream disposes the reader.
  internal sealed class PreorderTextStream<TValue> : IPreorderStream<TValue>
  {
    public PreorderTextStream(TextReader reader, Func<string, TValue> map)
    {
      _Reader = reader;
      _Scanner = new ValueTokenStreamScanner(reader);
      _Map = map;
    }

    private readonly TextReader _Reader;
    private readonly ValueTokenStreamScanner _Scanner;
    private readonly Func<string, TValue> _Map;

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

      while (true)
      {
        var accumulate = _Depth <= maxDepth;

        if (!_Scanner.TryScanEvent(accumulate, out var hasValue, out var terminator))
        {
          _Exhausted = true;
          return false;
        }

        switch (terminator)
        {
          case '(':
            if (hasValue && accumulate)
            {
              value = _Map(_Scanner.GetValue());
              depth = _Depth;
              _Depth++;
              return true;
            }

            _Depth++;
            break;

          case ',':
            if (hasValue && accumulate)
            {
              value = _Map(_Scanner.GetValue());
              depth = _Depth;
              return true;
            }

            break;

          case ')':
            if (hasValue && accumulate)
            {
              value = _Map(_Scanner.GetValue());
              depth = _Depth;
              _Depth--;
              return true;
            }

            _Depth--;
            break;

          case '|':
          case ';':
            throw new FormatException(
              $"Unexpected '{terminator}': this is a level-order structural character, so the source " +
              "is not a depth-first-serialized tree (use DeserializeBreadthFirstTree).");

          default: // end of text
            _Exhausted = true;

            if (hasValue && accumulate)
            {
              value = _Map(_Scanner.GetValue());
              depth = _Depth;
              return true;
            }

            return false;
        }
      }
    }

    public void Dispose() => _Reader.Dispose();
  }
}
