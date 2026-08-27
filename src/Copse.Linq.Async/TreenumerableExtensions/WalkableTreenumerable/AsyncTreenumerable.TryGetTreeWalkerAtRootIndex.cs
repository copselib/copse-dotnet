using Copse.Core;
using Copse;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>
    /// The root door: a walker standing at root <paramref name="rootIndex"/>, or an empty
    /// result past the last root. TryGet by the Try law: the miss is expected and TYPED (a
    /// forest may have fewer roots, or none) -- the result struct is the async spelling of
    /// the try-pattern (<c>out</c> cannot cross an <c>await</c>). The name spells RootIndex
    /// because the int here is a root ordinal, not a handle -- the door that takes a handle
    /// is <see cref="GetTreeWalkerAtAsync{TNode, THandle}"/>, and when <c>THandle</c> is
    /// <c>int</c> only the names keep the two questions apart. Sugar over the door: the
    /// roots are the unfocused stance's child group, so this is one knock and one downward step,
    /// and the answer is the step family's own result shape.
    /// </summary>
    public static async ValueTask<AsyncTreeWalkerResult<TNode, THandle>> TryGetTreeWalkerAtRootIndexAsync<TNode, THandle>(
      this IAsyncWalkableTreenumerable<TNode, THandle> source,
      int rootIndex = 0)
      => await (await source.GetTreeWalkerAsync().ConfigureAwait(false)).MoveToChildAsync(rootIndex).ConfigureAwait(false);
  }
}
