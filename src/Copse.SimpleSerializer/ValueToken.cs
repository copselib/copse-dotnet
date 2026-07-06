using System.IO;

namespace Copse.SimpleSerializer
{
  // The value-token layer both payload grammars share: how a node value is rendered into (and
  // recovered from) serialized text without colliding with the structural characters. Bare
  // tokens are written verbatim; a value that could be misread -- one containing a structural
  // character from EITHER grammar, a quote, a line ending, or nothing at all -- is CSV-style
  // quoted, with '""' for a literal quote. The escape set is the UNION of both grammars'
  // structural characters on purpose: each reader throws on the other grammar's characters for
  // wrong-layout detection, so a writer that left them bare would emit payloads its own reader
  // rejects; quoting them instead keeps detection sound (only UNQUOTED structure counts) and
  // makes a value serialize identically in both layouts.
  //
  // Nothing else is special. Whitespace is ordinary (bare tokens are never trimmed), and even
  // line endings are ordinary value characters to the readers EXCEPT in the two places they
  // cannot be data: an unquoted trailing run at end of input (files end in newlines) and
  // between a closing quote and its terminator. The writer quotes any value containing them,
  // so its own output never leans on that tolerance.
  //
  // Quoting was chosen over backslash escaping to preserve the string stores' zero-copy span
  // mapping: a quoted value without an embedded quote is still a contiguous slice of the
  // source, whereas any backslash-escaped character would break contiguity.
  //
  // ValueTokenStringScanner / ValueTokenStreamScanner are this layer's readers; change one,
  // check the others.
  internal static class ValueToken
  {
    public const char Quote = '"';

    public static bool IsStructural(char character)
      => character == '(' || character == ')' || character == ',' || character == ';' || character == '|';

    private static bool NeedsQuoting(string value)
    {
      if (value.Length == 0)
        return true;

      foreach (var character in value)
        if (IsStructural(character) || character == Quote || character == '\r' || character == '\n')
          return true;

      return false;
    }

    // A null map result serializes as the empty string -- the text format has no null.
    public static void Write(TextWriter writer, string value)
    {
      if (value != null && !NeedsQuoting(value))
      {
        writer.Write(value);
        return;
      }

      writer.Write(Quote);

      if (value != null)
      {
        foreach (var character in value)
        {
          if (character == Quote)
            writer.Write(Quote);

          writer.Write(character);
        }
      }

      writer.Write(Quote);
    }
  }
}
