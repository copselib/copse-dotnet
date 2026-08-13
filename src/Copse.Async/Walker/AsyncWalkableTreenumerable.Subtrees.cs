using System.Threading.Tasks;

namespace Copse.Async
{
  public static partial class AsyncWalkableTreenumerable
  {
    /// <summary>
    /// The tree of subtrees: every node relabeled with the subtree rooted at it, shape
    /// untouched. This is the comonad's <c>duplicate</c> in its COFREE presentation
    /// (docs/CATEGORY_THEORY_SURVEY.md §4) -- on <c>a(b,c)</c> the labels are <c>a(b,c)</c>,
    /// <c>b</c>, <c>c</c>: extract the root's label and the whole tree comes back. Each label
    /// is a severed, re-rooted VIEW sharing the source's handles -- two fields, no copying,
    /// built lazily per pull -- so the tree of subtrees costs O(1) per label where reifying
    /// it would cost O(n) per node. Labels carry no identity (two pulls of the same handle
    /// yield distinct view objects); comparing them is meaningless, in the spirit of the
    /// no-node-equality pledge.
    ///
    /// <para>Severed means severed: a label answers its root's parent probe with "none" --
    /// the subtree is the vantage's FUTURE only. Observations that need upward sight (depth,
    /// ancestor values, root-path folds) are <c>Extend</c>'s territory, whose observers
    /// receive the unsevered source. Laws pinned nodewise in the Subtrees law suites: the
    /// subtree at a root is that root's whole tree, every label's root value is the original
    /// value, and a subtree of a subtree is the deeper subtree (the counits and
    /// co-associativity, in the cofree clothing).</para>
    /// </summary>
    public static IAsyncWalkableTreenumerable<IAsyncWalkableTreenumerable<TValue, THandle>, THandle> Subtrees<TValue, THandle>(
      this IAsyncWalkableTreenumerable<TValue, THandle> source)
      => source.Extend<TValue, THandle, IAsyncWalkableTreenumerable<TValue, THandle>>(
        (walkable, handle) => new ValueTask<IAsyncWalkableTreenumerable<TValue, THandle>>(
          new AsyncSubtreeWalkable<TValue, THandle>(walkable, handle)));
  }
}
