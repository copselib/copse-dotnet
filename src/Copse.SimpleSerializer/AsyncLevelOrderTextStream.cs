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

    // NOT async, and neither is the skip below: every scan is PROBED (the fast-path probe idiom
    // -- see AsyncToSync), and each scanned event lands through the same commit helper whether
    // the scan answered inline or through the pending continuation.
    public ValueTask<LevelOrderRead<TValue>> TryReadNextInGroupAsync()
    {
      if (_GroupEnded || _Exhausted)
        return default;

      while (true)
      {
        var scanned = _Scanner.TryScanEventAsync(accumulate: true);

        if (!scanned.IsCompletedSuccessfully)
          return AwaitThenFinishReadAsync(scanned);

        if (TryCommitRead(scanned.Result, out var read))
          return new ValueTask<LevelOrderRead<TValue>>(read);
      }
    }

    // Land one scanned event: yield a value or the group/stream end (true, with the read), or
    // note an empty slot and keep scanning (false).
    private bool TryCommitRead(ScanEvent ev, out LevelOrderRead<TValue> read)
    {
      if (!ev.Ok)
      {
        _Exhausted = true;
        _GroupEnded = true;
        read = default;
        return true;
      }

      switch (ev.Terminator)
      {
        case ',':
          if (ev.HasValue)
          {
            read = new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));
            return true;
          }

          break;

        case '|':
        case ';':
          _GroupEnded = true;

          read = ev.HasValue ? new LevelOrderRead<TValue>(_Map(_Scanner.GetValue())) : default;
          return true;

        case '(':
        case ')':
          throw new FormatException(
            $"Unexpected '{ev.Terminator}': this is a depth-first structural character, so the source " +
            "is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

        default: // end of text
          _Exhausted = true;
          _GroupEnded = true;

          read = ev.HasValue ? new LevelOrderRead<TValue>(_Map(_Scanner.GetValue())) : default;
          return true;
      }

      read = default;
      return false;
    }

    public ValueTask<int> SkipGroupRemainderAsync()
    {
      if (_GroupEnded || _Exhausted)
        return new ValueTask<int>(0);

      return SkipGroupRemainderCoreAsync(0);
    }

    private ValueTask<int> SkipGroupRemainderCoreAsync(int count)
    {
      while (true)
      {
        var scanned = _Scanner.TryScanEventAsync(accumulate: false);

        if (!scanned.IsCompletedSuccessfully)
          return AwaitThenFinishSkipAsync(scanned, count);

        if (TryCommitSkip(scanned.Result, ref count))
          return new ValueTask<int>(count);
      }
    }

    // Land one scanned event: count it and note whether the group (or stream) ended (true), or
    // keep skipping (false).
    private bool TryCommitSkip(ScanEvent ev, ref int count)
    {
      if (!ev.Ok)
      {
        _Exhausted = true;
        _GroupEnded = true;
        return true;
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

          return true;

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

          return true;
      }

      return false;
    }

    // No I/O -- just flips the group flag; never pending, so no probe and no continuation.
    public ValueTask<bool> TryMoveToNextGroupAsync()
    {
      if (_Exhausted)
        return new ValueTask<bool>(false);

      if (!_GroupEnded)
        throw new InvalidOperationException("The current group must be finished (read or skipped to its end) before advancing.");

      _GroupEnded = false;
      return new ValueTask<bool>(true);
    }

    // codegen: begin async-only
    //
    // The suspension continuations. A scan ADVANCES the reader, so each consumes the pending
    // event through the same commit helper as the fast path, then re-enters its probing loop
    // (the skip's running count rides as the core's parameter).
    private async ValueTask<LevelOrderRead<TValue>> AwaitThenFinishReadAsync(ValueTask<ScanEvent> pendingScan)
    {
      if (TryCommitRead(await pendingScan.ConfigureAwait(false), out var read))
        return read;

      return await TryReadNextInGroupAsync().ConfigureAwait(false);
    }

    private async ValueTask<int> AwaitThenFinishSkipAsync(ValueTask<ScanEvent> pendingScan, int count)
    {
      if (TryCommitSkip(await pendingScan.ConfigureAwait(false), ref count))
        return count;

      return await SkipGroupRemainderCoreAsync(count).ConfigureAwait(false);
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
