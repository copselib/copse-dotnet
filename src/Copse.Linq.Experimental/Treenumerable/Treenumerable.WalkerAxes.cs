using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    // The sequence floor of the walker tower (design-docs/WALKER_DESIGN.md): axes yield lazy sequences of
    // HANDLES, so ordinary LINQ is the walker's operator algebra -- no operator algebra of its own.
    // Values resolve through the walker. Names follow the 2016 ITreeWalker surface (GetAncestors,
    // GetChildren, ...), whose extensions these resurrect, spelled over the door and the walker's
    // steps (the only public navigation surface post-cut -- the adjacency probes are provider SPI).
    // Parked here so the axis spelling isn't locked in by shipping; the region and walk floors are
    // not scaffolded yet.

    public static IEnumerable<THandle> GetAncestors<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
    {
      var stance = source.GetTreeWalkerAt(handle);

      // The handle axes exclude the unfocused stance: it has no handle to yield (climbs top out
      // there; the axis stops one step earlier).
      while (stance.MoveToParent().TryGetValue(out stance) && stance.HasFocus)
        yield return stance.Focus;
    }

    public static IEnumerable<THandle> GetAncestorsAndSelf<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
    {
      yield return handle;

      foreach (var ancestor in source.GetAncestors(handle))
        yield return ancestor;
    }

    public static THandle GetRoot<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
    {
      var walker = source.GetTreeWalkerAt(handle);

      while (walker.MoveToParent().TryGetValue(out var parent) && parent.HasFocus)
        walker = parent;

      return walker.Focus;
    }

    // The number of proper ancestors. O(depth) -- contrast a height, which is a subtree sweep.
    public static int GetDepth<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
    {
      var depth = 0;
      var stance = source.GetTreeWalkerAt(handle);

      while (stance.MoveToParent().TryGetValue(out stance) && stance.HasFocus)
        depth++;

      return depth;
    }

    public static IEnumerable<HandleAndSiblingIndex<THandle>> GetChildren<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
    {
      var walker = source.GetTreeWalkerAt(handle);

      for (var childIndex = 0; walker.MoveToChild(childIndex).TryGetValue(out var child); childIndex++)
        yield return new HandleAndSiblingIndex<THandle>(child.Focus, childIndex);
    }

    public static IEnumerable<HandleAndSiblingIndex<THandle>> GetRootNodes<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source)
    {
      for (var rootIndex = 0; source.TryGetTreeWalkerAtRootIndex(rootIndex).TryGetValue(out var root); rootIndex++)
        yield return new HandleAndSiblingIndex<THandle>(root.Focus, rootIndex);
    }

    // The derived count -- deliberately NOT on the contract: the step is finite work per call
    // whatever the fan-out, but a count diverges on a generator-backed provider with an
    // unbounded child group. This walks the child axis to the first miss -- the LINQ Count()
    // contract, divergent on infinite sequences by the caller's choice. Finite providers offer
    // cheap counts as members of their concrete types.
    public static int GetChildCount<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source,
      THandle handle)
    {
      var walker = source.GetTreeWalkerAt(handle);
      var childCount = 0;

      while (walker.MoveToChild(childCount).HasValue)
        childCount++;

      return childCount;
    }

    // The swap-down verb of the walker/treenumerable pair (the AsEnumerable precedent): does
    // nothing, exists purely to steer the static type back to the streaming surface mid-chain.
    // The swap UP is Materialize -- the capture's door binds the index, free where the
    // capability survives, a documented capture where it does not.
    public static ITreenumerable<TNode> AsTreenumerable<TNode, THandle>(
      this IWalkableTreenumerable<TNode, THandle> source)
      => source;
  }
}
