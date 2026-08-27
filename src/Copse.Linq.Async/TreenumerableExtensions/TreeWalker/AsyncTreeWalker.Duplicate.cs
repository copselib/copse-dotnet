using Copse.Core;
using Copse;
using System.Threading.Tasks;

namespace Copse.Linq
{
  /// <summary>The operator algebra over tree walkers -- Extend, Duplicate, Subtree, and the
  /// doors -- as extension methods.</summary>
  public static partial class AsyncTreeWalker
  {
    /// <summary>Duplicate: the tree of walkers, still standing at this focus -- extend of
    /// the identity, which is the definition. Duplicating and extracting recovers the
    /// walker: the counit, readable in the types.</summary>
    public static AsyncTreeWalker<AsyncTreeWalker<TNode, THandle>, THandle> Duplicate<TNode, THandle>(
      this AsyncTreeWalker<TNode, THandle> walker)
      => walker.Extend(focus => new ValueTask<AsyncTreeWalker<TNode, THandle>>(focus));
  }
}
