using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // Forward-only IPreorderStream over the terse dft payload grammar ("a(b(d,e),c)"): an
  // incremental version of TreeSerializer's eager parse. A value followed by '(' is a parent
  // (depth increases behind it); ',' separates siblings; ')' closes a subtree. Each committed
  // value is delivered with the depth in effect while its characters were read. Values ride the
  // shared value-token layer (ValueTokenStreamScanner): quoted values may contain ANY character;
  // unquoted trailing line endings at end of input are ignored (files end in newlines).
  //
  // Struct-return read seam (TryReadNext/TrySkipToDepth -> PreorderRead): the shape shared with the
  // async twin (AsyncPreorderTextStream). TrySkipToDepth honors the skip contract: tokens belonging
  // to deeper values are consumed WITHOUT being accumulated or mapped -- a skip costs I/O only.
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

    public PreorderRead<TValue> TryReadNext() => TryScan(int.MaxValue);

    public PreorderRead<TValue> TrySkipToDepth(int maxDepth) => TryScan(maxDepth);

    // Scan to the next value committing at depth <= maxDepth; deeper values are structural
    // noise for the caller and their characters are discarded unaccumulated.
    private PreorderRead<TValue> TryScan(int maxDepth)
    {
      if (_Exhausted)
        return default;

      while (true)
      {
        var accumulate = _Depth <= maxDepth;

        if (!_Scanner.TryScanEvent(accumulate, out var hasValue, out var terminator))
        {
          _Exhausted = true;
          return default;
        }

        switch (terminator)
        {
          case '(':
            if (hasValue && accumulate)
            {
              var value = _Map(_Scanner.GetValue());
              var depth = _Depth;
              _Depth++;
              return new PreorderRead<TValue>(value, depth);
            }

            _Depth++;
            break;

          case ',':
            if (hasValue && accumulate)
              return new PreorderRead<TValue>(_Map(_Scanner.GetValue()), _Depth);

            break;

          case ')':
            if (hasValue && accumulate)
            {
              var value = _Map(_Scanner.GetValue());
              var depth = _Depth;
              _Depth--;
              return new PreorderRead<TValue>(value, depth);
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
              return new PreorderRead<TValue>(_Map(_Scanner.GetValue()), _Depth);

            return default;
        }
      }
    }

    public void Dispose() => _Reader.Dispose();
  }
}
