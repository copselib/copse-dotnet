using Copse.Core;
using Copse.Treenumerables;
using System;
using System.IO;
using System.Text;

namespace Copse.SimpleSerializer
{
  // The STRING tier of the serializer: an in-memory string is its own random-access buffer, so
  // deserializing one yields a full ITreenumerable (both dimensions honest) -- the caller names
  // the stored layout by choosing DeserializeDepthFirstTree (preorder grammar, "a(b(d,e),c)") or
  // DeserializeBreadthFirstTree (level-order groups grammar, "a;b,c;d,e"). There is NO layout
  // header: the method IS the layout declaration, and a wrong-layout string fails fast on the
  // first alien structural character (see the string stores).
  //
  // Every Deserialize overload, both tiers, has the SAME schedule -- Defer, the standard lazy
  // contract (unified 2026-08-03; the string tier previously shared one growing store across
  // every treenumerator of a result, an undisclosed Memoize schedule selected by overload
  // resolution). Each treenumerator acquisition constructs a fresh store via Tree.Defer, parses
  // only as far as its traversal's frontier reaches, and is collected with it: re-enumeration
  // re-parses, the value map runs per traversal, and every traversal sees fresh instances.
  // Parse-once-replay-many is the caller's explicit escalation: Materialize() (eager capture)
  // or Memoize() (incremental capture).
  //
  // The forward-only reader/file tier -- bounded memory, single dimension -- lives in
  // TreeSerializer.Stream.cs.
  public static partial class TreeSerializer
  {
    // ----- Deserialize (string -> full ITreenumerable) -----

    public static ITreenumerable<string> DeserializeDepthFirstTree(string tree)
      => DeserializeDepthFirstTree(tree, value => value);

    public static ITreenumerable<TValue> DeserializeDepthFirstTree<TValue>(string tree, Func<string, TValue> map)
    {
      SpanMap<TValue> spanMap = chars => map(chars.ToString());
      return DeserializeDepthFirstTree(tree, spanMap);
    }

    // Span overload: the map receives each value as a slice of the source (no intermediate
    // string), so deserializing into non-string values allocates no value strings at all.
    public static ITreenumerable<TValue> DeserializeDepthFirstTree<TValue>(string tree, SpanMap<TValue> map)
      => Tree.Defer(() =>
        new PreorderTreenumerable<TValue, PreorderStringStore<TValue>.Handle>(
          new PreorderStringStore<TValue>.Handle(new PreorderStringStore<TValue>(tree, map))));

    public static ITreenumerable<string> DeserializeBreadthFirstTree(string tree)
      => DeserializeBreadthFirstTree(tree, value => value);

    public static ITreenumerable<TValue> DeserializeBreadthFirstTree<TValue>(string tree, Func<string, TValue> map)
    {
      SpanMap<TValue> spanMap = chars => map(chars.ToString());
      return DeserializeBreadthFirstTree(tree, spanMap);
    }

    public static ITreenumerable<TValue> DeserializeBreadthFirstTree<TValue>(string tree, SpanMap<TValue> map)
      => Tree.Defer(() =>
        new LevelOrderTreenumerable<TValue, LevelOrderStringStore<TValue>.Handle>(
          new LevelOrderStringStore<TValue>.Handle(new LevelOrderStringStore<TValue>(tree, map))));

    // ----- Serialize (tree -> string) -----
    //
    // A depth-first-serialized tree only needs the depth-first dimension to write, and a
    // breadth-first-serialized tree only the breadth-first dimension -- so the narrow interfaces
    // are the honest receivers (a full ITreenumerable satisfies either by construction).

    public static string SerializeDepthFirstTree(this IDepthFirstTreenumerable<string> treenumerable)
      => treenumerable.SerializeDepthFirstTree(node => node);

    public static string SerializeDepthFirstTree<TNode>(this IDepthFirstTreenumerable<TNode> treenumerable, Func<TNode, string> map)
    {
      var builder = new StringBuilder();

      using (var writer = new StringWriter(builder))
        PreorderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }

    public static string SerializeBreadthFirstTree(this IBreadthFirstTreenumerable<string> treenumerable)
      => treenumerable.SerializeBreadthFirstTree(node => node);

    public static string SerializeBreadthFirstTree<TNode>(this IBreadthFirstTreenumerable<TNode> treenumerable, Func<TNode, string> map)
    {
      var builder = new StringBuilder();

      using (var writer = new StringWriter(builder))
        LevelOrderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }
  }
}
