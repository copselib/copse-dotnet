using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The trust door: a walker standing at <paramref name="handle"/> -- door, then jump. Pure
    /// construction, cannot fail; a forged handle detonates at the first probe. Bare <c>Get</c>
    /// by the Try law: choosing a focus is trust-based, so there is no typed miss. Stored
    /// handles re-enter here.
    /// </summary>
    public static DagWalker<TValue, THandle, TEdge> GetDagWalkerAt<TValue, THandle, TEdge>(
      this IWalkableDagnumerable<TValue, THandle, TEdge> source,
      THandle handle)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      return source.GetDagWalker().At(handle);
    }
  }
}
