using Copse.Core;
using Copse.Treenumerables;
using System;
using System.Text;

namespace Copse.SimpleSerializer
{
  // The STRING tier of the serializer: an in-memory string is its own random-access buffer, so
  // deserializing one yields a full ITreenumerable (both dimensions honest) -- the caller names
  // the stored layout by choosing DeserializeDepthFirstTree (preorder grammar, "a(b(d,e),c)") or
  // DeserializeBreadthFirstTree (level-order groups grammar, "a;b,c;d,e"). There is NO layout
  // header: the method IS the layout declaration, and a wrong-layout string fails fast on the
  // first alien structural character (see the string stores). Parsing is lazy -- composing
  // touches nothing; each pull parses only as far as the traversal's frontier reaches; one
  // shared store serves every treenumerator of the same result.
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
      => new PreorderTreenumerable<TValue, PreorderStringStore<TValue>.Handle>(
        new PreorderStringStore<TValue>.Handle(new PreorderStringStore<TValue>(tree, map)));

    public static ITreenumerable<string> DeserializeBreadthFirstTree(string tree)
      => DeserializeBreadthFirstTree(tree, value => value);

    public static ITreenumerable<TValue> DeserializeBreadthFirstTree<TValue>(string tree, Func<string, TValue> map)
    {
      SpanMap<TValue> spanMap = chars => map(chars.ToString());
      return DeserializeBreadthFirstTree(tree, spanMap);
    }

    public static ITreenumerable<TValue> DeserializeBreadthFirstTree<TValue>(string tree, SpanMap<TValue> map)
      => new LevelOrderTreenumerable<TValue, LevelOrderStringStore<TValue>.Handle>(
        new LevelOrderStringStore<TValue>.Handle(new LevelOrderStringStore<TValue>(tree, map)));

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

      using (var writer = new System.IO.StringWriter(builder))
        PreorderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }

    public static string SerializeBreadthFirstTree(this IBreadthFirstTreenumerable<string> treenumerable)
      => treenumerable.SerializeBreadthFirstTree(node => node);

    public static string SerializeBreadthFirstTree<TNode>(this IBreadthFirstTreenumerable<TNode> treenumerable, Func<TNode, string> map)
    {
      var builder = new StringBuilder();

      using (var writer = new System.IO.StringWriter(builder))
        LevelOrderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }
  }
}
