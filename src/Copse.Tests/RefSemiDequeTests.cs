using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Copse.Tests
{
  [TestClass]
  public class RefSemiDequeTests
  {
    [TestMethod]
    public void AddSingleItem()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();

      // Act
      sut.AddLast(1);

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 1 }));
    }


    [TestMethod]
    public void AddMoreItemsThanInitialPartitionCapacity()
    {
      // Arrange
      var sut = new RefSemiDeque<int>(1);

      // Act
      sut.AddLast(1);
      sut.AddLast(2);

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 1, 2 }));
    }

    [TestMethod]
    public void AddTwo_RemoveFirst()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();

      // Act
      sut.AddLast(1);
      sut.AddLast(2);
      sut.RemoveFirst();

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 2 }));
    }

    [TestMethod]
    public void AddTwo_RemoveLast()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();

      // Act
      sut.AddLast(1);
      sut.AddLast(2);
      sut.RemoveLast();

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 1 }));
    }

    [TestMethod]
    public void AddMoreItemsThanInitialPartitionCapacity_RemoveAllItemsInFirstPartition()
    {
      // Arrange
      var sut = new RefSemiDeque<int>(1);

      // Act
      sut.AddLast(1);
      sut.AddLast(2);
      sut.RemoveFirst();

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 2 }));
    }

    // --- Regression coverage for partition-size capping (MaxPartitionSize = 4096) ---
    //
    // Partition sizes ramp geometrically (8, 8, 16, 32, ... 2048) then stay flat at 4096.
    // Filling well past 4096 produces a heterogeneous, NOT size-ordered set of partitions once
    // emptied small front partitions are recycled to the back. These tests lock in that every
    // accessor reads partition lengths live (node.Value.Length) rather than assuming uniform or
    // monotonically increasing partition sizes.

    // Comfortably past the 4096 cap: spans the small ramp partitions, at least two full 4096
    // partitions, and a partially-filled back partition.
    private const int PastCapElementCount = 10_000;

    [TestMethod]
    public void AddPastPartitionCap_EnumeratesInOrder()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();

      // Act
      for (var i = 0; i < PastCapElementCount; i++)
        sut.AddLast(i);

      // Assert
      Assert.AreEqual(PastCapElementCount, sut.Count);
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), Enumerable.Range(0, PastCapElementCount)));
    }

    [TestMethod]
    public void GetFromBack_PastPartitionCap_ReturnsExpectedAcrossMixedPartitions()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();
      for (var i = 0; i < PastCapElementCount; i++)
        sut.AddLast(i);

      // GetFromBack(0) is the last element added (PastCapElementCount - 1); GetFromBack(index)
      // walks back across partitions. The chosen indices deliberately land in the current (back)
      // partition, the flat 4096 partitions, and the early small ramp partitions (large indices
      // near Count - 1 reach back into the 8/16/32-element partitions at the front).
      var indices = new[]
      {
        0,
        1,
        500,
        4095,
        4096,
        4097,
        8191,
        8192,
        9000,
        9990,
        PastCapElementCount - 1,
      };

      // Act / Assert
      foreach (var index in indices)
      {
        var expected = PastCapElementCount - 1 - index;
        Assert.AreEqual(expected, sut.GetFromBack(index), $"GetFromBack({index})");
      }
    }

    [TestMethod]
    public void RemoveLast_PastPartitionCap_ReturnsInReverseOrderAcrossPartitionBoundaries()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();
      for (var i = 0; i < PastCapElementCount; i++)
        sut.AddLast(i);

      // Drain enough to cross several partition boundaries from the back (one 4096 partition plus
      // some, so the back pointer walks across multiple partition Previous links of differing size).
      const int removeCount = 5000;

      // Act / Assert
      for (var i = 0; i < removeCount; i++)
      {
        var expected = PastCapElementCount - 1 - i;
        Assert.AreEqual(expected, sut.RemoveLast(), $"RemoveLast iteration {i}");
        Assert.AreEqual(PastCapElementCount - 1 - i, sut.Count);
      }

      // Remaining content is still 0 .. (PastCapElementCount - removeCount - 1) in order.
      Assert.IsTrue(
        Enumerable.SequenceEqual(sut.Snapshot(), Enumerable.Range(0, PastCapElementCount - removeCount)));
    }

    [TestMethod]
    public void FillDrainPartiallyRefill_RecyclesMixedPartitions_RemainsConsistent()
    {
      // This drives emptied FRONT partitions (small ramp ones AND full 4096 ones) to be recycled
      // to the BACK via RemoveFirst, producing a partition list that is no longer in size order.
      // We then assert the surviving sequence both ways (enumeration and GetFromBack), which only
      // holds if every code path uses live partition lengths.

      // Arrange
      var sut = new RefSemiDeque<int>();
      var model = new Queue<int>();
      var nextValue = 0;

      void Add(int count)
      {
        for (var i = 0; i < count; i++)
        {
          sut.AddLast(nextValue);
          model.Enqueue(nextValue);
          nextValue++;
        }
      }

      void Remove(int count)
      {
        for (var i = 0; i < count; i++)
        {
          var expected = model.Dequeue();
          Assert.AreEqual(expected, sut.RemoveFirst());
        }
      }

      // Act
      // Build well past the cap so the front holds small ramp partitions followed by 4096 ones.
      Add(10_000);
      // Drain past the small ramp partitions and across at least one full 4096 partition; each
      // emptied front partition gets recycled to the back, interleaving small partitions after
      // the cap-sized ones.
      Remove(6_000);
      // Refill: new additions flow into the recycled (out-of-size-order) back partitions, and new
      // partitions continue to be capped at 4096.
      Add(8_000);
      // Drain again across the heterogeneous middle.
      Remove(5_000);
      // Top up once more to leave the deque straddling several partitions.
      Add(2_000);

      // Assert
      Assert.AreEqual(model.Count, sut.Count);
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), model));

      // GetFromBack must agree with enumeration across the recycled, out-of-order partitions.
      var expectedSequence = model.ToArray();
      for (var index = 0; index < expectedSequence.Length; index++)
      {
        // Sample to keep the test fast: front edge, back edge, and a regular stride through the
        // middle (the stride is coprime-ish with partition sizes so samples land in varied spots).
        var nearEdge = index < 32 || index >= expectedSequence.Length - 32;
        if (!nearEdge && index % 257 != 0)
          continue;

        var expected = expectedSequence[expectedSequence.Length - 1 - index];
        Assert.AreEqual(expected, sut.GetFromBack(index), $"GetFromBack({index})");
      }
    }

    [TestMethod]
    public void FillDrainToEmptyThenRefill_PastPartitionCap_RemainsConsistent()
    {
      // Arrange
      var sut = new RefSemiDeque<int>();

      // Act: first fill past the cap.
      for (var i = 0; i < PastCapElementCount; i++)
        sut.AddLast(i);

      // Drain completely. Reaching Count == 0 resets the pointers (and leaves the recycled
      // partitions in place for reuse).
      for (var i = 0; i < PastCapElementCount; i++)
        sut.RemoveFirst();

      Assert.AreEqual(0, sut.Count);
      Assert.IsFalse(sut.Snapshot().Any());

      // Refill past the cap again, reusing the existing (now out-of-size-order) partitions.
      const int refillCount = 12_000;
      for (var i = 0; i < refillCount; i++)
        sut.AddLast(i);

      // Assert
      Assert.AreEqual(refillCount, sut.Count);
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), Enumerable.Range(0, refillCount)));
      Assert.AreEqual(refillCount - 1, sut.GetFromBack(0));
      Assert.AreEqual(0, sut.GetFromBack(refillCount - 1));
      Assert.AreEqual(0, sut.GetFirst());
      Assert.AreEqual(refillCount - 1, sut.GetLast());
    }
  }
}
