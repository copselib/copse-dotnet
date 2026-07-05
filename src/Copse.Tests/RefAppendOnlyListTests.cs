using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Copse.Tests
{
  [TestClass]
  public class RefAppendOnlyListTests
  {
    [TestMethod]
    public void AddSingleItem()
    {
      // Arrange
      var sut = new RefAppendOnlyList<int>();

      // Act
      sut.AddLast(1);

      // Assert
      Assert.AreEqual(1, sut.Count);
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 1 }));
    }

    [TestMethod]
    public void AddMoreItemsThanInitialPartitionCapacity()
    {
      // Arrange
      var sut = new RefAppendOnlyList<int>(1);

      // Act
      sut.AddLast(1);
      sut.AddLast(2);
      sut.AddLast(3);

      // Assert
      Assert.IsTrue(Enumerable.SequenceEqual(sut.Snapshot(), new[] { 1, 2, 3 }));
    }

    [TestMethod]
    public void IndexerReadsAcrossManyPartitions()
    {
      // Arrange
      // Bitness 1 forces maximum partition churn: sizes 2, 2, 4, 8, 16, ...
      var sut = new RefAppendOnlyList<int>(1);

      // Act
      for (var i = 0; i < 10_000; i++)
        sut.AddLast(i);

      // Assert
      for (var i = 0; i < 10_000; i++)
        Assert.AreEqual(i, sut[i]);
    }

    [TestMethod]
    public void RefIndexerMutatesInPlace()
    {
      // Arrange
      var sut = new RefAppendOnlyList<int>(1);

      for (var i = 0; i < 100; i++)
        sut.AddLast(0);

      // Act
      // The memo builders' backfill pattern: append a placeholder, mutate it later by ref.
      for (var i = 0; i < 100; i++)
      {
        ref var slot = ref sut[i];
        slot = i * 2;
      }

      // Assert
      for (var i = 0; i < 100; i++)
        Assert.AreEqual(i * 2, sut[i]);
    }

    [TestMethod]
    public void RefIndexerMutatesStructContentsInPlace()
    {
      // Arrange
      var sut = new RefAppendOnlyList<(int A, int B)>();
      sut.AddLast((1, 0));

      // Act
      sut[0].B = 2;

      // Assert
      Assert.AreEqual((1, 2), sut[0]);
    }

    [TestMethod]
    public void EmptyList()
    {
      // Arrange
      var sut = new RefAppendOnlyList<int>();

      // Assert
      Assert.AreEqual(0, sut.Count);
      Assert.AreEqual(0, sut.Snapshot().Length);
    }

    [TestMethod]
    public void IndexerThrowsOnNegativeIndex()
    {
      // Arrange
      var sut = new RefAppendOnlyList<int>();
      sut.AddLast(1);

      // Assert
      Assert.ThrowsException<IndexOutOfRangeException>(() => sut[-1]);
    }

    [TestMethod]
    public void IndexerThrowsAtCount()
    {
      // Arrange
      var sut = new RefAppendOnlyList<int>();
      sut.AddLast(1);

      // Assert
      Assert.ThrowsException<IndexOutOfRangeException>(() => sut[1]);
    }

    [TestMethod]
    public void ConstructorRejectsBitnessOutOfRange()
    {
      // Assert
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RefAppendOnlyList<int>(0));
      Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RefAppendOnlyList<int>(31));
    }
  }
}
