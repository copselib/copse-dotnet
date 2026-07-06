using System;

namespace Copse.SimpleSerializer
{
  // ValueToken's reader over an in-memory payload string, shared by both string stores. Each
  // TryScanEvent delivers the next token EVENT: an optional value followed by the structural
  // character that terminated it ('\0' for end of text, delivered exactly once; afterwards the
  // scanner reports exhaustion). The caller owns all grammar decisions -- which structural
  // characters are legal where -- the scanner only knows tokens: bare (runs verbatim to the
  // next structural character; unquoted '\r'/'\n' are insignificant and a quote after the
  // token has started is literal) and quoted (opened only by a '"' FIRST in the token; '""' is
  // a literal quote; structural characters and line endings inside are value characters; after
  // the close only line endings may precede the terminator).
  //
  // Values are exposed as spans to preserve the stores' zero-copy contract: a bare run and a
  // quoted value without escapes are slices of the source; only escaped quotes ('""') and
  // line-ending-interrupted bare runs pay a copy into the reusable scratch buffer. ValueChars
  // is valid until the next scan.
  internal sealed class ValueTokenStringScanner
  {
    public ValueTokenStringScanner(string source)
    {
      _Source = source;
    }

    private readonly string _Source;

    private char[] _Scratch;
    private int _ScratchLength;
    private bool _UseScratch;

    private int _SpanStart;
    private int _SpanLength;

    private int _Cursor;
    private bool _EndDelivered;

    // The index just past the last consumed character (for error messages).
    public int Position => _Cursor;

    public ReadOnlySpan<char> ValueChars
      => _UseScratch
        ? new ReadOnlySpan<char>(_Scratch, 0, _ScratchLength)
        : _Source.AsSpan(_SpanStart, _SpanLength);

    public bool TryScanEvent(out bool hasValue, out char terminator)
    {
      hasValue = false;
      terminator = '\0';

      if (_EndDelivered)
        return false;

      _UseScratch = false;
      _ScratchLength = 0;
      _SpanLength = 0;

      var runStart = -1;
      var runEnd = -1;

      while (_Cursor < _Source.Length)
      {
        var character = _Source[_Cursor];

        if (character == '\r' || character == '\n')
        {
          _Cursor++;
          continue;
        }

        if (ValueToken.IsStructural(character))
        {
          _Cursor++;
          terminator = character;
          hasValue = FinishBare(runStart, runEnd);
          return true;
        }

        if (runStart < 0 && character == ValueToken.Quote)
        {
          _Cursor++;
          ScanQuoted();
          hasValue = true;
          terminator = ScanTerminatorAfterQuote();
          return true;
        }

        if (runStart < 0)
        {
          runStart = _Cursor;
          runEnd = _Cursor + 1;
        }
        else if (_UseScratch)
        {
          AppendScratch(character);
        }
        else if (runEnd == _Cursor)
        {
          runEnd = _Cursor + 1;
        }
        else
        {
          // A line ending interrupted the run: concatenate across it via the scratch buffer.
          SwitchToScratch(runStart, runEnd - runStart);
          AppendScratch(character);
        }

        _Cursor++;
      }

      _EndDelivered = true;
      hasValue = FinishBare(runStart, runEnd);
      return true;
    }

    private bool FinishBare(int runStart, int runEnd)
    {
      if (_UseScratch)
        return true;

      if (runStart < 0)
        return false;

      _SpanStart = runStart;
      _SpanLength = runEnd - runStart;
      return true;
    }

    // Cursor is just past the opening quote on entry, just past the closing quote on exit.
    private void ScanQuoted()
    {
      var contentStart = _Cursor;

      while (_Cursor < _Source.Length)
      {
        var character = _Source[_Cursor];

        if (character == ValueToken.Quote)
        {
          if (_Cursor + 1 < _Source.Length && _Source[_Cursor + 1] == ValueToken.Quote)
          {
            // '""' -- one literal quote; the value is no longer a contiguous slice.
            if (!_UseScratch)
              SwitchToScratch(contentStart, _Cursor - contentStart);

            AppendScratch(ValueToken.Quote);
            _Cursor += 2;
            continue;
          }

          if (!_UseScratch)
          {
            _SpanStart = contentStart;
            _SpanLength = _Cursor - contentStart;
          }

          _Cursor++;
          return;
        }

        if (_UseScratch)
          AppendScratch(character);

        _Cursor++;
      }

      throw new FormatException($"Unterminated quoted value (opening '\"' at index {contentStart - 1}).");
    }

    private char ScanTerminatorAfterQuote()
    {
      while (_Cursor < _Source.Length)
      {
        var character = _Source[_Cursor];

        if (character == '\r' || character == '\n')
        {
          _Cursor++;
          continue;
        }

        if (ValueToken.IsStructural(character))
        {
          _Cursor++;
          return character;
        }

        throw new FormatException(
          $"Unexpected '{character}' at index {_Cursor} after a quoted value: expected a structural character or end of text.");
      }

      _EndDelivered = true;
      return '\0';
    }

    private void SwitchToScratch(int sourceStart, int length)
    {
      if (_Scratch == null || _Scratch.Length <= length)
        _Scratch = new char[Math.Max(16, length * 2)];

      _Source.AsSpan(sourceStart, length).CopyTo(_Scratch);
      _ScratchLength = length;
      _UseScratch = true;
    }

    private void AppendScratch(char character)
    {
      if (_ScratchLength == _Scratch.Length)
      {
        var grown = new char[_Scratch.Length * 2];
        _Scratch.AsSpan(0, _ScratchLength).CopyTo(grown);
        _Scratch = grown;
      }

      _Scratch[_ScratchLength++] = character;
    }
  }
}
