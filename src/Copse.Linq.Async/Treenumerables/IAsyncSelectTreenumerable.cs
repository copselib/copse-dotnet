using Copse.Core;
using System;

namespace Copse.Linq.Treenumerables
{
  /// <summary>
  /// The PUBLIC projection citizenship, streaming tier
  /// (design-docs/SELECT_INTO_CAPTURES_DESIGN.md): a treenumerable that can compose a
  /// projection into its own machinery instead of being wrapped. <c>Select</c> probes for
  /// this citizenship (after the internal composition lattice, before the wrapper
  /// fallback), so implementing this interface is the ENTIRE act of joining -- no
  /// registration, no cooperation from this library.
  ///
  /// <para>The contract is deliberately minimal and FINAL (the supported older TFMs have
  /// no default interface members, so any member added later would break every
  /// implementer): one method, value-selector flavor only -- the positional Select flavor
  /// always takes the wrapper over citizens, the same guard the internal lattice applies
  /// through its <c>ComposePositional</c> door.</para>
  ///
  /// <para>The laws (the admission test; the battery in Copse.TestUtils pins them for any
  /// claimant): <c>ComposeSelect</c> must be extensionally equal to wrapper-Select over
  /// this source -- values projected, positions and visit stream untouched -- and must
  /// satisfy the functor laws: identity composes to the source's behavior, and
  /// <c>ComposeSelect(f)</c> then <c>ComposeSelect(g)</c> equals
  /// <c>ComposeSelect(g after f)</c>. The return type IS the citizenship: closure
  /// (Select over a citizen stays a citizen) is enforced by the signature.</para>
  /// </summary>
  public interface IAsyncSelectTreenumerable<TNode> : IAsyncTreenumerable<TNode>
  {
    /// <summary>
    /// Compose <paramref name="selector"/> into this treenumerable's own machinery,
    /// returning a citizen that produces the projected values with this source's exact
    /// shape. Cost contract: no worse than the wrapper it replaces; the point of
    /// citizenship is that it is usually strictly better.
    /// </summary>
    IAsyncSelectTreenumerable<TResult> ComposeSelect<TResult>(Func<TNode, TResult> selector);
  }
}
