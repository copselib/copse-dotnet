namespace Copse
{
  // The struct-return result of one forward-only level-order read within a group: HasValue == false
  // means the current group is finished. The async-legal counterpart of ILevelOrderStream's
  // (out value) -- out params can't cross an await -- so sync and async share one codegen source.
  // (Group-boundary and skip-count signals stay bool/int; only the value read needed a struct.)
  public readonly struct LevelOrderRead<TValue>
  {
    public LevelOrderRead(TValue value)
    {
      HasValue = true;
      Value = value;
    }

    public readonly bool HasValue;
    public readonly TValue Value;
  }
}
