using Copse.Core;
using Copse.Treenumerators;
using System;
using System.Collections.Generic;

namespace Copse.Treenumerables
{
  public class Treenumerable<TValue, TNode, TChildEnumerator> : ITreenumerable<TValue>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public Treenumerable(
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> nodeToValueMap,
      params TNode[] roots)
      : this(
          childEnumeratorFactory,
          nodeToValueMap,
          roots as IEnumerable<TNode>)
    {
    }

    public Treenumerable(
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> nodeToValueMap,
      IEnumerable<TNode> roots)
    {
      _ChildEnumeratorFactory = childEnumeratorFactory;
      _NodeToValueMap = nodeToValueMap;
      _Roots = roots;
    }

    private readonly IEnumerable<TNode> _Roots;
    private readonly Func<NodeContext<TNode>, TChildEnumerator> _ChildEnumeratorFactory;
    private readonly Func<TNode, TValue> _NodeToValueMap;

    public ITreenumerator<TValue> GetBreadthFirstTreenumerator()
    {
      return
        new BreadthFirstTreenumerator<TValue, TNode, TChildEnumerator>(
          _Roots,
          _ChildEnumeratorFactory,
          _NodeToValueMap);
    }

    public ITreenumerator<TValue> GetDepthFirstTreenumerator()
    {
      return
        new DepthFirstTreenumerator<TValue, TNode, TChildEnumerator>(
          _Roots,
          _ChildEnumeratorFactory,
          _NodeToValueMap);
    }
  }

  // Convenience base for trees whose node IS its surfaced value (TValue == TNode): the value map is
  // the identity, so callers don't supply one. Trees with a distinct internal handle (e.g.
  // PreorderTree's int index) use the three-parameter base above with an explicit resolution map.
  public class Treenumerable<TNode, TChildEnumerator>
    : Treenumerable<TNode, TNode, TChildEnumerator>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public Treenumerable(
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      params TNode[] roots)
      : base(childEnumeratorFactory, node => node, roots)
    {
    }

    public Treenumerable(
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      IEnumerable<TNode> roots)
      : base(childEnumeratorFactory, node => node, roots)
    {
    }
  }
}
