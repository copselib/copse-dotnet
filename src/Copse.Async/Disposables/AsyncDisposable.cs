// The async dual of the lifted Disposable factory (System.Reactive, see
// Copse.Primitives/Disposables): Empty and Create over IAsyncDisposable. The codegen transcribes
// AsyncDisposable.Create(x.DisposeAsync) to Disposable.Create(x.Dispose), so async sources using
// this algebra produce sync twins over the lifted originals.

using System;
using System.Threading.Tasks;

namespace Copse.Disposables
{
  /// <summary>
  /// Provides a set of static methods for creating <see cref="IAsyncDisposable"/> objects.
  /// </summary>
  public static class AsyncDisposable
  {
    /// <summary>
    /// Represents an async disposable that does nothing on disposal.
    /// </summary>
    private sealed class EmptyAsyncDisposable : IAsyncDisposable
    {
      /// <summary>
      /// Singleton default async disposable.
      /// </summary>
      public static readonly EmptyAsyncDisposable Instance = new EmptyAsyncDisposable();

      private EmptyAsyncDisposable()
      {
      }

      /// <summary>
      /// Does nothing.
      /// </summary>
      public ValueTask DisposeAsync() => default;
    }

    /// <summary>
    /// Gets the async disposable that does nothing when disposed.
    /// </summary>
    public static IAsyncDisposable Empty => EmptyAsyncDisposable.Instance;

    /// <summary>
    /// Creates an async disposable object that awaits the specified function when disposed.
    /// </summary>
    /// <param name="dispose">Function to await during the first call to <see cref="IAsyncDisposable.DisposeAsync"/>.
    /// The function is guaranteed to be awaited at most once.</param>
    /// <returns>The async disposable object that awaits <paramref name="dispose"/> upon disposal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dispose"/> is <c>null</c>.</exception>
    public static IAsyncDisposable Create(Func<ValueTask> dispose)
    {
      if (dispose == null)
        throw new ArgumentNullException(nameof(dispose));

      return new AnonymousAsyncDisposable(dispose);
    }
  }
}
