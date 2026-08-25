using Copse.Core;
using System;

namespace Copse.Traversal
{
  /// <summary>A scheduled node: its visit state and its child enumerator in one slot, only ever touched by ref.</summary>
  internal struct BreadthFirstFrame<THandle, TEnumerator>
    where TEnumerator : IDisposable
  {
    public BreadthFirstFrame(THandle node, NodePosition position, TEnumerator childEnumerator)
    {
      Node = node;
      Position = position;
      VisitCount = 0;
      ChildEnumerator = childEnumerator;
    }

    public THandle Node;
    public NodePosition Position;
    public int VisitCount;
    public TEnumerator ChildEnumerator;
  }
}
