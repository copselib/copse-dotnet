using Copse;
using Copse.Core;

namespace Copse.Linq.Treenumerables
{
  // The intersection made concrete: a store-backed walkable presented with the buffer marker.
  // A wrapper for the same reason TreenumerableBuffer is one -- the walkable citizens live in
  // Copse and cannot implement this Copse.Linq interface directly. Pure delegation; the layout
  // is declared by whoever built the capture. Ordinal handles (int) always: everything that
  // arrives through the escalation is flat-store-backed.
  internal sealed class WalkableTreenumerableBuffer<TValue> : IWalkableTreenumerableBuffer<TValue, int>
  {
    public WalkableTreenumerableBuffer(IWalkableTreenumerable<TValue, int> walkable, BufferLayout nativeLayout)
    {
      _Walkable = walkable;
      NativeLayout = nativeLayout;
    }

    private readonly IWalkableTreenumerable<TValue, int> _Walkable;

    public BufferLayout? NativeLayout { get; }

    public ITreenumerator<TValue> GetDepthFirstTreenumerator() => _Walkable.GetDepthFirstTreenumerator();

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator() => _Walkable.GetBreadthFirstTreenumerator();

    public TValue GetValue(int node) => _Walkable.GetValue(node);

    public ParentResult<int> GetParent(int node) => _Walkable.GetParent(node);

    public ChildResult<int> GetChildAt(int node, int childIndex) => _Walkable.GetChildAt(node, childIndex);

    public ChildResult<int> GetRootAt(int rootIndex) => _Walkable.GetRootAt(rootIndex);
  }
}
