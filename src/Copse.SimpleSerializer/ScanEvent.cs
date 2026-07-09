namespace Copse.SimpleSerializer
{
  // The struct-return result of one value-token scan: Ok == false means the scanner was already
  // exhausted (the end token was delivered on a prior call). Otherwise HasValue says whether a
  // value was accumulated and Terminator is the structural character that ended it ('\0' at end of
  // text). The async-legal replacement for the scanner's (out hasValue, out terminator) pair -- out
  // params can't cross an await -- so the sync and async scanners share one codegen source.
  internal readonly struct ScanEvent
  {
    public ScanEvent(bool hasValue, char terminator)
    {
      Ok = true;
      HasValue = hasValue;
      Terminator = terminator;
    }

    public readonly bool Ok;
    public readonly bool HasValue;
    public readonly char Terminator;
  }
}
