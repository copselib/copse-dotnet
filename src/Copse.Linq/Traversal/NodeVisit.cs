using Copse.Core;

namespace Copse.Linq
{
  /// <summary>
  /// One event of a traversal's visit stream: which node, how many visits it has received so
  /// far, and where it sits in the tree. Whether the event schedules the node or visits it is
  /// carried by <see cref="VisitCount"/> -- a node is scheduled exactly once, at count 0, and
  /// every visiting event has count 1 or more -- so <see cref="Mode"/> is derived, and an
  /// inconsistent visit record cannot be constructed.
  /// </summary>
  public readonly struct NodeVisit<TNode>
  {
    /// <summary>Creates a visit record from its three facts.</summary>
    public NodeVisit(
      TNode node,
      int visitCount,
      NodePosition position)
    {
      Node = node;
      VisitCount = visitCount;
      Position = position;
    }

    /// <summary>Whether this event schedules the node (first appearance, count 0) or visits
    /// it (a return to it while its children are being enumerated).</summary>
    public TreenumeratorMode Mode => TreenumeratorModes.FromVisitCount(VisitCount);

    /// <summary>The node this event is about.</summary>
    public TNode Node { get; }

    /// <summary>How many times the node has been visited so far: 0 while being scheduled,
    /// then 1 on its first visit, incrementing on each return.</summary>
    public int VisitCount { get; }

    /// <summary>The node's position (sibling index and depth) in the tree.</summary>
    public NodePosition Position { get; }

    /// <summary>Renders as "(siblingIndex, depth)  S|V  visitCount  node" -- the compact form
    /// used throughout the test suites' expected streams.</summary>
    public override string ToString()
      => $"{Position}  {(VisitCount == 0 ? 'S' : 'V')}  {VisitCount}  {Node}";
  }
}
