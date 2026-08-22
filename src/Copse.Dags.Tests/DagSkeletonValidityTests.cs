using System;
using Copse.Dags;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Copse.Dags.Tests
{
  // The validity predicate's own pins: the diamond's skeleton passes; each lie -- wrong offset
  // length, a decreasing offset, a target out of range, a non-topological target, an
  // in-adjacency that does not reverse the out-adjacency -- is refused with its coordinates.
  [TestClass]
  public class DagSkeletonValidityTests
  {
    // apex(0) -> left(1), right(2); left -> venture(3); right -> venture(3).
    private static readonly int[] Offsets = { 0, 2, 3, 4, 4 };
    private static readonly int[] Targets = { 1, 2, 3, 3 };

    [TestMethod]
    public void TheDiamondSkeleton_IsValid_AndTransposeConsistent()
    {
      DagSkeletonValidity.AssertValid(4, Offsets, Targets);
      DagSkeletonValidity.AssertTransposeConsistent(4, Offsets, Targets, new[] { 0, 0, 1, 2, 4 }, new[] { 0, 0, 1, 2 });
    }

    [TestMethod]
    public void EachLie_IsRefusedWithItsCoordinates()
    {
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => DagSkeletonValidity.AssertValid(3, Offsets, Targets)).Message, "must have 4 entries");
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => DagSkeletonValidity.AssertValid(4, new[] { 0, 3, 2, 4, 4 }, Targets)).Message, "decrease at ordinal 1");
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => DagSkeletonValidity.AssertValid(4, Offsets, new[] { 1, 2, 3, 7 })).Message, "outside [0, 4)");
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => DagSkeletonValidity.AssertValid(4, Offsets, new[] { 1, 2, 0, 3 })).Message, "not topological");
      StringAssert.Contains(Assert.ThrowsException<InvalidOperationException>(() => DagSkeletonValidity.AssertTransposeConsistent(4, Offsets, Targets, new[] { 0, 0, 1, 2, 4 }, new[] { 0, 0, 2, 2 })).Message, "In-edge group of ordinal 3 lists parents [2,2] but the out-adjacency gives [1,2]");
    }
  }
}
