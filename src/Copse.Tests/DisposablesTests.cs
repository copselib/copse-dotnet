using Copse.Disposables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Copse.Tests
{
  // Locks the lifted System.Reactive semantics on this repo's target frameworks -- these are
  // the behaviors the rest of the library (and consumers) lean on, not a re-test of Rx.
  [TestClass]
  public class DisposablesTests
  {
    private sealed class TestResource : IDisposable
    {
      public int DisposeCount { get; private set; }
      public bool Disposed => DisposeCount > 0;
      public void Dispose() => DisposeCount++;
    }

    // ---------------------------------------------------------------------------------------
    // Disposable.Create
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Create_RunsActionAtMostOnce()
    {
      var runs = 0;
      var disposable = Disposable.Create(() => runs++);

      disposable.Dispose();
      disposable.Dispose();

      Assert.AreEqual(1, runs);
    }

    [TestMethod]
    public void Empty_IsSingletonNoOp()
    {
      Assert.AreSame(Disposable.Empty, Disposable.Empty);
      Disposable.Empty.Dispose(); // no throw
    }

    // ---------------------------------------------------------------------------------------
    // RefCountDisposable
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void RefCount_PrimaryDisposeWithNoHandles_ReleasesImmediately()
    {
      var resource = new TestResource();
      var refCount = new RefCountDisposable(resource);

      refCount.Dispose();

      Assert.AreEqual(1, resource.DisposeCount);
    }

    [TestMethod]
    public void RefCount_UnderlyingHeldUntilLastHandleReleases()
    {
      var resource = new TestResource();
      var refCount = new RefCountDisposable(resource);

      var handle1 = refCount.GetDisposable();
      var handle2 = refCount.GetDisposable();

      refCount.Dispose();          // primary: marked, but two handles outstanding
      Assert.IsFalse(resource.Disposed);

      handle1.Dispose();
      Assert.IsFalse(resource.Disposed);

      handle2.Dispose();
      Assert.AreEqual(1, resource.DisposeCount);
    }

    [TestMethod]
    public void RefCount_HandlesAloneNeverRelease()
    {
      var resource = new TestResource();
      var refCount = new RefCountDisposable(resource);

      refCount.GetDisposable().Dispose();
      refCount.GetDisposable().Dispose();

      Assert.IsFalse(resource.Disposed);
    }

    [TestMethod]
    public void RefCount_GetDisposableAfterDisposed_ReturnsNoOpHandle()
    {
      var resource = new TestResource();
      var refCount = new RefCountDisposable(resource);

      refCount.Dispose();

      var handle = refCount.GetDisposable();
      handle.Dispose(); // no throw, no double release

      Assert.AreEqual(1, resource.DisposeCount);
      Assert.IsTrue(refCount.IsDisposed);
    }

    [TestMethod]
    public void RefCount_DoubleHandleDispose_DecrementsOnce()
    {
      var resource = new TestResource();
      var refCount = new RefCountDisposable(resource);

      var handle1 = refCount.GetDisposable();
      var handle2 = refCount.GetDisposable();

      refCount.Dispose();

      handle1.Dispose();
      handle1.Dispose(); // second dispose of the same handle must not count

      Assert.IsFalse(resource.Disposed);

      handle2.Dispose();
      Assert.AreEqual(1, resource.DisposeCount);
    }

    // ---------------------------------------------------------------------------------------
    // CompositeDisposable
    // ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Composite_DisposesChildrenInAddOrder()
    {
      var order = new List<int>();
      var composite = new CompositeDisposable
      {
        Disposable.Create(() => order.Add(1)),
        Disposable.Create(() => order.Add(2)),
        Disposable.Create(() => order.Add(3)),
      };

      composite.Dispose();

      CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
    }

    [TestMethod]
    public void Composite_AddAfterDispose_DisposesItemImmediately()
    {
      var composite = new CompositeDisposable();
      composite.Dispose();

      var resource = new TestResource();
      composite.Add(resource);

      Assert.AreEqual(1, resource.DisposeCount);
      Assert.AreEqual(0, composite.Count);
    }

    [TestMethod]
    public void Composite_RemoveDisposesTheItem()
    {
      var resource = new TestResource();
      var composite = new CompositeDisposable { resource };

      Assert.IsTrue(composite.Remove(resource));
      Assert.AreEqual(1, resource.DisposeCount);
      Assert.AreEqual(0, composite.Count);

      Assert.IsFalse(composite.Remove(resource)); // already gone
      Assert.AreEqual(1, resource.DisposeCount);
    }

    [TestMethod]
    public void Composite_ClearDisposesChildrenButStaysUsable()
    {
      var first = new TestResource();
      var composite = new CompositeDisposable { first };

      composite.Clear();

      Assert.AreEqual(1, first.DisposeCount);
      Assert.IsFalse(composite.IsDisposed);

      var second = new TestResource();
      composite.Add(second);

      Assert.AreEqual(1, composite.Count);
      Assert.IsFalse(second.Disposed);

      composite.Dispose();
      Assert.AreEqual(1, second.DisposeCount);
    }

    [TestMethod]
    public void Composite_DisposeIsIdempotent()
    {
      var resource = new TestResource();
      var composite = new CompositeDisposable { resource };

      composite.Dispose();
      composite.Dispose();

      Assert.AreEqual(1, resource.DisposeCount);
      Assert.IsTrue(composite.IsDisposed);
    }
  }
}
