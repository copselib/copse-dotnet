using System;

namespace Copse.Linq.Treenumerables
{
  /// <summary>
  /// The PUBLIC projection citizenship, capture tier
  /// (design-docs/SELECT_INTO_CAPTURES_DESIGN.md): a buffer whose projection composes into
  /// the CAPTURE ITSELF -- for a deferred buffer, into the pending build (the product is
  /// manufactured already-projected; the un-projected intermediate never exists), and for
  /// a settled one, into a fresh capture of projected values. Either way the result is a
  /// buffer: the buffer-producer rule discloses the O(n) product in the return type.
  ///
  /// <para>A separate interface from the streaming citizenship because the tiers' results
  /// differ in kind and the language cannot abstract over that (a buffer citizen composes
  /// to a buffer, a streaming citizen to a treenumerable). Same minimal-and-FINAL
  /// contract, same laws, same battery -- see the streaming twin's remarks.</para>
  /// </summary>
  public interface IAsyncSelectTreenumerableBuffer<TNode> : IAsyncTreenumerableBuffer<TNode>
  {
    /// <summary>
    /// Compose <paramref name="selector"/> into this capture, returning a buffer citizen
    /// of projected values with this capture's exact shape. Deferred like every capture:
    /// composing builds nothing; the first pull does.
    /// </summary>
    IAsyncSelectTreenumerableBuffer<TResult> ComposeSelect<TResult>(Func<TNode, TResult> selector);
  }
}
