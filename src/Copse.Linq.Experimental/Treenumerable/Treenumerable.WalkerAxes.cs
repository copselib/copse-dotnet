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

    public static IEnumerable<TNode> GetAncestors<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source,
      TNode node)
    {
      var parentResult = source.GetParent(node);

      while (parentResult.HasParent)
      {
        yield return parentResult.Parent;

        parentResult = source.GetParent(parentResult.Parent);
      }
    }

    public static IEnumerable<TNode> GetAncestorsAndSelf<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source,
      TNode node)
    {
      yield return node;

      foreach (var ancestor in source.GetAncestors(node))
        yield return ancestor;
    }

    public static TNode GetRoot<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source,
      TNode node)
    {
      var root = node;
      var parentResult = source.GetParent(node);

      while (parentResult.HasParent)
      {
        root = parentResult.Parent;

        parentResult = source.GetParent(root);
      }

      return root;
    }

    // The number of proper ancestors. O(depth) -- contrast a height, which is a subtree sweep.
    public static int GetDepth<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source,
      TNode node)
    {
      var depth = 0;
      var parentResult = source.GetParent(node);

      while (parentResult.HasParent)
      {
        depth++;

        parentResult = source.GetParent(parentResult.Parent);
      }

      return depth;
    }

    public static IEnumerable<NodeAndSiblingIndex<TNode>> GetChildren<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source,
      TNode node)
    {
      for (var childIndex = 0; ; childIndex++)
      {
        var childResult = source.GetChildAt(node, childIndex);

        if (!childResult.HasChild)
          yield break;

        yield return childResult.Child;
      }
    }

    public static IEnumerable<NodeAndSiblingIndex<TNode>> GetRootNodes<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source)
    {
      for (var rootIndex = 0; ; rootIndex++)
      {
        var rootResult = source.GetRootAt(rootIndex);

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
    public static int GetChildCount<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source,
      TNode node)
    {
      var childCount = 0;

      while (source.GetChildAt(node, childCount).HasChild)
        childCount++;

      return childCount;
    }

    // The swap-down verb of the walker/treenumerable pair (the AsEnumerable precedent): does
    // nothing, exists purely to steer the static type back to the streaming surface mid-chain.
    // The swap UP is MaterializeWalkable's probe ladder -- free where the capability survives,
    // a documented capture where it does not.
    public static ITreenumerable<TValue> AsTreenumerable<TValue, TNode>(
      this IWalkableTreenumerable<TValue, TNode> source)
      => source;
  }
}
