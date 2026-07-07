namespace Copse
{
  // The struct-return result of one forward-only preorder read: HasValue == false means the
  // stream is exhausted. This is the async-legal counterpart of IPreorderStream's
  // (out value, out depth) pair -- out params cannot cross an await, exactly the constraint that
  // made ChildResult replace IChildEnumerator's out-style child pull. Small and transient (returned
  // and immediately consumed, never stored per-frame), so it carries no allocation cost.
  public readonly struct PreorderRead<TValue>
  {
    public PreorderRead(TValue value, int depth)
    {
      HasValue = true;
      Value = value;
      Depth = depth;
    }

    public readonly bool HasValue;
    public readonly TValue Value;
    public readonly int Depth;
  }
}
