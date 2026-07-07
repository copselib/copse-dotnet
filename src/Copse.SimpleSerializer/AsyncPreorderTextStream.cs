using Copse.Async;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  // ASYNC forward-only IAsyncPreorderStream over the terse dft payload grammar ("a(b(d,e),c)"),
  // reading through the async scanner (await at the char seam). Byte-for-byte the same scan logic as
  // the sync twin; the difference is the awaits and the struct-return ScanEvent.
  //
  // This is the single source of truth. Strip the awaits and it collapses to the synchronous
  // Copse.SimpleSerializer.PreorderTextStream (the checked-in .g.cs twin). Owns its reader.
  internal sealed class AsyncPreorderTextStream<TValue> : IAsyncPreorderStream<TValue>
  {
    public AsyncPreorderTextStream(TextReader reader, Func<string, TValue> map)
    {
      _Reader = reader;
      _Scanner = new AsyncValueTokenStreamScanner(reader);
      _Map = map;
    }

    private readonly TextReader _Reader;
    private readonly AsyncValueTokenStreamScanner _Scanner;
    private readonly Func<string, TValue> _Map;

    private int _Depth;
    private bool _Exhausted;

    public ValueTask<PreorderRead<TValue>> TryReadNextAsync() => TryScanAsync(int.MaxValue);

    public ValueTask<PreorderRead<TValue>> TrySkipToDepthAsync(int maxDepth) => TryScanAsync(maxDepth);

    // Scan to the next value committing at depth <= maxDepth; deeper values are structural
    // noise for the caller and their characters are discarded unaccumulated.
    private async ValueTask<PreorderRead<TValue>> TryScanAsync(int maxDepth)
    {
      if (_Exhausted)
        return default;

      while (true)
      {
        var accumulate = _Depth <= maxDepth;

        var ev = await _Scanner.TryScanEventAsync(accumulate).ConfigureAwait(false);

        if (!ev.Ok)
        {
          _Exhausted = true;
          return default;
        }

        switch (ev.Terminator)
        {
          case '(':
            if (ev.HasValue && accumulate)
            {
              var value = _Map(_Scanner.GetValue());
              var depth = _Depth;
              _Depth++;
              return new PreorderRead<TValue>(value, depth);
            }

            _Depth++;
            break;

          case ',':
            if (ev.HasValue && accumulate)
              return new PreorderRead<TValue>(_Map(_Scanner.GetValue()), _Depth);

            break;

          case ')':
            if (ev.HasValue && accumulate)
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
              $"Unexpected '{ev.Terminator}': this is a level-order structural character, so the source " +
              "is not a depth-first-serialized tree (use DeserializeBreadthFirstTree).");

          default: // end of text
            _Exhausted = true;

            if (ev.HasValue && accumulate)
              return new PreorderRead<TValue>(_Map(_Scanner.GetValue()), _Depth);

            return default;
        }
      }
    }

    // The reader closes synchronously; async only to satisfy the IAsyncDisposable seam (the twin
    // becomes a plain void Dispose).
    public async ValueTask DisposeAsync()
    {
      _Reader.Dispose();
    }
  }
}
