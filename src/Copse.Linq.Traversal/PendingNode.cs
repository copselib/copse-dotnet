using Copse;

namespace Copse.Linq
{
  // A node awaiting its subtree to close during a leaffix pass: its slot index in the flat
  // accumulation buffer plus the context handed to the seed-selector/accumulator/survey. Shared
  // by the sync LeaffixScan/LeaffixDispatch/LeaffixAggregate and their async analogs.
  internal readonly struct PendingNode<TNode>
  {
    public PendingNode(int index, NodeContext<TNode> context)
    {
      Index = index;
      Context = context;
    }

    public int Index { get; }
    public NodeContext<TNode> Context { get; }
  }
}
