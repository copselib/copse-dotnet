using Copse.Core;
using Copse.Core.Async;
using System;
using System.Threading.Tasks;

namespace Copse.Linq.Async
{
  /// <summary>
  /// <b>async</b> <c>PruneAfter</c>: the direct-style async port of
  /// <c>Copse.Linq.Treenumerators.PruneAfterTreenumerator</c>. Forwards the inner (async) visit stream
  /// unchanged except that a scheduled node matching the predicate keeps its own visit but sheds its
  /// subtree -- <see cref="NodeTraversalStrategies.SkipDescendants"/> is added to the pull. The only
  /// seam is the awaited inner pull. Dimension-agnostic (the strategy tweak is order-independent), so
  /// one class serves both traversal dimensions.
  ///
  /// <para><b>This is the codegen source of truth for the sync PruneAfter twin.</b> Strip the
  /// <c>await</c> and it becomes the synchronous driver.</para>
  /// </summary>
  public sealed class AsyncPruneAfterTreenumerator<TNode>
    : IAsyncTreenumerator<TNode>
  {
    public AsyncPruneAfterTreenumerator(
      Func<IAsyncTreenumerator<TNode>> innerTreenumeratorFactory,
      Func<NodeContext<TNode>, bool> predicate)
    {
      _Inner = innerTreenumeratorFactory();
      _Predicate = predicate;
    }

    private readonly IAsyncTreenumerator<TNode> _Inner;
    private readonly Func<NodeContext<TNode>, bool> _Predicate;

    private bool _Finished;

    public TNode Node { get; private set; } = default;
    public int VisitCount { get; private set; } = 0;
    public TreenumeratorMode Mode { get; private set; } = default;
    public NodePosition Position { get; private set; } = NodePosition.ForestRoot;

    public async ValueTask<bool> MoveNextAsync(NodeTraversalStrategies nodeTraversalStrategies)
    {
      if (_Finished)
        return false;

      // The strategy applies to the node just scheduled (the current node): if it matches the
      // predicate, prune its subtree while keeping the node itself.
      if (Mode == TreenumeratorMode.SchedulingNode && _Predicate(new NodeContext<TNode>(Node, Position)))
        nodeTraversalStrategies |= NodeTraversalStrategies.SkipDescendants;

      // THE SEAM: awaited inner pull.
      var moved = await _Inner.MoveNextAsync(nodeTraversalStrategies).ConfigureAwait(false);

      Mode = _Inner.Mode;

      if (moved)
      {
        Node = _Inner.Node;
        VisitCount = _Inner.VisitCount;
        Position = _Inner.Position;
      }
      else
      {
        _Finished = true;
      }

      return moved;
    }

    public ValueTask DisposeAsync() => _Inner.DisposeAsync();
  }
}
