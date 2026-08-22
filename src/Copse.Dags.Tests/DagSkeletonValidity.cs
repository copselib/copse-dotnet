using System;

namespace Copse.Dags.Tests
{
  // The dag skeleton's validity predicate as code (the foundation restatement's conditional-laws
  // bargain, dag-side: the comonad laws hold per representation, conditional on that
  // representation's validity). A (values, out-CSR) pair is a legal dag skeleton iff the CSR is
  // well-formed (offsets monotone from zero to the edge count, targets in range) and TOPOLOGICAL
  // (every out-edge points to a strictly later ordinal -- dense ordinals are a topological order,
  // which is what makes the Sourcefix/Sinkfix folds one pass). A third leg, TRANSPOSE CONSISTENCY,
  // checks an in-adjacency against the out-adjacency it claims to reverse. Each lie gets a
  // coordinate-bearing message.
  public static class DagSkeletonValidity
  {
    public static void AssertValid(int nodeCount, int[] outOffsets, int[] outTargets)
    {
      if (outOffsets.Length != nodeCount + 1)
        throw new InvalidOperationException($"Out-offsets must have {nodeCount + 1} entries, found {outOffsets.Length}.");
      if (outOffsets[0] != 0)
        throw new InvalidOperationException($"Out-offsets must start at 0, found {outOffsets[0]}.");
      if (outOffsets[nodeCount] != outTargets.Length)
        throw new InvalidOperationException($"Out-offsets must end at the edge count {outTargets.Length}, found {outOffsets[nodeCount]}.");

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        if (outOffsets[ordinal + 1] < outOffsets[ordinal])
          throw new InvalidOperationException($"Out-offsets decrease at ordinal {ordinal}: {outOffsets[ordinal]} -> {outOffsets[ordinal + 1]}.");

        for (var slot = outOffsets[ordinal]; slot < outOffsets[ordinal + 1]; slot++)
        {
          if (outTargets[slot] < 0 || outTargets[slot] >= nodeCount)
            throw new InvalidOperationException($"Edge slot {slot} of ordinal {ordinal} targets {outTargets[slot]}, outside [0, {nodeCount}).");
          if (outTargets[slot] <= ordinal)
            throw new InvalidOperationException($"Edge slot {slot} of ordinal {ordinal} targets {outTargets[slot]}: not topological (targets must be strictly later ordinals).");
        }
      }
    }

    public static void AssertTransposeConsistent(int nodeCount, int[] outOffsets, int[] outTargets, int[] inOffsets, int[] inParents)
    {
      if (inOffsets.Length != nodeCount + 1 || inParents.Length != outTargets.Length)
        throw new InvalidOperationException("In-adjacency sizes do not match the out-adjacency.");

      // Multiset equality per node: the in-edge group's parents must be exactly the sources of
      // the out-edges targeting it, parallel edges counted.
      var expectedParents = new System.Collections.Generic.List<int>[nodeCount];
      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        expectedParents[ordinal] = new System.Collections.Generic.List<int>();
      for (var parent = 0; parent < nodeCount; parent++)
        for (var slot = outOffsets[parent]; slot < outOffsets[parent + 1]; slot++)
          expectedParents[outTargets[slot]].Add(parent);

      for (var ordinal = 0; ordinal < nodeCount; ordinal++)
      {
        var actual = new System.Collections.Generic.List<int>();
        for (var slot = inOffsets[ordinal]; slot < inOffsets[ordinal + 1]; slot++)
          actual.Add(inParents[slot]);
        actual.Sort();
        expectedParents[ordinal].Sort();

        var agrees = actual.Count == expectedParents[ordinal].Count;
        for (var index = 0; agrees && index < actual.Count; index++)
          agrees = actual[index] == expectedParents[ordinal][index];

        if (!agrees)
          throw new InvalidOperationException(
            $"In-edge group of ordinal {ordinal} lists parents [{string.Join(",", actual)}] but the out-adjacency gives [{string.Join(",", expectedParents[ordinal])}].");
      }
    }
  }
}
