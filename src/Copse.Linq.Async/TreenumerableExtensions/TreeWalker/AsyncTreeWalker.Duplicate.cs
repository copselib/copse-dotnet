using Copse.Async;
using System.Threading.Tasks;

namespace Copse.Linq
{
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
