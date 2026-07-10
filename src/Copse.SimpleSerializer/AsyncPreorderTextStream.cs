using Copse.Async;
using System;
using System.IO;
using System.Threading;
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
    public AsyncPreorderTextStream(TextReader reader, Func<string, TValue> map, CancellationToken cancellationToken)
    {
      _Reader = reader;
      _Scanner = new AsyncValueTokenStreamScanner(reader, cancellationToken);
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
    // noise for the caller and their characters are discarded unaccumulated. NOT async: every
    // scan is PROBED (the fast-path probe idiom -- see AsyncToSync), and each scanned event
    // lands through the same commit helper whether the scan answered inline or through the
    // pending continuation.
    private ValueTask<PreorderRead<TValue>> TryScanAsync(int maxDepth)
    {
      if (_Exhausted)
        return default;

      while (true)
      {
        var accumulate = _Depth <= maxDepth;

        var scanned = _Scanner.TryScanEventAsync(accumulate);

        if (!scanned.IsCompletedSuccessfully)
          return AwaitThenFinishScanAsync(scanned, maxDepth, accumulate);

        if (TryCommitEvent(scanned.Result, accumulate, out var read))
          return new ValueTask<PreorderRead<TValue>>(read);
      }
    }

    // Land one scanned event: commit a value (true, with the read), end the stream (true,
    // default read), or note the depth movement and keep scanning (false).
    private bool TryCommitEvent(ScanEvent ev, bool accumulate, out PreorderRead<TValue> read)
    {
      if (!ev.Ok)
      {
        _Exhausted = true;
        read = default;
        return true;
      }

      switch (ev.Terminator)
      {
        case '(':
          if (ev.HasValue && accumulate)
          {
            var value = _Map(_Scanner.GetValue());
            var depth = _Depth;
            _Depth++;
            read = new PreorderRead<TValue>(value, depth);
            return true;
          }

          _Depth++;
          break;

        case ',':
          if (ev.HasValue && accumulate)
          {
            read = new PreorderRead<TValue>(_Map(_Scanner.GetValue()), _Depth);
            return true;
          }

          break;

        case ')':
          if (ev.HasValue && accumulate)
          {
            var value = _Map(_Scanner.GetValue());
            var depth = _Depth;
            _Depth--;
            read = new PreorderRead<TValue>(value, depth);
            return true;
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
          {
            read = new PreorderRead<TValue>(_Map(_Scanner.GetValue()), _Depth);
            return true;
          }

          read = default;
          return true;
      }

      read = default;
      return false;
    }

    // codegen: begin async-only
    //
    // The suspension continuation. A scan ADVANCES the reader, so it consumes the pending event
    // through the same commit helper as the fast path, then re-enters the probing loop (nothing
    // to carry -- accumulate is passed along because the commit decision must match the value
    // the scan was issued with).
    private async ValueTask<PreorderRead<TValue>> AwaitThenFinishScanAsync(ValueTask<ScanEvent> pendingScan, int maxDepth, bool accumulate)
    {
      if (TryCommitEvent(await pendingScan.ConfigureAwait(false), accumulate, out var read))
        return read;

      return await TryScanAsync(maxDepth).ConfigureAwait(false);
    }
    // codegen: end async-only

    // The reader closes synchronously; async only to satisfy the IAsyncDisposable seam (the twin
    // becomes a plain void Dispose).
    public async ValueTask DisposeAsync()
    {
      _Reader.Dispose();
    }
  }
}
