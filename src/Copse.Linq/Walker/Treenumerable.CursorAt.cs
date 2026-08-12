using Copse;

namespace Copse.Linq
{
  public static partial class Treenumerable
  {
    /// <summary>
    /// Enter the comonadic view: a cursor standing at <paramref name="handle"/>. Choosing a
    /// focus is an explicit act -- handles come from recording positions while consuming
    /// (<see cref="GetHandles{TValue, THandle}"/>) or from the root door below, never from
    /// value search -- and there is deliberately no door that produces an unfocused cursor.
    /// The handle is presumed to be one this walkable issued (the foreign-handle clause).
    /// </summary>
    public static TreeCursor<TValue, THandle> CursorAt<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      THandle handle)
      => new TreeCursor<TValue, THandle>(source, handle);

    /// <summary>
    /// The root door: a cursor standing at root <paramref name="rootIndex"/>, or an empty
    /// result past the last root. Result-typed because the probe can miss (a forest may have
    /// fewer roots, or none) -- the no-unfocused-cursor invariant, kept at the door.
    /// </summary>
    public static TreeCursorResult<TValue, THandle> GetRootCursor<TValue, THandle>(
      this IWalkableTreenumerable<TValue, THandle> source,
      int rootIndex = 0)
    {
      var rootResult = source.GetRootAt(rootIndex);

      return rootResult.HasChild
        ? new TreeCursorResult<TValue, THandle>(new TreeCursor<TValue, THandle>(source, rootResult.Child.Node))
        : default;
    }
  }
}
