using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  // The return-shaped relabels answer a node's whole group at once: one answer per seat, in
  // seat order, or the operator refuses the answer.
  internal static class DagSeats
  {
    public static void RequireOnePerSeat<TAnswer>(string operatorName, int ordinal, IReadOnlyList<TAnswer> answers, int seats, string unit)
    {
      if (answers == null || answers.Count != seats)
        throw new InvalidOperationException(
          $"{operatorName} at ordinal {ordinal} returned {answers?.Count.ToString() ?? "null"} {unit} for {seats} edges; one per edge, in group order.");
    }
  }
}
