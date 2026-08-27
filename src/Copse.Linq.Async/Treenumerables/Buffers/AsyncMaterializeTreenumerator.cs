using Copse.Core;
using System.Threading.Tasks;

namespace Copse.Linq.Treenumerables
{
  // The settle seam of AsyncMaterializeTreenumerable: a replay treenumerator acquired before the
  // buffer settled. Acquisition already did its organic work (acquiring the inner memo replay is
  // the pin, when nothing else pinned first); the first pull runs the owner's settle -- bulk
  // completion, and the layout-guarantee transpose if the shared memo's history won the pin --
  // then delegates every call to the inner replay. In the transpose case the still-unstarted
  // inner is swapped for a replay over the transposed capture, so the first pull is also the
  // last moment the swap is legal. All property reads delegate: an unstarted inner already holds
  // the pre-enumeration convention (NodePosition.ForestRoot, VisitCount 0, SchedulingNode).
  internal sealed class AsyncMaterializeTreenumerator<TNode> : IAsyncTreenumerator<TNode>
  {
    public AsyncMaterializeTreenumerator(
      AsyncMaterializeTreenumerable<TNode> owner,
      TreeTraversalStrategy dimension,
      IAsyncTreenumerator<TNode> inner)
    {
      _Owner = owner;
      _Dimension = dimension;
      _Inner = inner;
    }

    private readonly AsyncMaterializeTreenumerable<TNode> _Owner;
    private readonly TreeTraversalStrategy _Dimension;
    private IAsyncTreenumerator<TNode> _Inner;
    private bool _SettledOnFirstPull;

    public TNode Node => _Inner.Node;
    public int VisitCount => _Inner.VisitCount;
    public TreenumeratorMode Mode => _Inner.Mode;
    public NodePosition Position => _Inner.Position;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (!_SettledOnFirstPull)
      {
        _SettledOnFirstPull = true;

        var settled = await _Owner.SettleAsync().ConfigureAwait(false);

        if (!ReferenceEquals(settled, _Owner.Memo))
        {
          await _Inner.DisposeAsync().ConfigureAwait(false);

          _Inner = _Dimension == TreeTraversalStrategy.DepthFirst
            ? settled.GetAsyncDepthFirstTreenumerator()
            : settled.GetAsyncBreadthFirstTreenumerator();
        }
      }

      return await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
