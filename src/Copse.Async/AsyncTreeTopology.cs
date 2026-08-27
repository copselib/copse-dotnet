using Copse.Topologies;
using Copse.Core;

namespace Copse
{
  /// <summary>Factories for <see cref="IAsyncTreeTopology{TNode, THandle}"/> instances, the
  /// topology counterpart of the <c>Tree</c> factories.</summary>
  public static class AsyncTreeTopology
  {
    /// <summary>A topology over <paramref name="source"/>, acquired lazily: the source's
    /// walker is obtained once, at the first probe, and the topology it carries answers every
    /// probe after -- <c>Tree.Lazy</c>'s semantics at the topology level. Constructing this
    /// forces nothing, which is what a view composed over an arbitrary walkable needs. Over an
    /// empty forest, probes answer absent and <c>GetNodeAsync</c> throws (no handle is valid
    /// there).</summary>
    public static IAsyncTreeTopology<TNode, THandle> Lazy<TNode, THandle>(
      IAsyncWalkableTreenumerable<TNode, THandle> source)
      => new AsyncLazyTopology<TNode, THandle>(source);
  }
}
