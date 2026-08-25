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
    public TreenumeratorWrapper(
      Func<ITreenumerator<TInner>> innerTreenumeratorFactory)
    {
      InnerTreenumerator = innerTreenumeratorFactory();
    }

    protected ITreenumerator<TInner> InnerTreenumerator { get; }

    protected override void OnDisposing()
    {
      base.OnDisposing();

      InnerTreenumerator?.Dispose();
    }
  }

  /// <summary>The value-preserving form of <see cref="TreenumeratorWrapper{TInner, TNode}"/>: inner and outer share one node type.</summary>
  public abstract class TreenumeratorWrapper<TNode> : TreenumeratorWrapper<TNode, TNode>
  {
    protected TreenumeratorWrapper(
      Func<ITreenumerator<TNode>> innerTreenumeratorFactory)
      : base(innerTreenumeratorFactory)
    {
    }
  }
}
