using Copse.Core;
using System;

namespace Copse.Linq.Tests
{
  // The shared vocabulary of the combinatorial oracle suites (CombinatorialWhereTests,
  // CombinatorialMergeTests): the exhaustive tree corpus and the per-node consumer-strategy
  // assignment element. Deliberately NOT a [TestClass] -- nothing here is ever touched by
  // MSTest discovery.
  internal static class CombinatorialTestData
  {
    public struct NodeAndTraversalStrategy
    {
      public NodeAndTraversalStrategy(string node, NodeTraversalStrategies nodeTraversalStrategy)
      {
        if (node == null)
          throw new ArgumentNullException(nameof(node));

        Node = node;
        NodeTraversalStrategy = nodeTraversalStrategy;
      }

      public string Node { get; set; }
      public NodeTraversalStrategies NodeTraversalStrategy { get; set; }
    }

    // Full exhaustive tree set (groups c..i, 3..9 nodes). Consumed by the in-process
    // combinatorial suites, which loop these directly -- never by [DynamicData] (MSTest
    // enumerates DynamicData during DISCOVERY, even for [Ignore]d methods, and case sets
    // built over this corpus overwhelm the host).
    public static readonly string[] AllTreeStrings =
    {
      // c
      "a(b(c))",
      "a(b,c)",
      "a,b(c)",
      "a,b,c",

      // d
      "a(b,c,d)",
      "a,b(c,d)",
      "a,b(d),c",

      // e
      "a(b(d(e)),c)",
      "a(b(c,d,e))",
      "a(d),b,c(e)",

      // f
      "a(c,d),b(e,f)",
      "a(d),b(e),c(f)",
      "a(b(d,e,f),c)",
      "a,b(d),c(e(f))",

      // g
      "a(b(e),c(f),d(g))",
      "a(b(d,e),c(f(g)))",

      // h
      "a(d(f,g,h)),b,c(e)",

      // i
      "a(b(d,e,f),c(g,h,i))",
      "a(d(g)),b(e(h)),c(f(i))",
    };
  }
}
