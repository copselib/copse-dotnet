using Copse.Core;
using Copse.Treenumerables;
using System;
using System.Collections.Generic;
using System.Text;

namespace Copse.SimpleSerializer
{
  public static partial class TreeSerializer
  {
    public static ITreenumerable<string> Deserialize(string tree)
      => Deserialize(tree, value => value);

    public static ITreenumerable<TValue> Deserialize<TValue>(
      string tree,
      Func<string, TValue> map)
    {
      // Adapt the string map to a span map: each value is materialized once (chars.ToString()).
      SpanMap<TValue> spanMap = chars => map(chars.ToString());
      return Parse(tree, spanMap);
    }

    // Span overload: the map receives each value as a slice of the source text (no intermediate
    // string), so deserializing into non-string values (e.g. int.Parse(chars)) allocates no value
    // strings at all.
    public static ITreenumerable<TValue> Deserialize<TValue>(
      string tree,
      SpanMap<TValue> map)
      => Parse(tree, map);

    // Single pass over the (pre-order) text, building the flat PreorderTree arrays directly -- no
    // StringBuilder, no token stream, no intermediate SimpleNode tree. A value followed by '(' is a
    // parent (its subtree size is backfilled at its matching ')'); a value followed by ',', ')' or
    // end-of-string is a leaf (size 1). Subtrees are contiguous, so a parent's size is simply
    // (node count - its index) at the moment it closes.
    private static PreorderTree<TValue> Parse<TValue>(string tree, SpanMap<TValue> map)
    {
      var values = new List<TValue>();
      var subtreeSizes = new List<int>();
      var open = new Stack<int>();   // indices of parents whose ')' hasn't been seen yet
      var valueStart = -1;           // start of the pending value run; -1 = none

      void Commit(int end, bool asParent)
      {
        if (valueStart < 0)
          return;
        var index = values.Count;
        values.Add(map(tree.AsSpan(valueStart, end - valueStart)));
        subtreeSizes.Add(asParent ? 0 : 1);   // a parent's size is backfilled when it closes
        valueStart = -1;
        if (asParent)
          open.Push(index);
      }

      for (int i = 0; i < tree.Length; i++)
      {
        switch (tree[i])
        {
          case '(':
            Commit(i, asParent: true);
            break;

          case ')':
            Commit(i, asParent: false);
            var closed = open.Pop();
            subtreeSizes[closed] = values.Count - closed;
            break;

          case ',':
            Commit(i, asParent: false);
            break;

          default:
            if (valueStart < 0)
              valueStart = i;
            break;
        }
      }

      Commit(tree.Length, asParent: false); // trailing top-level value, if any

      return new PreorderTree<TValue>(values.ToArray(), subtreeSizes.ToArray());
    }

    public static string Serialize(this IDepthFirstTreenumerable<string> treenumerable)
      => Serialize(treenumerable, node => node);

    public static string Serialize<TNode>(
      this IDepthFirstTreenumerable<TNode> treenumerable,
      Func<TNode, string> map)
    {
      var builder = new StringBuilder();

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
              builder.Append('(');
            }
            else if (depth < previousDepth)
            {
              for (int i = 0; i < previousDepth - depth; i++)
                builder.Append(')');

              builder.Append(',');
            }
            else
            {
              builder.Append(',');
            }
          }

          builder.Append(map(treenumerator.Node));

          previousDepth = treenumerator.Position.Depth;
        }

        while (previousDepth-- > 0)
          builder.Append(')');

        return builder.ToString();
      }
    }
  }
}
