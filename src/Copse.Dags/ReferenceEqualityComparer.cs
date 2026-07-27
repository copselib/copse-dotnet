#if !NET5_0_OR_GREATER
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Collections.Generic
{
  internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>, IEqualityComparer
  {
    public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

    private ReferenceEqualityComparer()
    {
    }

    bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

    bool IEqualityComparer.Equals(object x, object y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);

    int IEqualityComparer.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
  }
}
#endif
