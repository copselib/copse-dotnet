using Copse.Core;
using System;

namespace Copse.Traversal
{
  /// <summary>A scheduled node: its visit state and its child enumerator in one slot, only ever touched by ref.</summary>
  internal struct BreadthFirstFrame<TNode, TEnumerator>
    where TEnumerator : IDisposable
  {
    public BreadthFirstFrame(TNode node, NodePosition position, TEnumerator childEnumerator)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
      ChildEnumerator = childEnumerator;
    }

    public TNode Node;
    public NodePosition Position;
    public int VisitCount;
    public TEnumerator ChildEnumerator;
  }
}
