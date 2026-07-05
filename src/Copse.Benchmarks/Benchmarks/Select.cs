using Copse.Linq;
using BenchmarkDotNet.Attributes;
using System.Linq;

namespace Copse.Benchmarks
{
  [MemoryDiagnoser]
  [BenchmarkCategory("LINQ", "Projection")]
  public class Select
  {
    [Benchmark]
    public int SelectComposition() =>
      Enumerable
      .Range(0, 1_000_000)
      .ToTrivialForest()
      .Select(x => x.Node * 2)
      .Select(x => x.Node + 'a')
      .Select(x => x.Node + 1)
      .Select(x => (char)x.Node)
      .PreorderTraversal()
      .Count();
  }
}
