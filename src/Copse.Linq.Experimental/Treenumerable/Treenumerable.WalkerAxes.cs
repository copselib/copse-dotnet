using Copse.Core;
using System.Collections.Generic;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    // The sequence floor of the walker tower (docs/WALKER_DESIGN.md): axes yield lazy sequences of
    // HANDLES, so ordinary LINQ is the walker's operator algebra -- no operator algebra of its own.
    // Values resolve through GetValue. Names follow the 2016 ITreeWalker surface (GetAncestors,
    // GetChildren, ...), whose extensions these resurrect, over the indexed child contract (the
    // VisualTreeHelper shape -- no enumerator objects anywhere below this floor; the IEnumerable
    // allocation here is the LINQ boundary's, where it was always going to be paid). Parked here
    // so the axis spelling isn't locked in by shipping; the region and walk floors are not
    // scaffolded yet.

    public static IEnumerable<THandle> GetAncestors<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var parentResult = source.TryGetParent(handle);

      while (parentResult.HasParent)
      {
        yield return parentResult.Parent;

        parentResult = source.TryGetParent(parentResult.Parent);
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
      var root = handle;
      var parentResult = source.TryGetParent(handle);

      while (parentResult.HasParent)
      {
        root = parentResult.Parent;

        parentResult = source.TryGetParent(root);
      }

      return root;
    }

    // The number of proper ancestors. O(depth) -- contrast a height, which is a subtree sweep.
    public static int GetDepth<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var depth = 0;
      var parentResult = source.TryGetParent(handle);

      while (parentResult.HasParent)
      {
        depth++;

        parentResult = source.TryGetParent(parentResult.Parent);
      }

      return depth;
    }

    public static IEnumerable<NodeAndSiblingIndex<THandle>> GetChildren<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      for (var childIndex = 0; ; childIndex++)
      {
        var childResult = source.TryGetChildAt(handle, childIndex);

        if (!childResult.HasChild)
          yield break;

        yield return childResult.Child;
      }
    }

    public static IEnumerable<NodeAndSiblingIndex<THandle>> GetRootNodes<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source)
    {
      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = source.TryGetRootAt(rootIndex);

        if (!rootResult.HasChild)
          yield break;

        yield return rootResult.Child;
      }
    }

    // The derived count -- deliberately NOT on the contract: the probe is finite work per call
    // whatever the fan-out, but a count diverges on a generator-backed provider with an
    // unbounded child group. This walks the probe to the first miss -- the LINQ Count()
    // contract, divergent on infinite sequences by the caller's choice. Finite providers offer
    // cheap counts as members of their concrete types.
    public static int GetChildCount<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
    {
      var childCount = 0;

      while (source.TryGetChildAt(handle, childCount).HasChild)
        childCount++;

      return childCount;
    }

    // The swap-down verb of the walker/treenumerable pair (the AsEnumerable precedent): does
    // nothing, exists purely to steer the static type back to the streaming surface mid-chain.
    // The swap UP is Materialize's probe ladder (the walker escalation collapsed into it) -- free where the capability survives,
    // a documented capture where it does not.
    public static ITreenumerable<TValue> AsTreenumerable<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source)
      => source;
  }
}
