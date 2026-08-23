using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// The source door: a walker standing at source <paramref name="sourceIndex"/>, or the
    /// miss past the last source -- the door plus one downward step (the sources are the
    /// unfocused stance's child group). SourceIndex spelled out so ordinal-vs-handle stays
    /// visible when THandle is int.
    /// </summary>
    public static DagWalkerResult<TValue, THandle, TEdge> TryGetDagWalkerAtSourceIndex<TValue, THandle, TEdge>(
      this IWalkableDagnumerable<TValue, THandle, TEdge> source,
      int sourceIndex = 0)
    {
      if (source == null)
        throw new ArgumentNullException(nameof(source));

      return source.GetDagWalker().MoveToSource(sourceIndex);
    }
  }
}
