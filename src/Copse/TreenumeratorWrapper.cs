using Copse.Core;
using System;

namespace Copse
{
  /// <summary>
  /// A base class for treenumerators that transform one inner traversal: holds the inner
  /// cursor as <c>InnerTreenumerator</c> and disposes it on teardown.
  /// </summary>
  public abstract class TreenumeratorWrapper<TInner, TNode>
    : TreenumeratorBase<TNode>
  {
    /// <summary>Acquires the inner cursor from the factory immediately; the wrapper owns and
    /// disposes it.</summary>
    public TreenumeratorWrapper(
      Func<ITreenumerator<TInner>> innerTreenumeratorFactory)
    {
      InnerTreenumerator = innerTreenumeratorFactory();
    }

    /// <summary>The wrapped traversal this treenumerator transforms.</summary>
    protected ITreenumerator<TInner> InnerTreenumerator { get; }

    /// <inheritdoc/>
    protected override void OnDisposing()
    {
      base.OnDisposing();

      InnerTreenumerator?.Dispose();
    }
  }

  /// <summary>The value-preserving form of <see cref="TreenumeratorWrapper{TInner, TNode}"/>: inner and outer share one node type.</summary>
  public abstract class TreenumeratorWrapper<TNode> : TreenumeratorWrapper<TNode, TNode>
  {
    /// <summary>Acquires the inner cursor from the factory immediately; the wrapper owns and
    /// disposes it.</summary>
    protected TreenumeratorWrapper(
      Func<ITreenumerator<TNode>> innerTreenumeratorFactory)
      : base(innerTreenumeratorFactory)
    {
    }
  }
}
