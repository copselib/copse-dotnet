using Copse;
using System.Collections.Generic;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// The ROWID SCAN's bare half: every handle in the walkable, enumeration order
    /// DELIBERATELY UNSPECIFIED (a set, lazily yielded -- callers wanting an order commit one
    /// through the streaming surface). Handles are ENUMERATED, never computed from values --
    /// the direction that keeps equality out of every signature. The generic derivation below
    /// is an explicit-stack descent over the indexed child axis; providers with a dense handle
    /// space (the flat stores: 0..n-1) can serve this as a flat scan through a future
    /// capability probe, which would also spare the preorder walkable its adjacency-index
    /// build. Diverges on an infinite walkable, by the caller's choice -- the LINQ Count()
    /// divergence contract, same as every whole-structure consumer.
    /// </summary>
    public static IEnumerable<TNode> GetHandles<TValue, TNode>(this IWalkableTreenumerable<TValue, TNode> source)
    {
      var pendingFrames = new Stack<(TNode Handle, int ChildIndex)>();

      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = source.GetRootAt(rootIndex);

        if (!rootResult.HasChild)
          yield break;

        yield return rootResult.Child.Node;
        pendingFrames.Push((rootResult.Child.Node, 0));

        while (pendingFrames.Count > 0)
        {
          var frame = pendingFrames.Pop();
          var childResult = source.GetChildAt(frame.Handle, frame.ChildIndex);

          if (!childResult.HasChild)
            continue;

          pendingFrames.Push((frame.Handle, frame.ChildIndex + 1));

          yield return childResult.Child.Node;
          pendingFrames.Push((childResult.Child.Node, 0));
        }
      }
    }

    /// <summary>
    /// The rowid scan: every (handle, value) row of the labeling function, so predicates over
    /// values can pick out handles -- the bridge from value-space (what the consumer knows) to
    /// handle-space (what the walkable speaks). Same unspecified order and divergence contract
    /// as <see cref="GetHandles{TValue, TNode}"/>.
    /// </summary>
    public static IEnumerable<HandleAndValue<TNode, TValue>> GetHandlesWithValues<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source)
    {
      foreach (var handle in source.GetHandles())
        yield return new HandleAndValue<TNode, TValue>(handle, source.GetValue(handle));
    }
  }
}
