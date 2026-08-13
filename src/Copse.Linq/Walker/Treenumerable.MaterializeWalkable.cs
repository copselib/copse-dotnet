using Copse.Core;
using Copse.Linq.Treenumerables;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// The walker escalation -- now literally a declared-layout <c>Materialize</c>, because the
    /// buffer re-parent made every capture walkable ("captures are never address-poor",
    /// docs/WALKABLE_CONTRACT_DESIGN.md): the return type IS the intersection the PoC once
    /// spelled as a separate interface -- <see cref="ITreenumerableBuffer{TValue}"/> is an
    /// <see cref="IWalkableTreenumerable{TValue, THandle}"/> over ordinal handles, adjacency
    /// and capture in one citizen. Deferred per the lazy-Materialize law (nothing enumerated
    /// at the call; the pin lands at the call, the capture at the first pull or probe), and
    /// preorder per the adjacency-first rider -- the ancestry-cheap layout, and the ordinal
    /// space the walker tier's hand-pinned expectations speak.
    ///
    /// <para>Scheduled to dissolve into <c>Materialize</c> itself (OPEN-3, ratified): this
    /// alias survives only until the walker tier's call sites migrate.</para>
    /// </summary>
    public static ITreenumerableBuffer<TValue> MaterializeWalkable<TValue>(this ITreenumerable<TValue> source)
      => source.Materialize(BufferLayout.Preorder);
  }
}
