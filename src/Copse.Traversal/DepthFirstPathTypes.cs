using Copse.Core;

namespace Copse.Traversal
{
  /// <summary>The visit-state of one accepted node on the depth-first path.</summary>
  internal struct DepthFirstNodeState<TNode>
  {
    public DepthFirstNodeState(TNode node, NodePosition position)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
    }

    public TNode Node;
    public NodePosition Position;
    public int VisitCount;
  }

  /// <summary>What the level the engine just backtracked to needs next (from <c>PopFinishedLevelAndClassify</c>).</summary>
  internal enum DepthFirstBacktrackStep
  {
    GoToRoot,          // The whole forest path is unwound; schedule the next root.
    PromoteNextChild,  // No visit owed here; advance this level's enumerator.
    EmitReturnVisit,   // The accepted node here owes its next between/after-children visit.
  }
}
