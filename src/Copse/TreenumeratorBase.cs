using Copse.Core;

namespace Copse
{
  // Hand-written twin of AsyncTreenumeratorBase (they are a maintained pair, NOT codegen'd from
  // each other) even though the operators that derive from these ARE codegen'd. The base is the
  // one place a pure await-strip breaks: its disposal seam is a no-op virtual, and an async no-op
  // (`ValueTask OnDisposingAsync() => default;`) has no valid sync transcription -- `void
  // OnDisposing() => default;` is illegal, so single-sourcing the base would force the transcriber
  // to learn a SEMANTIC rule (rewrite a `default`-bodied method whose return just became void into
  // `{ }`) that serves this one file alone. The codegen is only trustworthy while it stays a dumb,
  // syntactic await-strip; buying ~40 lines of stable, rarely-touched base back at the cost of a
  // permanent special-case is the wrong trade. So the base stays a hand-written parallel pair; the
  // churn (the operators) is what the codegen earns its keep on.
  /// <summary>
  /// The base class for implementing a treenumerator: derive, implement <c>OnMoveNext</c> to
  /// advance and set the four state properties, and the base handles exhaustion, disposal
  /// idempotence, and teardown (override <c>OnDisposing</c>).
  /// </summary>
  public abstract class TreenumeratorBase<TNode> : ITreenumerator<TNode>
  {
    /// <inheritdoc/>
    public TNode Node { get; protected set; } = default;

    /// <inheritdoc/>
    public int VisitCount { get; protected set; } = 0;

    /// <inheritdoc/>
    public NodePosition Position { get; protected set; } = NodePosition.ForestRoot;

    /// <inheritdoc/>
    public TreenumeratorMode Mode { get; protected set; } = default;

    /// <summary>True once a pull has answered false; every later pull answers false without
    /// calling <see cref="OnMoveNext"/> again.</summary>
    protected bool EnumerationFinished { get; private set; }


    /// <inheritdoc/>
    public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategy)
    {
      if (Disposed || EnumerationFinished)
        return false;

      if (OnMoveNext(nodeTraversalStrategy))
        return true;

      EnumerationFinished = true;

      return false;
    }

    /// <summary>Advances the traversal one visit and sets <see cref="Node"/>,
    /// <see cref="VisitCount"/>, <see cref="Position"/>, and <see cref="Mode"/>; answers
    /// false when the traversal is exhausted. The base never calls this after exhaustion
    /// or disposal.</summary>
    protected abstract bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategy);

    #region IDisposable

    /// <summary>True once <see cref="Dispose"/> has run; pulls answer false from then on.</summary>
    protected bool Disposed { get; private set; } = false;

    // No finalizer: treenumerators hold only managed state (inner treenumerators, child
    // enumerators), so there is nothing for a finalize path to reclaim -- the canonical
    // Dispose(bool)/GC.SuppressFinalize/~Finalizer boilerplate would release nothing here (the
    // finalize path skips OnDisposing anyway). Disposal is just: run OnDisposing once. This also
    // keeps the base structurally parallel to AsyncTreenumeratorBase (DisposeAsync/OnDisposingAsync).
    /// <inheritdoc/>
    public void Dispose()
    {
      if (Disposed)
        return;

      OnDisposing();
      Disposed = true;
    }

    /// <summary>Teardown hook, run exactly once, before <see cref="Disposed"/> is set.</summary>
    protected virtual void OnDisposing()
    {
    }

    #endregion IDisposable
  }
}
