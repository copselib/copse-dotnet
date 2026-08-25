using Copse.Core;
using Copse.Treenumerables;
using System;
using System.IO;

namespace Copse.SimpleSerializer
{
  // The STREAM tier of the serializer: a forward-only reader is bounded memory and single-pass,
  // so it affords only its native dimension -- deserializing one yields the NARROW interface
  // (IDepthFirstTreenumerable / IBreadthFirstTreenumerable), and a caller who wants the other
  // dimension escalates explicitly with Memoize/Materialize. There is no hidden buffering: the
  // unaffordable dimension is simply not on the returned type.
  //
  // Each treenumerator acquisition opens a fresh reader via the factory, owns it, and disposes
  // it (the treenumerator's Dispose is the release point); re-enumeration re-reads the source --
  // the standard lazy contract. The string tier (random-access, full ITreenumerable) lives in
  // TreeSerializer.String.cs.
  public static partial class TreeSerializer
  {
    // ----- Deserialize (reader factory / file -> narrow) -----

    public static IDepthFirstTreenumerable<TNode> DeserializeDepthFirstTree<TNode>(
      Func<TextReader> readerFactory,
      Func<string, TNode> map)
      => new PreorderStreamTreenumerable<TNode, PreorderTextStream<TNode>>(
        () => new PreorderTextStream<TNode>(readerFactory(), map));

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirstTree(Func<TextReader> readerFactory)
      => DeserializeDepthFirstTree(readerFactory, value => value);

    public static IDepthFirstTreenumerable<TNode> DeserializeDepthFirstTreeFromFile<TNode>(string path, Func<string, TNode> map)
      => DeserializeDepthFirstTree(() => File.OpenText(path), map);

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirstTreeFromFile(string path)
      => DeserializeDepthFirstTreeFromFile(path, value => value);

    public static IBreadthFirstTreenumerable<TNode> DeserializeBreadthFirstTree<TNode>(
      Func<TextReader> readerFactory,
      Func<string, TNode> map)
      => new LevelOrderStreamTreenumerable<TNode, LevelOrderTextStream<TNode>>(
        () => new LevelOrderTextStream<TNode>(readerFactory(), map));

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirstTree(Func<TextReader> readerFactory)
      => DeserializeBreadthFirstTree(readerFactory, value => value);

    public static IBreadthFirstTreenumerable<TNode> DeserializeBreadthFirstTreeFromFile<TNode>(string path, Func<string, TNode> map)
      => DeserializeBreadthFirstTree(() => File.OpenText(path), map);

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeFromFile(string path)
      => DeserializeBreadthFirstTreeFromFile(path, value => value);

    // The Serialize (tree -> writer) surface lives in TreeSerializer.Serialize.g.cs, generated
    // from TreeSerializer.SerializeAsync.cs.
  }
}
