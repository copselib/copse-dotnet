// The async dual of the lifted RefCountDisposable (System.Reactive, see
// Copse.Primitives/Disposables): same Interlocked bit-31/count algebra, but the underlying
// disposable is an IAsyncDisposable and the terminal release awaits it.
//
// NOT a verbatim AsyncRx.NET lift: AsyncRx's RefCountAsyncDisposable acquires handles via an
// awaited GetDisposableAsync (its async-everywhere gate style). Here handle acquisition is pure
// Interlocked bookkeeping -- nothing about CREATING a handle suspends -- so GetDisposable stays
// synchronous, which lets sync-signature factory methods (GetAsyncDepthFirstTreenumerator and
// friends) hand out handles without becoming async themselves. Only disposal awaits.
//
// The codegen transcribes AsyncRefCountDisposable to RefCountDisposable (and DisposeAsync to
// Dispose), so async sources using this produce sync twins over the lifted original.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Disposables
{
  /// <summary>
  /// Represents an async disposable resource that only disposes its underlying disposable resource
  /// when all <see cref="GetDisposable">dependent disposable objects</see> have been disposed.
  /// </summary>
  public sealed class AsyncRefCountDisposable : IAsyncCancelable
  {
    private readonly bool _throwWhenDisposed;
    private IAsyncDisposable _disposable;

    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncRefCountDisposable"/> class with the specified disposable.
    /// </summary>
    public AsyncRefCountDisposable(IAsyncDisposable disposable) : this(disposable, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncRefCountDisposable"/> class with the specified disposable.
    /// </summary>
    public AsyncRefCountDisposable(IAsyncDisposable disposable, bool throwWhenDisposed)
    {
      _disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
      _count = 0;
      _throwWhenDisposed = throwWhenDisposed;
    }

    /// <summary>
    /// Gets a value that indicates whether the object is disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _count) == int.MinValue;

    /// <summary>
    /// Returns a dependent async disposable that when disposed decreases the refcount on the
    /// underlying disposable. Acquisition is synchronous (pure bookkeeping); only disposal awaits.
    /// </summary>
    public IAsyncDisposable GetDisposable()
    {
      // the current state
      var cnt = Volatile.Read(ref _count);

      for (; ; )
      {
        // If bit 31 is set and the active count is zero, don't create an inner
        if (cnt == int.MinValue)
        {
          if (_throwWhenDisposed)
          {
            throw new ObjectDisposedException("AsyncRefCountDisposable");
          }

          return AsyncDisposable.Empty;
        }

        // Should not overflow the bits 0..30
        if ((cnt & 0x7FFFFFFF) == int.MaxValue)
        {
          throw new OverflowException($"AsyncRefCountDisposable can't handle more than {int.MaxValue} disposables");
        }

        // Increment the active count by one, works because the increment
        // won't affect bit 31
        var u = Interlocked.CompareExchange(ref _count, cnt + 1, cnt);
        if (u == cnt)
        {
          return new InnerDisposable(this);
        }

        cnt = u;
      }
    }

    /// <summary>
    /// Disposes the underlying disposable only when all dependent disposables have been disposed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
      var cnt = Volatile.Read(ref _count);

      for (; ; )
      {
        // already marked as disposed via bit 31?
        if ((cnt & 0x80000000) != 0)
        {
          // yes, nothing to do
          break;
        }

        // how many active disposables are there?
        var active = cnt & 0x7FFFFFFF;

        // keep the active count but set the dispose marker of bit 31
        var u = int.MinValue | active;

        var b = Interlocked.CompareExchange(ref _count, u, cnt);

        if (b == cnt)
        {
          // if there were 0 active disposables, there can't be any more after
          // the CAS so we can dispose the underlying disposable
          if (active == 0)
          {
            var disposable = _disposable;
            _disposable = null;

            if (disposable != null)
              await disposable.DisposeAsync().ConfigureAwait(false);
          }

          break;
        }

        cnt = b;
      }
    }

    private async ValueTask ReleaseAsync()
    {
      var cnt = Volatile.Read(ref _count);

      for (; ; )
      {
        // extract the main disposed state (bit 31)
        var main = (int)(cnt & 0x80000000);

        // get the active count
        var active = cnt & 0x7FFFFFFF;

        // keep the main disposed state but decrement the counter
        // in theory, active should be always > 0 at this point,
        // guaranteed by the InnerDisposable.DisposeAsync's Exchange operation.
        Debug.Assert(active > 0);
        var u = main | (active - 1);

        var b = Interlocked.CompareExchange(ref _count, u, cnt);

        if (b == cnt)
        {
          // if after the CAS there was zero active disposables and
          // the main has been also marked for disposing,
          // it is safe to dispose the underlying disposable
          if (u == int.MinValue)
          {
            var disposable = _disposable;
            _disposable = null;

            if (disposable != null)
              await disposable.DisposeAsync().ConfigureAwait(false);
          }

          break;
        }

        cnt = b;
      }
    }

    private sealed class InnerDisposable : IAsyncDisposable
    {
      private AsyncRefCountDisposable _parent;

      public InnerDisposable(AsyncRefCountDisposable parent)
      {
        _parent = parent;
      }

      public async ValueTask DisposeAsync()
      {
        var parent = Interlocked.Exchange(ref _parent, null);

        if (parent != null)
          await parent.ReleaseAsync().ConfigureAwait(false);
      }
    }
  }
}
