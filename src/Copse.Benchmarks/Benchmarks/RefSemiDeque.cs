using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  [MemoryDiagnoser]
  [BenchmarkCategory("DataStructures")]
  public class RefSemiDeque
  {
    [Benchmark]
    public void Add_8M()
    {
      var deque = new Copse.Collections.RefSemiDeque<int>();

      for (int i = 0; i < 8_000_000; i++)
        deque.AddLast(i);
    }

    [Benchmark]
    public void RemoveFirst_8M()
    {
      var deque = new Copse.Collections.RefSemiDeque<int>();

      for (int i = 0; i < 8_000_000; i++)
        deque.AddLast(i);

      while (deque.Count > 0)
        deque.RemoveFirst();
    }

    [Benchmark]
    public void RemoveLast_8M()
    {
      var deque = new Copse.Collections.RefSemiDeque<int>();

      for (int i = 0; i < 8_000_000; i++)
        deque.AddLast(i);

      while (deque.Count > 0)
        deque.RemoveLast();
    }

    // Large node value (64 B): exercises the deque with LOH-sized partitions
    // (64 B * MaxPartitionSize). Large, long-lived blocks belong on the LOH, so
    // this is the case that argues against forcing partitions sub-LOH.
    [Benchmark]
    public void Add_Block64_1M()
    {
      var deque = new Copse.Collections.RefSemiDeque<Block64>();

      for (int i = 0; i < 1_000_000; i++)
        deque.AddLast(default);
    }

    public struct Block64
    {
      public long A, B, C, D, E, F, G, H;
    }
  }
}
