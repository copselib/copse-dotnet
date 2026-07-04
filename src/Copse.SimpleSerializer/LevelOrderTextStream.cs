using System;
using System.IO;
using System.Text;

namespace Copse.SimpleSerializer
{
  // Forward-only ILevelOrderStream over the bft payload grammar ("a;b,c;d,e"): level-order
  // values in per-parent child groups -- ',' separates values within a family, '|' terminates a
  // family within its level, ';' terminates the last family of a level (the generation mark;
  // structurally equivalent to '|' on read, kept for human readability and future validation).
  // Trailing empty families are elided by the writer: end of text means every remaining group
  // is empty.
  //
  // SkipGroupRemainder honors the skip contract: discarded values are counted (positions are
  // load-bearing) but never accumulated or mapped.
  //
  // Owns its reader; disposing the stream disposes the reader.
  internal sealed class LevelOrderTextStream<TValue> : ILevelOrderStream<TValue>
  {
    public LevelOrderTextStream(TextReader reader, Func<string, TValue> map)
    {
      _Reader = reader;
      _Map = map;
    }

    private readonly TextReader _Reader;
    private readonly Func<string, TValue> _Map;
    private readonly StringBuilder _ValueBuilder = new StringBuilder();

    private bool _GroupEnded;
    private bool _Exhausted;

    public bool TryReadNextInGroup(out TValue value)
    {
      value = default;

      if (_GroupEnded || _Exhausted)
        return false;

      _ValueBuilder.Clear();
      var hasChars = false;

      while (true)
      {
        var read = _Reader.Read();

        if (read < 0)
        {
          _Exhausted = true;
          _GroupEnded = true;

          if (hasChars)
          {
            value = _Map(_ValueBuilder.ToString());
            return true;
          }

          return false;
        }

        var character = (char)read;

        switch (character)
        {
          case ',':
            if (hasChars)
            {
              value = _Map(_ValueBuilder.ToString());
              return true;
            }
            break;

          case '|':
          case ';':
            _GroupEnded = true;

            if (hasChars)
            {
              value = _Map(_ValueBuilder.ToString());
              return true;
            }

            return false;

          case '\n':
          case '\r':
            break;

          default:
            _ValueBuilder.Append(character);
            hasChars = true;
            break;
        }
      }
    }

    public int SkipGroupRemainder()
    {
      if (_GroupEnded || _Exhausted)
        return 0;

      var count = 0;
      var hasChars = false;

      while (true)
      {
        var read = _Reader.Read();

        if (read < 0)
        {
          _Exhausted = true;
          _GroupEnded = true;

          if (hasChars)
            count++;

          return count;
        }

        var character = (char)read;

        switch (character)
        {
          case ',':
            if (hasChars)
            {
              count++;
              hasChars = false;
            }
            break;

          case '|':
          case ';':
            _GroupEnded = true;

            if (hasChars)
              count++;

            return count;

          case '\n':
          case '\r':
            break;

          default:
            hasChars = true;
            break;
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
