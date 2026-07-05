using Copse.Core;
using Copse.Treenumerables;
using System;
using System.Text;

namespace Copse.SimpleSerializer
{
  public static partial class TreeSerializer
  {
    // The string surface: LAZY deserialization of any serialized string -- a bare terse payload
    // ("a(b,c)", by convention the dft grammar) or an enveloped string in either layout. A
    // string is its own random-access buffer, so the result is always a full ITreenumerable
    // (both dimensions honest) regardless of stored layout; the layout only decides which
    // dimension is the native sequential one. Nothing is parsed at compose time: each pull
    // parses exactly as far as the traversal's frontier demands, the value map runs once per
    // node ever reached, and one lazily-growing store is shared by every treenumerator of the
    // same result (parse once, replay many).
    public static ITreenumerable<string> Deserialize(string tree)
      => Deserialize(tree, value => value);

    public static ITreenumerable<TValue> Deserialize<TValue>(
      string tree,
      Func<string, TValue> map)
    {
      // Adapt the string map to a span map: each value is materialized once (chars.ToString()).
      SpanMap<TValue> spanMap = chars => map(chars.ToString());
      return Deserialize(tree, spanMap);
    }

    // Span overload: the map receives each value as a slice of the source text (no intermediate
    // string), so deserializing into non-string values (e.g. int.Parse(chars)) allocates no value
    // strings at all.
    public static ITreenumerable<TValue> Deserialize<TValue>(
      string tree,
      SpanMap<TValue> map)
    {
      if (EnvelopeHeader.TryRead(tree, out var layout, out var payloadStart)
        && layout == TreeTraversalStrategy.BreadthFirst)
        return new LevelOrderTreenumerable<TValue, LevelOrderStringStore<TValue>.Handle>(
          new LevelOrderStringStore<TValue>.Handle(new LevelOrderStringStore<TValue>(tree, payloadStart, map)));

      return new PreorderTreenumerable<TValue, PreorderStringStore<TValue>.Handle>(
        new PreorderStringStore<TValue>.Handle(new PreorderStringStore<TValue>(tree, payloadStart, map)));
    }

    public static string Serialize(this IDepthFirstTreenumerable<string> treenumerable)
      => Serialize(treenumerable, node => node);

    // The bare (headerless) dft payload as a string: the terse grammar, load-bearing for tests
    // and fixtures. The enveloped forms live in TreeSerializer.Envelope.cs.
    public static string Serialize<TNode>(
      this IDepthFirstTreenumerable<TNode> treenumerable,
      Func<TNode, string> map)
    {
      var builder = new StringBuilder();

      using (var writer = new System.IO.StringWriter(builder))
        PreorderTextWriter.WritePayload(treenumerable, writer, map);

      return builder.ToString();
    }
  }
}
