using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.SimpleSerializer
{
  // ValueToken's ASYNC reader over a forward-only TextReader, shared by both async text streams.
  // Same event contract as ValueTokenStringScanner -- an optional value, then the structural
  // character that terminated it ('\0' at end of text, delivered exactly once). accumulate: false
  // honors the skip contract: the token is consumed and reported but its characters are never
  // buffered, so a skip costs I/O only.
  //
  // I/O happens at BLOCK granularity: one awaited ReadAsync refills the block buffer, and every
  // character between refills is served synchronously from it -- never an await per character.
  // The one-character pushback (needed to tell '""' from a closing quote) is a position
  // decrement into the block; it never crosses a refill because the pushed-back character was
  // just read from the current block.
  //
  // Cancellation is cooperative at the same block granularity: the token is checked before each
  // refill (once per 4096 characters of I/O), not per character. The in-flight ReadAsync itself
  // is not cancellable -- the Memory<char>/CancellationToken reader overloads do not exist on
  // net48/netstandard2.0, and a block completes quickly anyway.
  //
  // This is the single source of truth. Strip the awaits and it collapses to the synchronous
  // ValueTokenStreamScanner (the checked-in .g.cs twin) -- which reads the same block-buffered
  // way, so the single source buys both colors the batched I/O; the cancellation plumbing is
  // elided from the twin entirely. The struct-return ScanEvent replaces the out params, which
  // cannot cross an await. Does NOT own the reader; the enclosing stream does.
  internal sealed class AsyncValueTokenStreamScanner
  {
    private const int BlockSize = 4096;

    public AsyncValueTokenStreamScanner(TextReader reader, CancellationToken cancellationToken)
    {
      _Reader = reader;
      _CancellationToken = cancellationToken;
    }

    private readonly TextReader _Reader;
    private readonly CancellationToken _CancellationToken;
    private readonly StringBuilder _ValueBuilder = new StringBuilder();
    private readonly char[] _Block = new char[BlockSize];

    // The served [_Position, _Length) window of _Block; equal means drained (refill on next read).
    private int _Position;
    private int _Length;

    private bool _EndDelivered;

    // The value of the last accumulated event with hasValue = true; valid until the next scan.
    public string GetValue() => _ValueBuilder.ToString();

    // NOT async, and neither are the scan loops below: every character read is PROBED, and a
    // character already in the block stays ordinary method calls with no state machine -- the
    // fast-path probe idiom (see AsyncToSync). A pending read is always a REFILL, so its
    // continuation pushes the refilled character back into the block (the scanner's own
    // one-character pushback) and re-enters the probing loop, which re-serves it synchronously
    // -- re-entry needs no resume state beyond the loop's carried flags, which ride as
    // parameters of the core.
    public ValueTask<ScanEvent> TryScanEventAsync(bool accumulate)
    {
      if (_EndDelivered)
        return default;

      _ValueBuilder.Clear();

      // a bare token of nothing but line endings vanishes at end of input (survivesTrim)
      return ScanEventCoreAsync(accumulate, started: false, survivesTrim: false);
    }

    private ValueTask<ScanEvent> ScanEventCoreAsync(bool accumulate, bool started, bool survivesTrim)
    {
      while (true)
      {
        var read = ReadCharacterAsync();

        if (!read.IsCompletedSuccessfully)
          return AwaitPushbackThenScanEventCoreAsync(read, accumulate, started, survivesTrim);

        if (read.Result < 0)
        {
          _EndDelivered = true;

          var hasValue = false;

          if (started && survivesTrim)
          {
            if (accumulate)
              while (_ValueBuilder.Length > 0
                && (_ValueBuilder[_ValueBuilder.Length - 1] == '\r' || _ValueBuilder[_ValueBuilder.Length - 1] == '\n'))
                _ValueBuilder.Length--;

            hasValue = true;
          }

          return new ValueTask<ScanEvent>(new ScanEvent(hasValue, '\0'));
        }

        var character = (char)read.Result;

        if (ValueToken.IsStructural(character))
          return new ValueTask<ScanEvent>(new ScanEvent(started, character));

        if (!started && character == ValueToken.Quote)
        {
          var terminator = ScanQuotedThenTerminatorAsync(accumulate);

          if (!terminator.IsCompletedSuccessfully)
            return AwaitThenFinishQuotedEventAsync(terminator);

          return new ValueTask<ScanEvent>(new ScanEvent(true, terminator.Result));
        }

        started = true;

        if (character != '\r' && character != '\n')
          survivesTrim = true;

        if (accumulate)
          _ValueBuilder.Append(character);
      }
    }

    private ValueTask<char> ScanQuotedThenTerminatorAsync(bool accumulate)
    {
      while (true)
      {
        var read = ReadCharacterAsync();

        if (!read.IsCompletedSuccessfully)
          return AwaitPushbackThenScanQuotedAsync(read, accumulate);

        if (read.Result < 0)
          throw new FormatException("Unterminated quoted value.");

        var character = (char)read.Result;

        if (character == ValueToken.Quote)
        {
          var next = ReadCharacterAsync();

          if (!next.IsCompletedSuccessfully)
            return AwaitThenResolveQuoteLookaheadAsync(next, accumulate);

          if (ResolveQuoteLookahead(next.Result, accumulate))
            continue;

          return ScanTerminatorAfterQuoteAsync();
        }

        if (accumulate)
          _ValueBuilder.Append(character);
      }
    }

    // A quote inside a quoted value: doubled = a literal quote (still inside the value, true);
    // anything else closes it -- push the lookahead character back into the block (it was just
    // served from [_Position - 1], so stepping back re-serves it; nothing to restore at end of
    // text) and let the terminator scan re-serve it.
    private bool ResolveQuoteLookahead(int next, bool accumulate)
    {
      if (next == ValueToken.Quote)
      {
        if (accumulate)
          _ValueBuilder.Append(ValueToken.Quote);

        return true;
      }

      if (next >= 0)
        _Position--;

      return false;
    }

    private ValueTask<char> ScanTerminatorAfterQuoteAsync()
    {
      while (true)
      {
        var read = ReadCharacterAsync();

        if (!read.IsCompletedSuccessfully)
          return AwaitPushbackThenScanTerminatorAsync(read);

        if (read.Result < 0)
        {
          _EndDelivered = true;
          return new ValueTask<char>('\0');
        }

        var character = (char)read.Result;

        if (character == '\r' || character == '\n')
          continue;

        if (ValueToken.IsStructural(character))
          return new ValueTask<char>(character);

        throw new FormatException(
          $"Unexpected '{character}' after a quoted value: expected a structural character or end of text.");
      }
    }

    // codegen: begin async-only
    //
    // The suspension continuations. A pending read is always a refill, so the pushback
    // continuations await it, step _Position back over the refilled character (end of text has
    // nothing to push back -- the re-issued refill re-answers it), and RE-ENTER the probing
    // loop, which re-serves the character synchronously with all scan state intact. The
    // lookahead site cannot re-enter (the closing-quote candidate is already consumed), so it
    // resolves through the same consume helper as the fast path and continues with whichever
    // scan comes next.
    private async ValueTask<ScanEvent> AwaitPushbackThenScanEventCoreAsync(ValueTask<int> pendingRead, bool accumulate, bool started, bool survivesTrim)
    {
      if (await pendingRead.ConfigureAwait(false) >= 0)
        _Position--;

      return await ScanEventCoreAsync(accumulate, started, survivesTrim).ConfigureAwait(false);
    }

    private async ValueTask<ScanEvent> AwaitThenFinishQuotedEventAsync(ValueTask<char> pendingTerminator)
    {
      return new ScanEvent(true, await pendingTerminator.ConfigureAwait(false));
    }

    private async ValueTask<char> AwaitPushbackThenScanQuotedAsync(ValueTask<int> pendingRead, bool accumulate)
    {
      if (await pendingRead.ConfigureAwait(false) >= 0)
        _Position--;

      return await ScanQuotedThenTerminatorAsync(accumulate).ConfigureAwait(false);
    }

    private async ValueTask<char> AwaitThenResolveQuoteLookaheadAsync(ValueTask<int> pendingLookahead, bool accumulate)
    {
      if (ResolveQuoteLookahead(await pendingLookahead.ConfigureAwait(false), accumulate))
        return await ScanQuotedThenTerminatorAsync(accumulate).ConfigureAwait(false);

      return await ScanTerminatorAfterQuoteAsync().ConfigureAwait(false);
    }

    private async ValueTask<char> AwaitPushbackThenScanTerminatorAsync(ValueTask<int> pendingRead)
    {
      if (await pendingRead.ConfigureAwait(false) >= 0)
        _Position--;

      return await ScanTerminatorAfterQuoteAsync().ConfigureAwait(false);
    }
    // codegen: end async-only

    // Serve from the block; refill with ONE awaited ReadAsync only when it drains. -1 at end of
    // text (a drained reader keeps answering 0, so post-end calls stay correct). Split along
    // the block boundary: a character already in the block is served with no state machine --
    // the refill (one await per 4096 characters) is the only async path.
    private ValueTask<int> ReadCharacterAsync()
    {
      if (_Position < _Length)
        return new ValueTask<int>(_Block[_Position++]);

      return RefillThenReadAsync();
    }

    private async ValueTask<int> RefillThenReadAsync()
    {
      _CancellationToken.ThrowIfCancellationRequested();

      _Length = await _Reader.ReadAsync(_Block, 0, _Block.Length).ConfigureAwait(false);
      _Position = 0;

      if (_Length == 0)
        return -1;

      return _Block[_Position++];
    }
  }
}
