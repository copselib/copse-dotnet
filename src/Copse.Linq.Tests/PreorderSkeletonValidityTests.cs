using Copse.TestUtils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Copse.Linq.Tests
{
  // The validity predicate's own pins: the legal encodings pass, and each way an encoding
  // can lie is caught with a coordinate-bearing message.
  [TestClass]
  public class PreorderSkeletonValidityTests
  {
    [TestMethod]
    public void LegalEncodings_Pass()
    {
      PreorderSkeletonValidity.AssertValid(Array.Empty<int>());              // empty forest
      PreorderSkeletonValidity.AssertValid(new[] { 1 });                     // a
      PreorderSkeletonValidity.AssertValid(new[] { 1, 1, 1 });               // a,b,c
      PreorderSkeletonValidity.AssertValid(new[] { 3, 1, 1 });               // a(b,c)
      PreorderSkeletonValidity.AssertValid(new[] { 3, 2, 1 });               // a(b(c))
      PreorderSkeletonValidity.AssertValid(new[] { 7, 3, 1, 1, 3, 1, 1 });   // a(b(d,e),c(f,g))
      PreorderSkeletonValidity.AssertValid(new[] { 2, 1, 2, 1 });            // a(b),c(d)
    }

    [TestMethod]
    public void ZeroSize_IsCaught()
      => Assert.ThrowsException<InvalidOperationException>(
        () => PreorderSkeletonValidity.AssertValid(new[] { 2, 0 }));

    [TestMethod]
    public void SpanCrossingParent_IsCaught()
      // a claims 2 nodes; b claims 2 -- b's span [1,3) crosses a's end 2.
      => Assert.ThrowsException<InvalidOperationException>(
        () => PreorderSkeletonValidity.AssertValid(new[] { 2, 2, 1 }));

    [TestMethod]
    public void SpanOverrunningTheForest_IsCaught()
      => Assert.ThrowsException<InvalidOperationException>(
        () => PreorderSkeletonValidity.AssertValid(new[] { 3, 1 }));
  }
}
