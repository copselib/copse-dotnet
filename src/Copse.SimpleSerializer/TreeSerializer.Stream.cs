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

    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirstTree<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new PreorderStreamTreenumerable<TValue, PreorderTextStream<TValue>>(
        () => new PreorderTextStream<TValue>(readerFactory(), map));

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirstTree(Func<TextReader> readerFactory)
      => DeserializeDepthFirstTree(readerFactory, value => value);

    public static IDepthFirstTreenumerable<TValue> DeserializeDepthFirstTreeFromFile<TValue>(string path, Func<string, TValue> map)
      => DeserializeDepthFirstTree(() => File.OpenText(path), map);

    public static IDepthFirstTreenumerable<string> DeserializeDepthFirstTreeFromFile(string path)
      => DeserializeDepthFirstTreeFromFile(path, value => value);

    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstTree<TValue>(
      Func<TextReader> readerFactory,
      Func<string, TValue> map)
      => new LevelOrderStreamTreenumerable<TValue, LevelOrderTextStream<TValue>>(
        () => new LevelOrderTextStream<TValue>(readerFactory(), map));

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirstTree(Func<TextReader> readerFactory)
      => DeserializeBreadthFirstTree(readerFactory, value => value);

    public static IBreadthFirstTreenumerable<TValue> DeserializeBreadthFirstTreeFromFile<TValue>(string path, Func<string, TValue> map)
      => DeserializeBreadthFirstTree(() => File.OpenText(path), map);

    public static IBreadthFirstTreenumerable<string> DeserializeBreadthFirstTreeFromFile(string path)
      => DeserializeBreadthFirstTreeFromFile(path, value => value);

    // The Serialize (tree -> writer) surface lives in TreeSerializer.Serialize.g.cs, generated
    // from TreeSerializer.SerializeAsync.cs.
  }
}
