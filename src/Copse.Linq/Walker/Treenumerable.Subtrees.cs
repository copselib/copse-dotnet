using Copse;
using Copse.Linq.Treenumerables;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// The tree of subtrees: every node relabeled with the subtree rooted at it, shape
    /// untouched. This is the comonad's <c>duplicate</c> in its COFREE presentation
    /// (docs/CATEGORY_THEORY_SURVEY.md §4) -- on <c>a(b,c)</c> the labels are
    /// <c>a(b,c)</c>, <c>b</c>, <c>c</c>: extract the root's label and the whole tree
    /// comes back. Each label is a severed, re-rooted VIEW sharing the source's handles --
    /// two fields, no copying, built lazily per <c>GetValue</c> pull -- so the tree of
    /// subtrees costs O(1) per label where reifying it would cost O(n) per node. Labels
    /// carry no identity (two pulls of the same handle yield distinct view objects);
    /// comparing them is meaningless, in the spirit of the no-node-equality pledge.
    ///
    /// <para>Severed means severed: a label answers <c>GetParent</c> at its root with
    /// "none" -- the subtree is the vantage's FUTURE only. Observations that need upward
    /// sight (depth, ancestor values, root-path folds) are <see cref="Extend"/>'s
    /// territory, whose observers receive the unsevered source; the Store-presentation
    /// duplicate (labels keeping upward sight) remains the Extend-derived
    /// <c>Extend((w, h) =&gt; h)</c> diagonal pending a carrier type. Laws pinned nodewise
    /// in <c>SubtreesLawTests</c>: the subtree at a root is that root's whole tree, every
    /// label's root value is the original value, and a subtree of a subtree is the deeper
    /// subtree (the counits and co-associativity, in the cofree clothing).</para>
    /// </summary>
    public static IWalkableTreenumerable<IWalkableTreenumerable<TValue, THandle>, THandle> Subtrees<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source)
      => source.Extend<TValue, THandle, IWalkableTreenumerable<TValue, THandle>>(
        (walkable, handle) => new SubtreeWalkable<TValue, THandle>(walkable, handle));
  }
}
