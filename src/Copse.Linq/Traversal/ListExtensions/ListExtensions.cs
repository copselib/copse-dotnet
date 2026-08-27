using System.Collections.Generic;

namespace Copse.Linq
{
  internal static class ListExtensions
  {
    public static void RemoveLast<T>(this IList<T> list) => list.RemoveAt(list.Count - 1);
  }
}
