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

    public static IEnumerable<THandle> GetAncestors<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var step = source.GetTreeWalkerAt(handle).MoveToParent();

      while (step.HasWalker)
      {
        yield return step.Walker.Focus;

        step = step.Walker.MoveToParent();
      }
    }

    public static IEnumerable<THandle> GetAncestorsAndSelf<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      yield return handle;

      foreach (var ancestor in source.GetAncestors(handle))
        yield return ancestor;
    }

    public static THandle GetRoot<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var walker = source.GetTreeWalkerAt(handle);
      var step = walker.MoveToParent();

      while (step.HasWalker)
      {
        walker = step.Walker;

        step = walker.MoveToParent();
      }

      return walker.Focus;
    }

    // The number of proper ancestors. O(depth) -- contrast a height, which is a subtree sweep.
    public static int GetDepth<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var depth = 0;
      var step = source.GetTreeWalkerAt(handle).MoveToParent();

      while (step.HasWalker)
      {
        depth++;

        step = step.Walker.MoveToParent();
      }

      return depth;
    }

    public static IEnumerable<NodeAndSiblingIndex<THandle>> GetChildren<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var walker = source.GetTreeWalkerAt(handle);

      for (var childIndex = 0; ; childIndex++)
      {
        var step = walker.MoveToChild(childIndex);

        if (!step.HasWalker)
          yield break;

        yield return new NodeAndSiblingIndex<THandle>(step.Walker.Focus, childIndex);
      }
    }

    public static IEnumerable<NodeAndSiblingIndex<THandle>> GetRootNodes<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source)
    {
      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = source.TryGetTreeWalkerAtRootIndex(rootIndex);

        if (!rootResult.HasWalker)
          yield break;

        yield return new NodeAndSiblingIndex<THandle>(rootResult.Walker.Focus, rootIndex);
      }
    }

    // The derived count -- deliberately NOT on the contract: the step is finite work per call
    // whatever the fan-out, but a count diverges on a generator-backed provider with an
    // unbounded child group. This walks the child axis to the first miss -- the LINQ Count()
    // contract, divergent on infinite sequences by the caller's choice. Finite providers offer
    // cheap counts as members of their concrete types.
    public static int GetChildCount<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var walker = source.GetTreeWalkerAt(handle);
      var childCount = 0;

      while (walker.MoveToChild(childCount).HasWalker)
        childCount++;

      return childCount;
    }

    // The swap-down verb of the walker/treenumerable pair (the AsEnumerable precedent): does
    // nothing, exists purely to steer the static type back to the streaming surface mid-chain.
    // The swap UP is Materialize -- the capture's door binds the index, free where the
    // capability survives, a documented capture where it does not.
    public static ITreenumerable<TValue> AsTreenumerable<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source)
      => source;
  }
}
