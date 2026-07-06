using System;
using System.IO;
using System.Text;

namespace Copse.SimpleSerializer
{
  // ValueToken's reader over a forward-only TextReader, shared by both text streams. The same
  // event contract as ValueTokenStringScanner -- an optional value, then the structural
  // character that terminated it ('\0' at end of text, delivered exactly once) -- pulled one
  // character at a time. accumulate: false honors the skip contract: the token is consumed and
  // reported but its characters are never buffered, so a skip costs I/O only. The one-character
  // pushback (needed to tell '""' from a closing quote) replaces TextReader.Peek, which not
  // every reader implements.
  //
  // Does NOT own the reader; the enclosing stream does.
  internal sealed class ValueTokenStreamScanner
  {
    public ValueTokenStreamScanner(TextReader reader)
    {
      _Reader = reader;
    }

    private readonly TextReader _Reader;
    private readonly StringBuilder _ValueBuilder = new StringBuilder();

    private int _Pushback = -1;
    private bool _EndDelivered;

    // The value of the last accumulated event with hasValue = true; valid until the next scan.
    public string GetValue() => _ValueBuilder.ToString();

    public bool TryScanEvent(bool accumulate, out bool hasValue, out char terminator)
    {
      hasValue = false;
      terminator = '\0';

      if (_EndDelivered)
        return false;

      _ValueBuilder.Clear();
      var started = false;

      while (true)
      {
        var read = ReadCharacter();

        if (read < 0)
        {
          _EndDelivered = true;
          hasValue = started;
          return true;
        }

        var character = (char)read;

        if (character == '\r' || character == '\n')
          continue;

        if (ValueToken.IsStructural(character))
        {
          terminator = character;
          hasValue = started;
          return true;
        }

        if (!started && character == ValueToken.Quote)
        {
          ScanQuoted(accumulate);
          hasValue = true;
          terminator = ScanTerminatorAfterQuote();
          return true;
        }

        started = true;

        if (accumulate)
          _ValueBuilder.Append(character);
      }
    }

    private void ScanQuoted(bool accumulate)
    {
      while (true)
      {
        var read = ReadCharacter();

        if (read < 0)
          throw new FormatException("Unterminated quoted value.");

        var character = (char)read;

        if (character == ValueToken.Quote)
        {
          var next = ReadCharacter();

          if (next == ValueToken.Quote)
          {
            if (accumulate)
              _ValueBuilder.Append(ValueToken.Quote);

            continue;
          }

          if (next >= 0)
            _Pushback = next;

          return;
        }

        if (accumulate)
          _ValueBuilder.Append(character);
      }
    }

    private char ScanTerminatorAfterQuote()
    {
      while (true)
      {
        var read = ReadCharacter();

        if (read < 0)
        {
          _EndDelivered = true;
          return '\0';
        }

        var character = (char)read;

        if (character == '\r' || character == '\n')
          continue;

        if (ValueToken.IsStructural(character))
          return character;

        throw new FormatException(
          $"Unexpected '{character}' after a quoted value: expected a structural character or end of text.");
      }
    }

    private int ReadCharacter()
    {
      if (_Pushback >= 0)
      {
        var pending = _Pushback;
        _Pushback = -1;
        return pending;
      }

      return _Reader.Read();
    }
  }
}
