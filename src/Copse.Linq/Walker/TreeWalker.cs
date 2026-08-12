using Copse;
using Copse.Linq.Treenumerables;
using System;

namespace Copse.Linq
{
  /// <summary>
  /// The focused pair, reified: a walkable plus a VALID focus -- the carrier of the
  /// full-context (Store) comonad, the type whose instances are what
  /// docs/CATEGORY_THEORY_SURVEY.md §4 calls "the whole tree, seen from here." Two words of
  /// data, by value, nothing owned: many walkers share one terrain, and stepping never
  /// mutates -- every move returns a NEW walker (the comonad is pure; a stance is a value,
  /// not a machine).
  ///
  /// <para>THE INVARIANT: a walker is always focused on an actual node. "Not yet
  /// positioned" is traversal-protocol state (<c>NodePosition.ForestRoot</c>, the
  /// treenumerator's before-first convention) and deliberately has no walker spelling --
  /// extract must always have a value to return, so the unfocused state is not a member of
  /// the carrier. Every creation path (the <c>WalkerAt</c>/<c>GetRootWalker</c> doors, the
  /// step results, <c>Duplicate</c>'s labels) supplies a real handle. The CLR manufactures
  /// <c>default(TreeWalker&lt;,&gt;)</c> anyway; per the <see cref="ChildResult{TNode}"/>
  /// convention, that value is invalid and must not be used.</para>
  ///
  /// <para>The comonad, member by member: <see cref="Value"/> is extract;
  /// <see cref="Extend{TResult}"/> is co-bind (its observer takes a CURSOR -- the co-Kleisli
  /// arrows are just <c>Func&lt;TreeWalker, TResult&gt;</c>); and <see cref="Duplicate"/> is
  /// <c>Extend(walker =&gt; walker)</c> -- extend of the identity, the textbook definition,
  /// one line. The vantage is bidirectional (this is the Store presentation, not the severed
  /// cofree one <c>Subtrees()</c> ships): <see cref="MoveToParent"/> is legal because the
  /// focus keeps its ancestors.</para>
  /// </summary>
  public readonly struct TreeWalker<TValue, THandle>
  {
    internal TreeWalker(IWalkableTreenumerable<TValue, THandle> walkable, THandle focus)
    {
      _Walkable = walkable;
      Focus = focus;
    }

    private readonly IWalkableTreenumerable<TValue, THandle> _Walkable;

    /// <summary>The handle this walker stands at. Always an actual node -- see the invariant.</summary>
    public readonly THandle Focus;

    /// <summary>Extract: the value at the focus. Always valid -- a walker cannot be unfocused.</summary>
    public TValue Value => _Walkable.GetValue(Focus);

    /// <summary>Single upward step. The STEP can fail (a root has no parent); the stance
    /// cannot -- so the result is a by-value maybe, never an unfocused walker.</summary>
    public TreeWalkerResult<TValue, THandle> MoveToParent()
    {
      var parentResult = _Walkable.GetParent(Focus);

      return parentResult.HasParent
        ? new TreeWalkerResult<TValue, THandle>(new TreeWalker<TValue, THandle>(_Walkable, parentResult.Parent))
        : default;
    }

    /// <summary>Single downward step to the child at <paramref name="childIndex"/> in sibling
    /// order, or an empty result past the last child.</summary>
    public TreeWalkerResult<TValue, THandle> MoveToChild(int childIndex)
    {
      var childResult = _Walkable.GetChildAt(Focus, childIndex);

      return childResult.HasChild
        ? new TreeWalkerResult<TValue, THandle>(new TreeWalker<TValue, THandle>(_Walkable, childResult.Child.Node))
        : default;
    }

    /// <summary>Co-bind: relabel the whole terrain by an observation of every focus, and keep
    /// standing where you are. The observer receives a walker, so it can extract, step, and
    /// extend -- anything a vantage affords.</summary>
    public TreeWalker<TResult, THandle> Extend<TResult>(Func<TreeWalker<TValue, THandle>, TResult> observer)
    {
      var walkable = _Walkable;

      return new TreeWalker<TResult, THandle>(
        walkable.Extend<TValue, THandle, TResult>(
          (source, handle) => observer(new TreeWalker<TValue, THandle>(source, handle))),
        Focus);
    }

    /// <summary>Duplicate: the tree of walkers, still standing at this focus -- extend of the
    /// identity, which is the definition. <c>walker.Duplicate().Value</c> is the walker
    /// itself: the counit, readable in the types.</summary>
    public TreeWalker<TreeWalker<TValue, THandle>, THandle> Duplicate() => Extend(walker => walker);

    /// <summary>The reverse door: the treenumerable this stance denotes -- the subtree rooted
    /// at the focus, as a severed re-rooted view sharing the source's handles. Identical to
    /// the label <see cref="Treenumerable.Subtrees{TValue, THandle}"/> stamps at this focus
    /// (pinned). The round trip tree → root walker → <c>Subtree()</c> recovers the tree; the
    /// other round trip, walker → <c>Subtree()</c> → root walker, lands at the same focus but
    /// FORGETS the upward context (severance is the cofree forgetting -- deliberate, and the
    /// reason the two round trips are not symmetric).</summary>
    public IWalkableTreenumerable<TValue, THandle> Subtree()
      => new SubtreeWalkable<TValue, THandle>(_Walkable, Focus);
  }
}
