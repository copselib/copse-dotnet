namespace Copse
{
  /// <summary>
  /// A row of the labeling function: a handle paired with the value it labels. Tuple-like by
  /// design -- neither field contains the other; they are two facts about one node (the handle
  /// names the point, the value is its label, GetValue is the mapping between them). What the
  /// acquisition scan (GetHandlesWithValues) yields, so predicates over values can pick out
  /// handles.
  ///
  /// <para>The first type to adopt the THandle parameter naming (ruled 2026-08-10: "if the
  /// thing is a handle, call it a handle"); the walkable contract's TNode awaits the same
  /// rename in the nomenclature cleanup wave. A named struct, not a tuple, so
  /// each field keeps the name that says which fact it is.</para>
  /// </summary>
  public readonly struct HandleAndValue<THandle, TValue>
  {
    public HandleAndValue(THandle handle, TValue value)
    {
      Handle = handle;
      Value = value;
    }

    public readonly THandle Handle;
    public readonly TValue Value;

    public override string ToString() => $"{Handle}  {Value}";
  }
}
