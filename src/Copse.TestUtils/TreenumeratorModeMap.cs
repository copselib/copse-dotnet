using Copse.Core;
using System;

namespace Copse.TestUtils
{
  internal static class TreenumeratorModeMap
  {
    public static char ToChar(TreenumeratorMode mode)
    {
      switch (mode)
      {
        case TreenumeratorMode.SchedulingNode:
          return 'S';
        case TreenumeratorMode.VisitingNode:
          return 'V';
        default:
          throw new InvalidOperationException();
      }
    }
  }
}
