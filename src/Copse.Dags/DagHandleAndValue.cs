namespace Copse.Dags
{
  /// <summary>
  /// A handle paired with the value it labels -- one row of a walkable dag's labeling, the
  /// acquisition scan's element (<see cref="Dagnumerable.GetHandlesWithValues{TValue, THandle, TEdge}"/>).
  /// The dag family's twin of the tree vocabulary's <c>HandleAndValue</c>: the project stays
  /// self-contained, so shared vocabulary gets dag twins rather than references.
  /// </summary>
  public readonly struct DagHandleAndValue<THandle, TValue>
  {
    public DagHandleAndValue(THandle handle, TValue value)
    {
      Handle = handle;
      Value = value;
    }

    public readonly THandle Handle;
    public readonly TValue Value;

    public override string ToString() => $"{Handle}: {Value}";
  }
}
