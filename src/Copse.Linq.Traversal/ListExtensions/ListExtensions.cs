using System.Collections.Generic;

namespace Copse.Linq.Extensions
{
  internal static class ListExtensions
  {
    public static void RemoveLast<T>(this IList<T> list) => list.RemoveAt(list.Count - 1);

    public static void ReplaceLast<T>(this IList<T> list, T value) => list[list.Count - 1] = value;
  }
}
