using Copse.Core;
using Copse.Linq.Treenumerators;
using Copse.Linq.Treenumerables;
using Copse.Treenumerables;
using System;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    // Mirror: reverse the order of every node's children (and the roots). Two regimes, by what
    // the source can afford (see TRAVERSAL_DIMENSION_SPLIT.md):
    //
    //  - A breadth-first-ONLY source (a level-order stream) streams its mirror in O(width):
    //    reversing every sibling group reverses each level end-to-end, so no capture is needed
    //    and the result stays a narrow breadth-first treenumerable.
    //
    //  - Anything else materializes. The mirror's depth-first dimension is TOTAL (it owes the
    //    original's LAST child right after the root), so a full mirror needs the whole tree
    //    captured. Rather than FORCE the caller to spell .Memoize()/.Materialize() first (the old
    //    "disclose the O(n) on the input" rule, which left a depth-first-only source unable to
    //    Invert at all), Invert captures internally and discloses the O(n) on the OUTPUT: it
    //    returns an ITreenumerableBuffer -- a completed, owned capture. A buffer in hand skips to
    //    the mirror build; a plain ITreenumerable (or a depth-first-only stream) is Materialized
    //    first. (Overload resolution: a full source is equally breadth- and depth-first, so the
    //    ITreenumerable overload disambiguates it to here; only a NARROW source reaches the
    //    single-dimension overloads.)
    //
    // The mirror build still copies into fresh preorder arrays; the zero-copy mirrored VIEW over
    // the capture's own arrays is the planned upgrade, arriving with the layout-typed buffer
    // interfaces.
    public static IBreadthFirstTreenumerable<TNode> Invert<TNode>(this IBreadthFirstTreenumerable<TNode> source)
      => new LevelOrderStreamTreenumerable<TNode, InvertedLevelOrderStream<TNode>>(
        () => new InvertedLevelOrderStream<TNode>(source.GetBreadthFirstTreenumerator()));

    // The depth-first-only mirror cannot stream (the mirror owes the original's LAST child right
    // after the root), so it captures: Materialize, then invert the capture. The O(n) is
    // disclosed by the ITreenumerableBuffer return, not by a forced .Memoize() at the call site.
    public static ITreenumerableBuffer<TNode> Invert<TNode>(this IDepthFirstTreenumerable<TNode> source)
      => source.Materialize().Invert();

    // The full-source convenience overload (also the disambiguator for a source that is both
    // breadth- and depth-first): capture first, then invert the capture.
    public static ITreenumerableBuffer<TNode> Invert<TNode>(this ITreenumerable<TNode> source)
      => source.Materialize().Invert();

    // The buffer overload: a capture in hand makes the mirror's depth-first dimension affordable,
    // so the mirror is a full citizen -- returned as a completed ITreenumerableBuffer (the mirror
    // owns fresh arrays; there is no live feed, so the non-disposable base). Built once, lazily
    // on first acquisition, by walking the capture's depth-first replay into mirrored preorder
    // arrays; the source itself is never re-enumerated.
    public static ITreenumerableBuffer<TNode> Invert<TNode>(this ITreenumerableBuffer<TNode> source)
    {
      var mirror = new Lazy<PreorderTreenumerable<TNode, PreorderArrayStore<TNode>>>(() => BuildMirror(source));

      return new CompletedTreenumerableBuffer<TNode>(
        TreenumerableFactory.Create(
          () => mirror.Value.GetBreadthFirstTreenumerator(),
          () => mirror.Value.GetDepthFirstTreenumerator()));
    }

    private static PreorderTreenumerable<TNode, PreorderArrayStore<TNode>> BuildMirror<TNode>(ITreenumerableBuffer<TNode> source)
    {
      // 1. Capture flat preorder arrays (value + subtree size per node) from the buffer's
      //    depth-first replay.
      var values = new List<TNode>();
      var subtreeSizes = new List<int>();
      var open = new Stack<int>();

      using (var treenumerator = source.GetDepthFirstTreenumerator())
      {
        while (treenumerator.MoveNext(NodeTraversalStrategies.TraverseAll))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          while (open.Count > treenumerator.Position.Depth)
          {
            var closed = open.Pop();
            subtreeSizes[closed] = values.Count - closed;
          }

          open.Push(values.Count);
          values.Add(treenumerator.Node);
          subtreeSizes.Add(0);
        }
      }

      while (open.Count > 0)
      {
        var closed = open.Pop();
        subtreeSizes[closed] = values.Count - closed;
      }

      // 2. Emit the mirror. Pushing roots/children in forward order makes them pop in reverse,
      //    which is exactly the mirror's preorder. Each subtree keeps its size; only ordering
      //    changes.
      var count = values.Count;
      var mirroredValues = new TNode[count];
      var mirroredSubtreeSizes = new int[count];
      var stack = new Stack<int>();

      for (var root = 0; root < count; root += subtreeSizes[root])
        stack.Push(root);

      var output = 0;
      while (stack.Count > 0)
      {
        var index = stack.Pop();
        mirroredValues[output] = values[index];
        mirroredSubtreeSizes[output] = subtreeSizes[index];
        output++;

        var end = index + subtreeSizes[index];
        for (var child = index + 1; child < end; child += subtreeSizes[child])
          stack.Push(child);
      }

      return new PreorderTreenumerable<TNode, PreorderArrayStore<TNode>>(
        new PreorderArrayStore<TNode>(mirroredValues, mirroredSubtreeSizes));
    }
  }
}
