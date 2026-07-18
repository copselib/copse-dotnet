using Copse.Linq.Async.Stores;
using Copse.Async.Stores;
using Copse.Async;
using Copse.Async.Treenumerables;
using Copse.Async.Treenumerators;
using Copse.Core;
using Copse.Core.Async;
using Copse.Linq.Async.Treenumerables;
using Copse.Linq.Async.Treenumerators;
using Copse.Linq.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The survey-shaped downward pass: arrivals resolve top-down (roots receive
    /// <paramref name="seed"/>; every other node receives what its parent dispatched to it), and
    /// each internal node's <paramref name="survey"/> sees its arrival together with ALL of its
    /// children at once -- one write-handle per child, each of which must receive exactly one
    /// <see cref="DispatchTarget{TSource, TDispatch}.Dispatch"/> (a second throws immediately; a
    /// missed one throws when the survey returns). Sibling-complete visibility is the point: a
    /// fairness split cannot allocate its edges independently, and a setter-callback allocator
    /// plugs in verbatim -- <c>(child, amount) =&gt; child.Dispatch(amount)</c> IS its assignment
    /// callback. Leaves are not surveyed. Surveys run in depth-first preorder.
    ///
    /// <para>The result pairs every source value with what arrived at it
    /// (<see cref="DispatchNode{TSource, TDispatch}"/>) in the source tree's shape -- it
    /// DECORATES rather than replaces, so the flavors are compositions: project the pair away
    /// with Select for immutable values, or apply it with Do (then unwrap) for mutable ones.</para>
    ///
    /// <para>Returns an <see cref="IAsyncTreenumerableBuffer{TValue}"/> for LeaffixScan's reason,
    /// mirrored: the survey needs its FULL child list before the first child's value exists, and
    /// in a depth-first stream a parent's children are separated by entire sibling subtrees -- so
    /// the source is fully consumed before the first result visit can be published. Deferred:
    /// construction is pinned to the first treenumerator acquisition (Tree.Lazy), and the awaited
    /// build runs ONCE, on the first replay pull. The source is consumed depth-first only, so a
    /// streamed narrow source can dispatch.</para>
    /// </summary>
    public static IAsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>> RootfixDispatch<TSource, TDispatch>(
      this IAsyncDepthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<NodeContext<TSource>, TDispatch, IReadOnlyList<DispatchTarget<TSource, TDispatch>>> survey)
      => new AsyncTreenumerableBuffer<DispatchNode<TSource, TDispatch>>(
        AsyncTree.Lazy(() => PreorderDispatch(source, seed, survey)), BufferLayout.Preorder);

    // Preorder for BOTH dimensions, matching LeaffixScan's layout decision (see its note: the
    // breadth-first cross-decode tax over raw array stores is ~1.08x, not worth a transpose).
    private static IAsyncTreenumerable<DispatchNode<TSource, TDispatch>> PreorderDispatch<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<NodeContext<TSource>, TDispatch, IReadOnlyList<DispatchTarget<TSource, TDispatch>>> survey)
    {
      var dispatched = new AsyncLazyPreorderStore<DispatchNode<TSource, TDispatch>>(
        () => BuildRootfixDispatchAsync(source, seed, survey));

      return new AsyncPreorderTreenumerable<DispatchNode<TSource, TDispatch>, AsyncLazyPreorderStore<DispatchNode<TSource, TDispatch>>>(dispatched);
    }

    private static async ValueTask<AsyncPreorderArrayStore<DispatchNode<TSource, TDispatch>>> BuildRootfixDispatchAsync<TSource, TDispatch>(
      IAsyncDepthFirstTreenumerable<TSource> source,
      TDispatch seed,
      Action<NodeContext<TSource>, TDispatch, IReadOnlyList<DispatchTarget<TSource, TDispatch>>> survey)
    {
      // Pass 1: one forward DFS into flat pre-order arrays -- contexts plus subtree sizes (the
      // encoding LeaffixScan builds; a node's children sit at subtree-size hops after it).
      var contexts = new List<NodeContext<TSource>>();
      var subtreeSizes = new List<int>();
      var openNodeIndexes = new Stack<int>(); // open ancestors of the current node

      void Close()
      {
        var openIndex = openNodeIndexes.Pop();

        subtreeSizes[openIndex] = contexts.Count - openIndex;
      }

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          // Returning to this depth (or shallower) means every deeper open node is complete.
          while (openNodeIndexes.Count > treenumerator.Position.Depth)
            Close();

          openNodeIndexes.Push(contexts.Count);
          contexts.Add(treenumerator.ToNodeContext());
          subtreeSizes.Add(0);
        }
      }

      while (openNodeIndexes.Count > 0)
        Close();

      // Pass 2: top-down over the flat encoding. Preorder puts every parent before its children,
      // so each node's arrival is resolved before its own survey runs; roots (index 0 and every
      // whole-subtree hop after a root) are seeded first.
      var nodeCount = contexts.Count;
      var arrivals = new TDispatch[nodeCount];
      var results = new DispatchNode<TSource, TDispatch>[nodeCount];

      for (var rootIndex = 0; rootIndex < nodeCount; rootIndex += subtreeSizes[rootIndex])
        arrivals[rootIndex] = seed;

      for (var nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
      {
        results[nodeIndex] = new DispatchNode<TSource, TDispatch>(contexts[nodeIndex].Node, arrivals[nodeIndex]);

        if (subtreeSizes[nodeIndex] == 1)
          continue;

        var targets = new List<DispatchTarget<TSource, TDispatch>>();
        var subtreeEnd = nodeIndex + subtreeSizes[nodeIndex];

        for (var childIndex = nodeIndex + 1; childIndex < subtreeEnd; childIndex += subtreeSizes[childIndex])
          targets.Add(new DispatchTarget<TSource, TDispatch>(childIndex, contexts[childIndex]));

        survey(contexts[nodeIndex], arrivals[nodeIndex], targets);

        foreach (var target in targets)
          arrivals[target.Index] = target.GetDispatchedOrThrow();
      }

      return new AsyncPreorderArrayStore<DispatchNode<TSource, TDispatch>>(results, subtreeSizes.ToArray());
    }
  }
}
