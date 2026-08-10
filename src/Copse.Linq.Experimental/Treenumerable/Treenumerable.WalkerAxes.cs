using System.Collections.Generic;

namespace Copse.Linq.Experimental
{
  public static partial class Treenumerable
  {
    // The sequence floor of the walker tower (docs/WALKER_DESIGN.md): axes yield lazy sequences of
    // HANDLES, so ordinary LINQ is the walker's operator algebra -- no operator algebra of its own.
    // Values resolve through GetValue. Names follow the 2016 ITreeWalker surface (GetAncestors,
    // GetChildren, ...), whose extensions these resurrect. Parked here so the axis spelling isn't
    // locked in by shipping; the region and walk floors are not scaffolded yet.

    public static IEnumerable<TNode> GetAncestors<TValue, TNode, TChildEnumerator>(
      this IWalkableTreenumerable<TValue, TNode, TChildEnumerator> source,
      TNode node)
      where TChildEnumerator : IChildEnumerator<TNode>
    {
      var parentResult = source.GetParent(node);

      while (parentResult.HasParent)
      {
        yield return parentResult.Parent;

        parentResult = source.GetParent(parentResult.Parent);
      }
    }

    public static IEnumerable<TNode> GetAncestorsAndSelf<TValue, TNode, TChildEnumerator>(
      this IWalkableTreenumerable<TValue, TNode, TChildEnumerator> source,
      TNode node)
      where TChildEnumerator : IChildEnumerator<TNode>
    {
      yield return node;

      foreach (var ancestor in source.GetAncestors(node))
        yield return ancestor;
    }

    public static TNode GetRoot<TValue, TNode, TChildEnumerator>(
      this IWalkableTreenumerable<TValue, TNode, TChildEnumerator> source,
      TNode node)
      where TChildEnumerator : IChildEnumerator<TNode>
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
    public static int GetDepth<TValue, TNode, TChildEnumerator>(
      this IWalkableTreenumerable<TValue, TNode, TChildEnumerator> source,
      TNode node)
      where TChildEnumerator : IChildEnumerator<TNode>
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

    public static IEnumerable<NodeAndSiblingIndex<TNode>> GetChildren<TValue, TNode, TChildEnumerator>(
      this IWalkableTreenumerable<TValue, TNode, TChildEnumerator> source,
      TNode node)
      where TChildEnumerator : IChildEnumerator<TNode>
    {
      var childEnumerator = source.GetChildEnumerator(node);

      try
      {
        var childResult = childEnumerator.MoveNext();

        while (childResult.HasChild)
        {
          yield return childResult.Child;

          childResult = childEnumerator.MoveNext();
        }
      }
      finally
      {
        childEnumerator.Dispose();
      }
    }

    public static IEnumerable<NodeAndSiblingIndex<TNode>> GetRootNodes<TValue, TNode, TChildEnumerator>(
      this IWalkableTreenumerable<TValue, TNode, TChildEnumerator> source)
      where TChildEnumerator : IChildEnumerator<TNode>
    {
      var rootEnumerator = source.GetRootEnumerator();

      try
      {
        var rootResult = rootEnumerator.MoveNext();

        while (rootResult.HasChild)
        {
          yield return rootResult.Child;

          rootResult = rootEnumerator.MoveNext();
        }
      }
      finally
      {
        rootEnumerator.Dispose();
      }
    }
  }
}
