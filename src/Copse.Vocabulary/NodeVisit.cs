using System;

namespace Copse.Core
{
  /// <summary>
  /// One event of a traversal's visit stream: which node, in which mode
  /// (<see cref="TreenumeratorMode.SchedulingNode"/> for the node's first appearance,
  /// <see cref="TreenumeratorMode.VisitingNode"/> for each return to it), how many visits it
  /// has received so far, and where it sits in the tree.
  /// </summary>
  public readonly struct NodeVisit<TNode>
  {
    /// <summary>Creates a visit record from its four facts.</summary>
    public NodeVisit(
      TreenumeratorMode mode,
      TNode node,
      int visitCount,
      NodePosition position)
    {
      Mode = mode;
      Node = node;
      VisitCount = visitCount;
      Position = position;
    }

    /// <summary>Whether this event schedules the node (first appearance) or visits it
    /// (a return to it while its children are being enumerated).</summary>
    public TreenumeratorMode Mode { get; }

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
    {
      return $"{Position}  {ModeToChar()}  {VisitCount}  {Node}";
    }

    private char ModeToChar()
    {
      switch (Mode)
      {
        case TreenumeratorMode.SchedulingNode:
          return 'S';
        case TreenumeratorMode.VisitingNode:
          return 'V';
        default:
          throw new NotImplementedException();
      }
    }
  }
}
