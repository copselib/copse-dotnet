using Copse.Core.Async;

namespace Copse.Async.Topologies
{
  /// <summary>The topology tier's creation surface, beside <c>Tree</c>'s (the treenumerable
  /// tier's) -- factories hand out the contract; the implementations stay sealed.</summary>
  public static class AsyncTreeTopology
  {
    /// <summary>The walkable's topology, call-by-need: the door is knocked once, at the
    /// first probe, and the bound topology is cached for every answer after --
    /// <c>Tree.Lazy</c>'s semantics at the topology tier. This is the deferral any view
    /// over an arbitrary walkable needs (a constructor may neither await a door nor force
    /// the source it composes over; the operator tier's lens family builds on exactly
    /// this): acquire lazily, knock once, and let the empty forest answer as itself --
    /// probes miss honestly, and <c>GetValue</c> throws because on an empty forest every
    /// handle is forged (the two-channel doctrine: typed results for misses, exceptions
    /// for violations).</summary>
    public static IAsyncTreeTopology<TValue, THandle> Lazy<TValue, THandle>(
      IAsyncWalkableTreenumerable<TValue, THandle> source)
      => new AsyncLazyTopology<TValue, THandle>(source);
  }
}
