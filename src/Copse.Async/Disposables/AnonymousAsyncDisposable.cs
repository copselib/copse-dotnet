// The async dual of the lifted AnonymousDisposable (System.Reactive, see
// Copse.Primitives/Disposables): the disposal callback returns a ValueTask and is awaited.
// Same Interlocked.Exchange once-only semantics.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Disposables
{
  /// <summary>
  /// Represents a Func&lt;ValueTask&gt;-based async disposable.
  /// </summary>
  internal sealed class AnonymousAsyncDisposable : IAsyncCancelable
  {
    private volatile Func<ValueTask> _dispose;

    /// <summary>
    /// Constructs a new async disposable with the given function awaited upon disposal.
    /// </summary>
    /// <param name="dispose">Disposal function which will be awaited upon calling DisposeAsync.</param>
    public AnonymousAsyncDisposable(Func<ValueTask> dispose)
    {
      Debug.Assert(dispose != null);

      _dispose = dispose;
    }

    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => _dispose == null;

    /// <summary>
    /// Awaits the disposal function if and only if the current instance hasn't been disposed yet.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
      var dispose = Interlocked.Exchange(ref _dispose, null);

      if (dispose != null)
        await dispose().ConfigureAwait(false);
    }
  }
}
