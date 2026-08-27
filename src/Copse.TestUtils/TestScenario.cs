using Copse.Core;
using Copse.Linq;
using System;

namespace Copse.TestUtils
{
  public class TestScenario
  {
    public Func<NodeAndPosition<string>, NodeTraversalStrategies> NodeTraversalStrategiesSelector { get; set; }
    public Func<ITreenumerable<string>, ITreenumerable<string>> TreenumerableMap { get; set; }
    public string Description { get; set; }
    public NodeVisit<string>[] ExpectedDepthFirstResults { get; set; }
    public NodeVisit<string>[] ExpectedBreadthFirstResults { get; set; }
  }
}
