using Copse.Async;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  // ASYNC forward-only IAsyncLevelOrderStream over the bft payload grammar ("a;b,c;d,e"), reading
  // through the async scanner (await at the char seam). Byte-for-byte the same scan logic as the sync
  // twin; the difference is the awaits and the struct-return read.
  //
  // This is the single source of truth. Strip the awaits and it collapses to the synchronous
  // Copse.SimpleSerializer.LevelOrderTextStream (the checked-in .g.cs twin). Owns its reader.
  internal sealed class AsyncLevelOrderTextStream<TValue> : IAsyncLevelOrderStream<TValue>
  {
    public AsyncLevelOrderTextStream(TextReader reader, Func<string, TValue> map, CancellationToken cancellationToken)
    {
      _Reader = reader;
      _Scanner = new AsyncValueTokenStreamScanner(reader, cancellationToken);
      _Map = map;
    }

    private readonly TextReader _Reader;
    private readonly AsyncValueTokenStreamScanner _Scanner;
    private readonly Func<string, TValue> _Map;

    private bool _GroupEnded;
    private bool _Exhausted;

    public async ValueTask<LevelOrderRead<TValue>> TryReadNextInGroupAsync()
    {
      if (_GroupEnded || _Exhausted)
        return default;

      while (true)
      {
        var ev = await _Scanner.TryScanEventAsync(accumulate: true).ConfigureAwait(false);

        if (!ev.Ok)
        {
          _Exhausted = true;
          _GroupEnded = true;
          return default;
        }

        switch (ev.Terminator)
        {
          case ',':
            if (ev.HasValue)
              return new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));

            break;

          case '|':
          case ';':
            _GroupEnded = true;

            if (ev.HasValue)
              return new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));

            return default;

          case '(':
          case ')':
            throw new FormatException(
              $"Unexpected '{ev.Terminator}': this is a depth-first structural character, so the source " +
              "is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

          default: // end of text
            _Exhausted = true;
            _GroupEnded = true;

            if (ev.HasValue)
              return new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));

            return default;
        }
      }
    }

    public async ValueTask<int> SkipGroupRemainderAsync()
    {
      if (_GroupEnded || _Exhausted)
        return 0;

      var count = 0;

      while (true)
      {
        var ev = await _Scanner.TryScanEventAsync(accumulate: false).ConfigureAwait(false);

        if (!ev.Ok)
        {
          _Exhausted = true;
          _GroupEnded = true;
          return count;
        }

        switch (ev.Terminator)
        {
          case ',':
            if (ev.HasValue)
              count++;

            break;

          case '|':
          case ';':
            _GroupEnded = true;

            if (ev.HasValue)
              count++;

            return count;

          case '(':
          case ')':
            throw new FormatException(
              $"Unexpected '{ev.Terminator}': this is a depth-first structural character, so the source " +
              "is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

          default: // end of text
            _Exhausted = true;
            _GroupEnded = true;

            if (ev.HasValue)
              count++;

            return count;
        }
      }
    }

    // No I/O -- just flips the group flag -- but async to satisfy the seam (returns bool literals,
    // not new ValueTask<bool>(...), so the twin is a plain bool method).
    public async ValueTask<bool> TryMoveToNextGroupAsync()
    {
      if (_Exhausted)
        return false;

      if (!_GroupEnded)
        throw new InvalidOperationException("The current group must be finished (read or skipped to its end) before advancing.");

      _GroupEnded = false;
      return true;
    }

    // The reader closes synchronously; async only to satisfy the IAsyncDisposable seam (the twin
    // becomes a plain void Dispose).
    public async ValueTask DisposeAsync()
    {
      _Reader.Dispose();
    }
  }
}
