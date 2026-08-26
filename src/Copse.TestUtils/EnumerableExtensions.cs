using Copse.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.TestUtils
{
  public static class EnumerableExtensions
  {
    public static IEnumerable<T> Do<T>(this IEnumerable<T> source, Action<T> action)
    {
      foreach (var item in source)
      {
        action(item);
        yield return item;
      }
    }

    // The declared mode is redundant with the visit count (NodeVisit derives it), but the
    // fixtures read better carrying it -- so it is CHECKED rather than dropped: a fixture row
    // whose mode disagrees with its count fails the test loudly instead of lying silently.
    public static NodeVisit<T>[] ToNodeVisitArray<T>(this IEnumerable<(TreenumeratorMode, T, int, (int, int))> source)
    {
      return
        source
        .Select(expected =>
        {
          var visit =
            new NodeVisit<T>(
              expected.Item2,
              expected.Item3,
              new NodePosition(expected.Item4.Item1, expected.Item4.Item2));

          if (visit.Mode != expected.Item1)
            throw new InvalidOperationException(
              $"Fixture row declares {expected.Item1} but visit count {expected.Item3} derives {visit.Mode}.");

          return visit;
        })
        .ToArray();
    }
  }
}
