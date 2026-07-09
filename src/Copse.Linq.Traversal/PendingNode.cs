using Copse;

namespace Copse.Linq
{
  // A node awaiting its subtree to close during a leaffix pass: its slot index in the flat
  // accumulation buffer plus the context handed to the leaf-selector/accumulator. Shared by the
  // sync LeaffixScan/LeaffixAggregate and their async analogs.
  internal readonly struct PendingNode<TSource>
  {
    public PendingNode(int index, NodeContext<TSource> context)
    {
      Index = index;
      Context = context;
    }

    public int Index { get; }
    public NodeContext<TSource> Context { get; }
  }
}
