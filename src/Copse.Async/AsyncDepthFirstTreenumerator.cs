using Copse.Core;
using Copse.Core.Async;
using Copse.Engine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Copse.Async
{
  /// <summary>
  /// Depth-first <b>async</b> treenumerator. A thin driver over the shared
  /// <see cref="DepthFirstCadence{TNode, TEnumerator}"/>: it owns the roots enumerator and the value
  /// map, and performs the two awaited pulls (child, root) the cadence requests. Every structural
  /// decision lives in the (synchronous) cadence; this class differs from the future sync driver only
  /// in that its two seams are <c>await</c>ed.
  /// </summary>
  public sealed class AsyncDepthFirstTreenumerator<TValue, TNode, TAsyncChildEnumerator>
    : IAsyncTreenumerator<TValue>
    where TAsyncChildEnumerator : IAsyncChildEnumerator<TNode>
  {
    public AsyncDepthFirstTreenumerator(
      IAsyncEnumerable<TNode> rootNodes,
      Func<NodeContext<TNode>, TAsyncChildEnumerator> childEnumeratorFactory,
      Func<TNode, TValue> map)
    {
      _RootsEnumerator = rootNodes.GetAsyncEnumerator();
      _Cadence = new DepthFirstCadence<TNode, TAsyncChildEnumerator>(childEnumeratorFactory);
      _Map = map;
    }

    private readonly IAsyncEnumerator<TNode> _RootsEnumerator;
    private DepthFirstCadence<TNode, TAsyncChildEnumerator> _Cadence;
    private readonly Func<TNode, TValue> _Map;

    private bool _Finished;

    public TValue Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      _Cadence.BeginMove(nodeTraversalStrategies);

      while (true)
      {
        switch (_Cadence.Advance())
        {
          case DepthFirstStep.Emit:
            Publish();
            return true;

          case DepthFirstStep.NeedTopChild:
          {
            // Start the awaited pull WITHOUT holding a ref across the await: grab the ValueTask, await
            // it, then re-read the enumerator by ref to lift its Current.
            bool hasChild = await _Cadence.TopEnumerator.MoveNextAsync().ConfigureAwait(false);
            if (hasChild)
            {
              var child = _Cadence.TopEnumerator.Current;
              _Cadence.SupplyChild(true, child.Node, child.SiblingIndex);
            }
            else
            {
              _Cadence.SupplyChild(false, default, 0);
            }
            continue;
          }

          case DepthFirstStep.NeedRoot:
          {
            bool hasRoot = await _RootsEnumerator.MoveNextAsync().ConfigureAwait(false);
            _Cadence.SupplyRoot(hasRoot, hasRoot ? _RootsEnumerator.Current : default);
            continue;
          }

          default: // Done
            _Finished = true;
            return false;
        }
      }
    }

    private void Publish()
    {
      // No await between here and Advance()==Emit, so taking a ref into the cadence is safe.
      ref var node = ref _Cadence.Current;
      Mode = node.VisitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
      Node = _Map(node.Node);
      VisitCount = node.VisitCount;
      Position = node.Position;
    }

    public async ValueTask DisposeAsync()
    {
      _Cadence.Dispose();
      await _RootsEnumerator.DisposeAsync().ConfigureAwait(false);
    }
  }
}
