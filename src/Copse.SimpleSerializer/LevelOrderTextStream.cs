using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // Forward-only ILevelOrderStream over the bft payload grammar ("a;b,c;d,e"): level-order
  // values in per-parent child groups -- ',' separates values within a family, '|' terminates a
  // family within its level, ';' terminates the last family of a level (the generation mark;
  // structurally equivalent to '|' on read, kept for human readability and future validation).
  // Trailing empty families are elided by the writer: end of text means every remaining group
  // is empty. Values ride the shared value-token layer (ValueTokenStreamScanner): quoted values
  // may contain ANY character; unquoted trailing line endings at end of input are ignored
  // (files end in newlines).
  //
  // Struct-return read seam (TryReadNextInGroup -> LevelOrderRead): the shape shared with the async
  // twin (AsyncLevelOrderTextStream). SkipGroupRemainder honors the skip contract: discarded values
  // are counted (positions are load-bearing) but never accumulated or mapped.
  //
  // Owns its reader; disposing the stream disposes the reader.
  internal sealed class LevelOrderTextStream<TValue> : ILevelOrderStream<TValue>
  {
    public LevelOrderTextStream(TextReader reader, Func<string, TValue> map)
    {
      _Reader = reader;
      _Scanner = new ValueTokenStreamScanner(reader);
      _Map = map;
    }

    private readonly TextReader _Reader;
    private readonly ValueTokenStreamScanner _Scanner;
    private readonly Func<string, TValue> _Map;

    private bool _GroupEnded;
    private bool _Exhausted;

    public LevelOrderRead<TValue> TryReadNextInGroup()
    {
      if (_GroupEnded || _Exhausted)
        return default;

      while (true)
      {
        if (!_Scanner.TryScanEvent(accumulate: true, out var hasValue, out var terminator))
        {
          _Exhausted = true;
          _GroupEnded = true;
          return default;
        }

        switch (terminator)
        {
          case ',':
            if (hasValue)
              return new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));

            break;

          case '|':
          case ';':
            _GroupEnded = true;

            if (hasValue)
              return new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));

            return default;

          case '(':
          case ')':
            throw new FormatException(
              $"Unexpected '{terminator}': this is a depth-first structural character, so the source " +
              "is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

          default: // end of text
            _Exhausted = true;
            _GroupEnded = true;

            if (hasValue)
              return new LevelOrderRead<TValue>(_Map(_Scanner.GetValue()));

            return default;
        }
      }
    }

    public int SkipGroupRemainder()
    {
      if (_GroupEnded || _Exhausted)
        return 0;

      var count = 0;

      while (true)
      {
        if (!_Scanner.TryScanEvent(accumulate: false, out var hasValue, out var terminator))
        {
          _Exhausted = true;
          _GroupEnded = true;
          return count;
        }

        switch (terminator)
        {
          case ',':
            if (hasValue)
              count++;

            break;

          case '|':
          case ';':
            _GroupEnded = true;

            if (hasValue)
              count++;

            return count;

          case '(':
          case ')':
            throw new FormatException(
              $"Unexpected '{terminator}': this is a depth-first structural character, so the source " +
              "is not a breadth-first-serialized tree (use DeserializeDepthFirstTree).");

          default: // end of text
            _Exhausted = true;
            _GroupEnded = true;

            if (hasValue)
              count++;

            return count;
        }
      }
    }

    public bool TryMoveToNextGroup()
    {
      if (_Exhausted)
        return false;

      if (!_GroupEnded)
        throw new InvalidOperationException("The current group must be finished (read or skipped to its end) before advancing.");

      _GroupEnded = false;
      return true;
    }

    public void Dispose() => _Reader.Dispose();
  }
}
