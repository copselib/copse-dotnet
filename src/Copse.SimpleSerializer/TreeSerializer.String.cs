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
  // contract. Each treenumerator acquisition constructs a fresh store via Tree.Defer, parses
  // only as far as its traversal's frontier reaches, and is collected with it: re-enumeration
  // re-parses, the value map runs per traversal, and every traversal sees fresh instances.
  // Parse-once-replay-many is the caller's explicit escalation: Materialize() (eager capture)
  // or Memoize() (incremental capture).
  //
  // The forward-only reader/file tier -- bounded memory, single dimension -- lives in
  // TreeSerializer.Stream.cs.
  /// <summary>
  /// Header-free text serialization for trees, in two grammars: preorder,
  /// <c>"a(b(d,e),c)"</c> — a value's children in parentheses behind it — written and read by
  /// the DepthFirst methods; and level-order, <c>"a;b,c;d,e"</c> — depth levels separated by
  /// <c>;</c>, sibling groups by <c>,</c> — written and read by the BreadthFirst methods.
  /// There is no layout header: the method chosen IS the layout declaration, and a
  /// wrong-grammar input fails on its first alien structural character. Every Deserialize
  /// overload is deferred — each enumeration parses afresh and maps values afresh; call
  /// <c>Materialize()</c> or <c>Memoize()</c> on the result to parse once and replay.
  /// </summary>
  public static partial class TreeSerializer
  {
    // ----- Deserialize (string -> full ITreenumerable) -----

    /// <summary>Reads a preorder-grammar string (<c>"a(b(d,e),c)"</c>) as a tree of its raw
    /// string values. In-memory input affords random access, so the result carries both
    /// traversal dimensions. Deferred: each enumeration parses afresh.</summary>
    public static ITreenumerable<string> DeserializeDepthFirstTree(string tree)
      => DeserializeDepthFirstTree(tree, value => value);

    /// <summary>Reads a preorder-grammar string (<c>"a(b(d,e),c)"</c>) as a tree, mapping each
    /// serialized value through <paramref name="map"/>. Deferred: each enumeration parses and
    /// maps afresh.</summary>
    public static ITreenumerable<TNode> DeserializeDepthFirstTree<TNode>(string tree, Func<string, TNode> map)
    {
      SpanMap<TNode> spanMap = chars => map(chars.ToString());
      return DeserializeDepthFirstTree(tree, spanMap);
    }

    /// <summary>Reads a preorder-grammar string (<c>"a(b(d,e),c)"</c>) as a tree, mapping each
    /// serialized value from a span sliced out of the source — no intermediate value strings
    /// are allocated. Deferred: each enumeration parses and maps afresh.</summary>
    public static ITreenumerable<TNode> DeserializeDepthFirstTree<TNode>(string tree, SpanMap<TNode> map)
      => Tree.Defer(() =>
        new PreorderTreenumerable<TNode, PreorderStringStore<TNode>.Handle>(
          new PreorderStringStore<TNode>.Handle(new PreorderStringStore<TNode>(tree, map))));

    /// <summary>Reads a level-order-grammar string (<c>"a;b,c;d,e"</c>) as a tree of its raw
    /// string values. In-memory input affords random access, so the result carries both
    /// traversal dimensions. Deferred: each enumeration parses afresh.</summary>
    public static ITreenumerable<string> DeserializeBreadthFirstTree(string tree)
      => DeserializeBreadthFirstTree(tree, value => value);

    /// <summary>Reads a level-order-grammar string (<c>"a;b,c;d,e"</c>) as a tree, mapping each
    /// serialized value through <paramref name="map"/>. Deferred: each enumeration parses and
    /// maps afresh.</summary>
    public static ITreenumerable<TNode> DeserializeBreadthFirstTree<TNode>(string tree, Func<string, TNode> map)
    {
      SpanMap<TNode> spanMap = chars => map(chars.ToString());
      return DeserializeBreadthFirstTree(tree, spanMap);
    }

    /// <summary>Reads a level-order-grammar string (<c>"a;b,c;d,e"</c>) as a tree, mapping each
    /// serialized value from a span sliced out of the source — no intermediate value strings
    /// are allocated. Deferred: each enumeration parses and maps afresh.</summary>
    public static ITreenumerable<TNode> DeserializeBreadthFirstTree<TNode>(string tree, SpanMap<TNode> map)
      => Tree.Defer(() =>
        new LevelOrderTreenumerable<TNode, LevelOrderStringStore<TNode>.Handle>(
          new LevelOrderStringStore<TNode>.Handle(new LevelOrderStringStore<TNode>(tree, map))));

    // ----- Serialize (tree -> string) -----
    //
    // A depth-first-serialized tree only needs the depth-first dimension to write, and a
    // breadth-first-serialized tree only the breadth-first dimension -- so the narrow interfaces
    // are the honest receivers (a full ITreenumerable satisfies either by construction).

    /// <summary>Writes the tree as a preorder-grammar string (<c>"a(b(d,e),c)"</c>), taking
    /// each node's string as its serialized value.</summary>
    public static string SerializeDepthFirstTree(this IDepthFirstTreenumerable<string> treenumerable)
      => treenumerable.SerializeDepthFirstTree(node => node);

    /// <summary>Writes the tree as a preorder-grammar string (<c>"a(b(d,e),c)"</c>), mapping
    /// each node through <paramref name="map"/> to its serialized value.</summary>
    public static string SerializeDepthFirstTree<TNode>(this IDepthFirstTreenumerable<TNode> treenumerable, Func<TNode, string> map)
    {
      var builder = new StringBuilder();

      using (var writer = new StringWriter(builder))
        PreorderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }

    /// <summary>Writes the tree as a level-order-grammar string (<c>"a;b,c;d,e"</c>), taking
    /// each node's string as its serialized value.</summary>
    public static string SerializeBreadthFirstTree(this IBreadthFirstTreenumerable<string> treenumerable)
      => treenumerable.SerializeBreadthFirstTree(node => node);

    /// <summary>Writes the tree as a level-order-grammar string (<c>"a;b,c;d,e"</c>), mapping
    /// each node through <paramref name="map"/> to its serialized value.</summary>
    public static string SerializeBreadthFirstTree<TNode>(this IBreadthFirstTreenumerable<TNode> treenumerable, Func<TNode, string> map)
    {
      var builder = new StringBuilder();

      using (var writer = new StringWriter(builder))
        LevelOrderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }
  }
}
