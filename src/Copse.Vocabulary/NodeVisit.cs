using System;

namespace Copse.Core
{
  public readonly struct NodeVisit<TNode>
  {
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

    public TreenumeratorMode Mode { get; }
    public TNode Node { get; }
    public int VisitCount { get; }
    public NodePosition Position { get; }

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
