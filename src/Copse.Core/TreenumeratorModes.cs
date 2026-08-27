using System.Runtime.CompilerServices;

namespace Copse.Core
{
  /// <summary>The one place the visit-count law is spelled: a node is scheduled at visit
  /// count 0 and visited at every count after.</summary>
  public static class TreenumeratorModes
  {
    /// <summary>The mode a visit event with <paramref name="visitCount"/> is in:
    /// <see cref="TreenumeratorMode.SchedulingNode"/> at 0,
    /// <see cref="TreenumeratorMode.VisitingNode"/> from 1 up.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TreenumeratorMode FromVisitCount(int visitCount)
      => visitCount == 0 ? TreenumeratorMode.SchedulingNode : TreenumeratorMode.VisitingNode;
  }
}
