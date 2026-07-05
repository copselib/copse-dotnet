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
    // Mirror: reverse the order of every node's children (and the roots). The overload set
    // encodes the organizing principle (see TRAVERSAL_DIMENSION_SPLIT.md): mirroring is
    // WINDOWED under breadth-first arrival (reversing every sibling group reverses each level
    // end-to-end -- O(width)) and TOTAL under depth-first arrival (the mirror owes the
    // original LAST child right after the root), so:
    //
    //  - a breadth-first-capable source streams its mirror, and gets back only the
    //    breadth-first dimension (fullness of the SOURCE does not buy the mirror's depth-first
    //    dimension; only random access does);
    //  - a buffer -- the proof that the O(n) is already paid and seekable -- gets the full
    //    composite back (the overload below);
    //  - a depth-first-only source cannot call Invert at all: the only spelling is
    //    .Memoize().Invert(), putting the O(n) visibly in the caller's code. There is no
    //    spelling of silent buffering.
    public static IBreadthFirstTreenumerable<TNode> Invert<TNode>(this IBreadthFirstTreenumerable<TNode> source)
      => new LevelOrderStreamTreenumerable<TNode, InvertedLevelOrderStream<TNode>>(
        () => new InvertedLevelOrderStream<TNode>(source.GetBreadthFirstTreenumerator()));

    // The buffer overload: a capture in hand makes the mirror's depth-first dimension
    // affordable, so the mirror is a full citizen again. Built once, lazily (on first
    // acquisition), by walking the capture's depth-first replay into mirrored preorder arrays
    // -- one array build instead of the old materialize-then-copy, and the source itself is
    // never re-enumerated. (The zero-copy mirrored VIEW over the capture's own arrays is the
    // planned upgrade, arriving with the layout-typed buffer interfaces.)
    public static ITreenumerable<TNode> Invert<TNode>(this ITreenumerableBuffer<TNode> source)
    {
      var mirror = new Lazy<PreorderTreenumerable<TNode, PreorderArrayStore<TNode>>>(() => BuildMirror(source));

      return TreenumerableFactory.Create(
        () => mirror.Value.GetBreadthFirstTreenumerator(),
        () => mirror.Value.GetDepthFirstTreenumerator());
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
