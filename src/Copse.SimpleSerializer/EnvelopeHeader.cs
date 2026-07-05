using Copse.Core;
using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // The envelope's header line ("copse/1;layout=dft" or "copse/1;layout=bft"): one layout per
  // file, declared up front, dependency-free. Owns writing, reading, and the
  // expected-layout validation the layout-typed deserialize entry points rely on.
  internal static class EnvelopeHeader
  {
    private const string Prefix = "copse/1;layout=";
    private const string DepthFirstToken = "dft";
    private const string BreadthFirstToken = "bft";

    public static void Write(TextWriter writer, TreeTraversalStrategy layout)
    {
      writer.Write(Prefix);
      writer.Write(layout == TreeTraversalStrategy.DepthFirst ? DepthFirstToken : BreadthFirstToken);
      writer.Write('\n');
    }

    // Reads and validates the header line, returning the layout axis. Consumes through the
    // newline; the reader is left positioned at the payload.
    public static TreeTraversalStrategy Read(TextReader reader)
    {
      var builder = new System.Text.StringBuilder();

      int read;
      while ((read = reader.Read()) >= 0 && (char)read != '\n')
        if ((char)read != '\r')
          builder.Append((char)read);

      var header = builder.ToString();

      if (!header.StartsWith(Prefix, StringComparison.Ordinal))
        throw new FormatException($"Expected a Copse envelope header (\"{Prefix}dft|bft\"), found: \"{header}\".");

      var layoutToken = header.Substring(Prefix.Length);

      switch (layoutToken)
      {
        case DepthFirstToken: return TreeTraversalStrategy.DepthFirst;
        case BreadthFirstToken: return TreeTraversalStrategy.BreadthFirst;
        default: throw new FormatException($"Unknown layout \"{layoutToken}\" in Copse envelope header.");
      }
    }

    // Sniffs a STRING for the envelope header: true (with the layout axis and the payload's
    // start index) when present; false for a bare payload, which by convention is the terse
    // dft grammar.
    public static bool TryRead(string tree, out TreeTraversalStrategy layout, out int payloadStart)
    {
      layout = default;
      payloadStart = 0;

      if (!tree.StartsWith(Prefix, StringComparison.Ordinal))
        return false;

      var tokenStart = Prefix.Length;
      var lineEnd = tree.IndexOf('\n', tokenStart);
      var tokenEnd = lineEnd < 0 ? tree.Length : lineEnd;

      var token = tree.Substring(tokenStart, tokenEnd - tokenStart).TrimEnd('\r');

      switch (token)
      {
        case DepthFirstToken: layout = TreeTraversalStrategy.DepthFirst; break;
        case BreadthFirstToken: layout = TreeTraversalStrategy.BreadthFirst; break;
        default: throw new FormatException($"Unknown layout \"{token}\" in Copse envelope header.");
      }

      payloadStart = lineEnd < 0 ? tree.Length : lineEnd + 1;
      return true;
    }

    // Opens a reader via the factory and validates that the stored layout matches the entry
    // point's expectation -- the fast-failing assertion behind DeserializeDepthFirst/
    // DeserializeBreadthFirst. The reader is disposed on any failure.
    public static TextReader OpenValidated(Func<TextReader> readerFactory, TreeTraversalStrategy expectedLayout)
    {
      var reader = readerFactory();

      try
      {
        var layout = Read(reader);

        if (layout != expectedLayout)
          throw new InvalidOperationException(
            $"The envelope stores the {(layout == TreeTraversalStrategy.DepthFirst ? "dft" : "bft")} layout; " +
            $"use {(layout == TreeTraversalStrategy.DepthFirst ? "DeserializeDepthFirst" : "DeserializeBreadthFirst")} " +
            "(check ReadHeader to dispatch on unknown files).");

        return reader;
      }
      catch
      {
        reader.Dispose();
        throw;
      }
    }
  }
}
