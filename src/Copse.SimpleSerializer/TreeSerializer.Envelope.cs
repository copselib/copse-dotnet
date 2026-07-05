using Copse.Core;
using Copse.Treenumerables;
using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // The enveloped (file/stream) surface: one layout per file behind a dependency-free header
  // line, then that layout's payload -- the paren grammar for dft, the level-order groups
  // grammar for bft. Pure entry points; the moving parts live beside their concerns:
  // EnvelopeHeader (header line + layout validation), PreorderTextWriter/PreorderTextStream and
  // LevelOrderTextWriter/LevelOrderTextStream (one writer/reader pair per grammar).
  //
  // Deserialization is layout-specific by METHOD, not by argument: the caller's downstream code
  // is compiled against one narrow interface, so the entry point states the expected layout and
  // the header check turns that into a fast-failing assertion at treenumerator acquisition.
  // ReadHeader is the dispatch hatch for files of unknown layout. Each treenumerator
  // acquisition opens a fresh reader via the factory, owns it, and disposes it; re-enumeration
  // re-reads the source.
  //
  // Reserved characters (values must not contain them): '(' ')' ',' '|' ';' and line breaks.
  public static partial class TreeSerializer
  {
    // ----- Serialize -----

    public static void Serialize<TNode>(
      this ITreenumerable<TNode> treenumerable,
      TextWriter writer,
      TreeTraversalStrategy layout,
      Func<TNode, string> map)
    {
      EnvelopeHeader.Write(writer, layout);

      if (layout == TreeTraversalStrategy.DepthFirst)
        PreorderTextWriter.WritePayload(treenumerable, writer, map);
      else
        LevelOrderTextWriter.WritePayload(treenumerable, writer, map);
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

    // Narrow overloads: the layout is the source's native dimension, so a single-dimension
    // source serializes without any cross-order cost (and without naming a layout).
    public static void Serialize<TNode>(this IDepthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map)
    {
      EnvelopeHeader.Write(writer, TreeTraversalStrategy.DepthFirst);
      PreorderTextWriter.WritePayload(treenumerable, writer, map);
    }

    public static void Serialize(this IDepthFirstTreenumerable<string> treenumerable, TextWriter writer)
      => Serialize(treenumerable, writer, node => node);

    public static void Serialize<TNode>(this IBreadthFirstTreenumerable<TNode> treenumerable, TextWriter writer, Func<TNode, string> map)
    {
      EnvelopeHeader.Write(writer, TreeTraversalStrategy.BreadthFirst);
      LevelOrderTextWriter.WritePayload(treenumerable, writer, map);
    }

    public static void Serialize(this IBreadthFirstTreenumerable<string> treenumerable, TextWriter writer)
      => Serialize(treenumerable, writer, node => node);

    // ----- Deserialize -----

    // The dispatch hatch for files of unknown layout: reads and validates the header line,
    // returning the layout axis; the reader is left positioned at the payload.
    public static TreeTraversalStrategy ReadHeader(TextReader reader)
      => EnvelopeHeader.Read(reader);

    // Streaming lazy deserialization of a dft-layout envelope. Returns the narrow interface: a
    // forward-only source affords only the depth-first dimension (O(depth) memory; the
    // breadth-first request does not typecheck -- escalate via Memoize/Materialize).
    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirst<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new PreorderStreamTreenumerable<TValue, PreorderTextStream<TValue>>(
        () => new PreorderTextStream<TValue>(EnvelopeHeader.OpenValidated(readerFactory, TreeTraversalStrategy.DepthFirst), map));

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirst(Func<TextReader> readerFactory)
      => DeserializeDepthFirst(readerFactory, value => value);

    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirstFromFile<TValue>(string path, Func<string, TValue> map)
      => DeserializeDepthFirst(() => File.OpenText(path), map);

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirstFromFile(string path)
      => DeserializeDepthFirstFromFile(path, value => value);

    // The breadth-first dual over a bft-layout envelope (O(width) memory; the depth-first
    // dimension has no bounded strategy over a one-pass level-order source, so it does not
    // typecheck).
    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirst<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new LevelOrderStreamTreenumerable<TValue, LevelOrderTextStream<TValue>>(
        () => new LevelOrderTextStream<TValue>(EnvelopeHeader.OpenValidated(readerFactory, TreeTraversalStrategy.BreadthFirst), map));

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirst(Func<TextReader> readerFactory)
      => DeserializeBreadthFirst(readerFactory, value => value);

    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstFromFile<TValue>(string path, Func<string, TValue> map)
      => DeserializeBreadthFirst(() => File.OpenText(path), map);

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirstFromFile(string path)
      => DeserializeBreadthFirstFromFile(path, value => value);
  }
}
