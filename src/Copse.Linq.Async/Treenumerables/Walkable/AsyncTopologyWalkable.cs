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
  internal sealed class AsyncTopologyWalkable<TValue, THandle> : IAsyncWalkableTreenumerable<TValue, THandle>
  {
    public AsyncTopologyWalkable(IAsyncTreeTopology<TValue, THandle> topology)
    {
      _Topology = topology;
      _Walk = AsyncTree.FromTopology(topology);
    }

    private readonly IAsyncTreeTopology<TValue, THandle> _Topology;
    private readonly IAsyncTreenumerable<TValue> _Walk;

    public IAsyncTreenumerator<TValue> GetAsyncDepthFirstTreenumerator() => _Walk.GetAsyncDepthFirstTreenumerator();

    public IAsyncTreenumerator<TValue> GetAsyncBreadthFirstTreenumerator() => _Walk.GetAsyncBreadthFirstTreenumerator();

    public ValueTask<AsyncTreeWalker<TValue, THandle>> GetTreeWalkerAsync()
      => new ValueTask<AsyncTreeWalker<TValue, THandle>>(new AsyncTreeWalker<TValue, THandle>(_Topology));
  }
}
