using Copse.Async;
using Copse.Core.Async;
using System.Threading.Tasks;

namespace Copse.Linq.Async.Treenumerables
{
  // The identity view: a topology worn as a walkable, nothing rewritten. This is what the
  // unfocused stance's Subtree() denotes -- the source forest itself (there is there is nothing above it
  // to sever, and it contributes no row of its own: it has no value, and the
  // treenumerable has no spelling for a valueless node). Streams via the Walk adapter over
  // the same topology the door binds, so the walkable and its walkers agree on every answer.
  internal sealed class AsyncTopologyWalkable<TNode, THandle> : IAsyncWalkableTreenumerable<TNode, THandle>
  {
    public AsyncTopologyWalkable(IAsyncTreeTopology<TNode, THandle> topology)
    {
      _Topology = topology;
      _Walk = AsyncTree.FromTopology(topology);
    }

    private readonly IAsyncTreeTopology<TNode, THandle> _Topology;
    private readonly IAsyncTreenumerable<TNode> _Walk;

    public IAsyncTreenumerator<TNode> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TNode> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<AsyncTreeWalker<TNode, THandle>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TNode, THandle>>(new AsyncTreeWalker<TNode, THandle>(_Topology));
  }
}
