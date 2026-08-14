using Copse.Core.Async;

namespace Copse.Linq
{
  /// <summary>
  /// Async LINQ-style tree operators over <see cref="IAsyncTreenumerable{TNode}"/>. Sits in the
  /// <c>Copse.Linq</c> namespace alongside the synchronous <see cref="Treenumerable"/>, exactly as
  /// <c>System.Linq.AsyncEnumerable</c> sits alongside <c>Enumerable</c>: deferred operators keep their
  /// sync names (no <c>Async</c> suffix) and are overload-resolved by the async receiver type; terminal
  /// operators carry the <c>Async</c> suffix (they return an awaitable).
  ///
  /// <para>Each operator lives in its own <c>AsyncTreenumerable.&lt;Op&gt;.cs</c> file, mirroring the
  /// sync <c>Treenumerable.&lt;Op&gt;.cs</c> split. Deferred operators build the composite result via
  /// <c>AsyncDelegatingTreenumerable</c> -- the async analog of the sync delegating factory -- so the
  /// per-operator files carry only the fluent method, not a bespoke treenumerable wrapper.</para>
  /// </summary>
  public static partial class AsyncTreenumerable
  {
  }
}
