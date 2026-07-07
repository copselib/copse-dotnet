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
  public abstract class TreenumeratorBase<TNode> : ITreenumerator<TNode>
  {
    public TNode Node { get; protected set; } = default;

    public int VisitCount { get; protected set; } = 0;

    public NodePosition Position { get; protected set; } = NodePosition.ForestRoot;

    public TreenumeratorMode Mode { get; protected set; } = default;

    protected bool EnumerationFinished { get; private set; }


    public bool MoveNext(NodeTraversalStrategies nodeTraversalStrategy)
    {
      if (Disposed || EnumerationFinished)
        return false;

      if (OnMoveNext(nodeTraversalStrategy))
        return true;

      EnumerationFinished = true;

      return false;
    }

    protected abstract bool OnMoveNext(NodeTraversalStrategies nodeTraversalStrategy);

    #region IDisposable

    protected bool Disposed { get; private set; } = false;

    // No finalizer: treenumerators hold only managed state (inner treenumerators, child
    // enumerators), so there is nothing for a finalize path to reclaim -- the canonical
    // Dispose(bool)/GC.SuppressFinalize/~Finalizer boilerplate would release nothing here (the
    // finalize path skips OnDisposing anyway). Disposal is just: run OnDisposing once. This also
    // keeps the base structurally parallel to AsyncTreenumeratorBase (DisposeAsync/OnDisposingAsync).
    public void Dispose()
    {
      if (Disposed)
        return;

      OnDisposing();
      Disposed = true;
    }

    protected virtual void OnDisposing()
    {
    }

    #endregion IDisposable
  }
}
