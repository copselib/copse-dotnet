using Copse.Core;
using Copse.Engine;
using System;
using System.Collections.Generic;

namespace Copse.Treenumerators
{
  /// <summary>
  /// Depth-first <b>synchronous</b> treenumerator built on the shared
  /// <see cref="DepthFirstCadence{TNode, TEnumerator}"/> -- the Option-B counterpart to
  /// <see cref="DepthFirstTreenumerator{TValue, TNode, TChildEnumerator}"/>, which inlines the same
  /// cadence by hand. Its purpose is the A/B: same visit stream, driven by the shared cadence, so the
  /// conformance battery can validate the cadence and the benchmark can price the seam inversion
  /// against the hand-tuned engine.
  ///
  /// <para>The driver body is deliberately identical in shape to the async driver
  /// (Copse.Async.AsyncDepthFirstTreenumerator), differing only in that the two pulls are synchronous
  /// here and awaited there -- the whole point of extracting the cadence.</para>
  /// </summary>
  public sealed class DepthFirstCadenceTreenumerator<TValue, TNode, TChildEnumerator>
    : TreenumeratorBase<TValue>
    where TChildEnumerator : IChildEnumerator<TNode>
  {
    public DepthFirstCadenceTreenumerator(
      IEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetEnumerator();
      _Cadence = new DepthFirstCadence<TNode, TChildEnumerator>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IEnumerator<TNode> _RootsEnumerator;
    private DepthFirstCadence<TNode, TChildEnumerator> _Cadence;
    private readonly Func<TNode, TValue> _Map;

    protected override bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategies)
    {
      _Cadence.BeginMove(nodeTraversalStrategies);

      while (true)
      {
        switch (_Cadence.Advance())
        {
          case DepthFirstStep.Emit:
            Publish();
            return true;

          case DepthFirstStep.NeedTopChild:
            if (_Cadence.TopEnumerator.MoveNext(out var child))
              _Cadence.SupplyChild(true, child.Node, child.SiblingIndex);
            else
              _Cadence.SupplyChild(false, default, 0);
            continue;

          case DepthFirstStep.NeedRoot:
            if (_RootsEnumerator.MoveNext())
              _Cadence.SupplyRoot(true, _RootsEnumerator.Current);
            else
              _Cadence.SupplyRoot(false, default);
            continue;

          default: // Done
            return false;
        }
      }
    }

    private void Publish()
    {
      ref var node = ref _Cadence.Current;
      Mode = node.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(node.Node);
      VisitCount = node.VisitCount;
      Position = node.Position;
    }

    protected override void OnDisposing()
    {
      base.OnDisposing();

      _RootsEnumerator?.Dispose();
      _Cadence.Dispose();
    }
  }
}
