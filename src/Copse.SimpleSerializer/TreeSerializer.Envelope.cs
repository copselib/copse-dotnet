using Copse.Core;
using Copse.Treenumerables;
using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // The enveloped (file/stream) serializer surface: one layout per file, declared by a tiny
  // dependency-free header line ("copse/1;layout=dft" or "copse/1;layout=bft"), followed by
  // that layout's payload -- the terse paren grammar for dft, the level-order groups grammar
  // for bft (see LevelOrderTextStream). See docs/TRAVERSAL_DIMENSION_SPLIT.md, "Proposed
  // serializer surface".
  //
  // Deserialization is layout-specific by METHOD, not by argument: the caller's downstream code
  // is compiled against one narrow interface, so the entry point states the expected layout and
  // the header check turns that into a fast-failing assertion at treenumerator acquisition.
  // ReadHeader is the dispatch hatch for files of unknown layout.
  //
  // Reserved characters (values must not contain them): '(' ')' ',' '|' ';' and line breaks.
  public static partial class TreeSerializer
  {
    private const string HeaderPrefix = "copse/1;layout=";
    private const string DepthFirstLayoutToken = "dft";
    private const string BreadthFirstLayoutToken = "bft";

    // ----- Serialize -----

    public static void Serialize<TNode>(
      this ITreenumerable<TNode> treenumerable,
      TextWriter writer,
      TreeTraversalStrategy layout,
      Func<TNode, string> map)
    {
      writer.Write(HeaderPrefix);
      writer.Write(layout == TreeTraversalStrategy.DepthFirst ? DepthFirstLayoutToken : BreadthFirstLayoutToken);
      writer.Write('\n');

      if (layout == TreeTraversalStrategy.DepthFirst)
        WriteDepthFirstPayload(treenumerable, writer, map);
      else
        WriteBreadthFirstPayload(treenumerable, writer, map);
    }

    public static void Serialize(this ITreenumerable<string> treenumerable, TextWriter writer, TreeTraversalStrategy layout)
      => Serialize(treenumerable, writer, layout, node => node);

    public static string Serialize<TNode>(this ITreenumerable<TNode> treenumerable, TreeTraversalStrategy layout, Func<TNode, string> map)
    {
      using (var writer = new StringWriter())
      {
        Serialize(treenumerable, writer, layout, map);
        return writer.ToString();
      }
    }

    public static string Serialize(this ITreenumerable<string> treenumerable, TreeTraversalStrategy layout)
      => Serialize(treenumerable, layout, node => node);

    // The existing bare dft grammar, retargeted from StringBuilder to TextWriter: emit each
    // node on its first visit, with paren/comma structure derived from depth deltas.
    private static void WriteDepthFirstPayload<TNode>(ITreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map)
    {
      using (var treenumerator = treenumerable.GetDepthFirstTreenumerator())
      {
        int previousDepth = -1;

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.VisitCount != 1)
            continue;

          var depth = treenumerator.Position.Depth;

          if (previousDepth != -1)
          {
            if (depth > previousDepth)
            {
              writer.Write('(');
            }
            else
            {
              for (int i = 0; i < previousDepth - depth; i++)
                writer.Write(')');

              writer.Write(',');
            }
          }

          writer.Write(map(treenumerator.Node));

          previousDepth = depth;
        }

        while (previousDepth-- > 0)
          writer.Write(')');
      }
    }

    // The bft grammar writer, O(1) state: a node's VALUE is emitted when it is scheduled
    // (inside its parent's family), and its family terminator when it is first visited --
    // '|' within a level, ';' at a level boundary -- exactly the schedule/visit split
    // serialized (Jason's BreadthFirstTreeEnumerable tokenization rendered to text).
    // Separators buffer until the next value; at end of stream they drop, eliding all
    // trailing empty families.
    private static void WriteBreadthFirstPayload<TNode>(ITreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map)
    {
      using (var treenumerator = treenumerable.GetBreadthFirstTreenumerator())
      {
        var pendingSeparators = new System.Collections.Generic.Queue<char>();
        var currentLevelDepth = -1;
        var valueOpenInFamily = false;

        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode == TreenumeratorMode.SchedulingNode)
          {
            if (pendingSeparators.Count > 0)
            {
              while (pendingSeparators.Count > 0)
                writer.Write(pendingSeparators.Dequeue());

              valueOpenInFamily = false;
            }

            if (valueOpenInFamily)
              writer.Write(',');

            writer.Write(map(treenumerator.Node));

            valueOpenInFamily = true;
          }
          else if (treenumerator.VisitCount == 1)
          {
            if (treenumerator.Position.Depth == currentLevelDepth)
            {
              pendingSeparators.Enqueue('|');
            }
            else
            {
              pendingSeparators.Enqueue(';');
              currentLevelDepth++;
            }
          }
        }

        // Pending separators drop here: trailing empty families elide, and end of text means
        // "everything remaining is empty".
      }
    }

    // ----- Deserialize -----

    // Reads and validates the header line, returning the layout axis. Consumes through the
    // newline; the reader is left positioned at the payload. The dispatch hatch for callers
    // who must branch on a file they didn't write.
    public static TreeTraversalStrategy ReadHeader(TextReader reader)
    {
      var builder = new System.Text.StringBuilder();

      int read;
      while ((read = reader.Read()) >= 0 && (char)read != '\n')
        if ((char)read != '\r')
          builder.Append((char)read);

      var header = builder.ToString();

      if (!header.StartsWith(HeaderPrefix, StringComparison.Ordinal))
        throw new FormatException($"Expected a Copse envelope header (\"{HeaderPrefix}dft|bft\"), found: \"{header}\".");

      var layoutToken = header.Substring(HeaderPrefix.Length);

      switch (layoutToken)
      {
        case DepthFirstLayoutToken: return TreeTraversalStrategy.DepthFirst;
        case BreadthFirstLayoutToken: return TreeTraversalStrategy.BreadthFirst;
        default: throw new FormatException($"Unknown layout \"{layoutToken}\" in Copse envelope header.");
      }
    }

    // Streaming lazy deserialization of a dft-layout envelope. Returns the narrow interface:
    // a forward-only source affords only the depth-first dimension (O(depth) memory; the
    // breadth-first request does not typecheck -- escalate via Memoize/Materialize). Each
    // treenumerator acquisition opens a fresh reader via the factory, validates the header,
    // owns the reader, and disposes it; re-enumeration re-reads the source.
    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirst<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new PreorderStreamTreenumerable<TValue, PreorderTextStream<TValue>>(
        () => new PreorderTextStream<TValue>(OpenValidated(readerFactory, TreeTraversalStrategy.DepthFirst), map));

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirst(Func<TextReader> readerFactory)
      => DeserializeDepthFirst(readerFactory, value => value);

    // Single-shot convenience: the first treenumerator takes ownership of the reader; a second
    // acquisition throws. Pass a reader factory to re-enumerate.
    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirst<TValue>(TextReader reader, Func<string, TValue> map)
      => DeserializeDepthFirst(SingleShot(reader), map);

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirst(TextReader reader)
      => DeserializeDepthFirst(reader, value => value);

    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirstFromFile<TValue>(string path, Func<string, TValue> map)
      => DeserializeDepthFirst(() => File.OpenText(path), map);

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirstFromFile(string path)
      => DeserializeDepthFirstFromFile(path, value => value);

    // Streaming lazy deserialization of a bft-layout envelope: the breadth-first dual
    // (O(width) memory; the depth-first dimension has no bounded strategy over a one-pass
    // level-order source, so it does not typecheck).
    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirst<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new LevelOrderStreamTreenumerable<TValue, LevelOrderTextStream<TValue>>(
        () => new LevelOrderTextStream<TValue>(OpenValidated(readerFactory, TreeTraversalStrategy.BreadthFirst), map));

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirst(Func<TextReader> readerFactory)
      => DeserializeBreadthFirst(readerFactory, value => value);

    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirst<TValue>(TextReader reader, Func<string, TValue> map)
      => DeserializeBreadthFirst(SingleShot(reader), map);

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirst(TextReader reader)
      => DeserializeBreadthFirst(reader, value => value);

    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstFromFile<TValue>(string path, Func<string, TValue> map)
      => DeserializeBreadthFirst(() => File.OpenText(path), map);

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirstFromFile(string path)
      => DeserializeBreadthFirstFromFile(path, value => value);

    private static TextReader OpenValidated(Func<TextReader> readerFactory, TreeTraversalStrategy expectedLayout)
    {
      var reader = readerFactory();

      try
      {
        var layout = ReadHeader(reader);

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

    private static Func<TextReader> SingleShot(TextReader reader)
    {
      var used = false;

      return () =>
      {
        if (used)
          throw new InvalidOperationException(
            "A single-reader deserialize supports one enumeration; pass a reader factory to re-enumerate.");

        used = true;
        return reader;
      };
    }
  }
}
