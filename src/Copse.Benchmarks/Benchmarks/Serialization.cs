using Copse.Core;
using Copse.Linq;
using Copse.SimpleSerializer;
using BenchmarkDotNet.Attributes;
using System;
using System.Linq;

namespace Copse.Benchmarks
{
  // Baseline for TreeSerializer.Serialize / Deserialize. Two shapes:
  //   * Wide  -- a flat forest of 1M roots (stresses the symbol/comma path: 1M values + 1M commas).
  //   * Deep  -- a degenerate 100K-deep path (stresses nesting: 100K '(' + 100K ')').
  // Trees are materialized SimpleNode structures in [GlobalSetup] (via Deserialize), so the timed
  // methods measure pure serializer work, not tree generation.
  [MemoryDiagnoser]
  [BenchmarkCategory("Serialization")]
  public class Serialization
  {
    private const int Width = 1_000_000;
    private const int Depth = 100_000;

    private string _wideString;
    private string _deepString;
    private ITreenumerable<string> _wideTree;
    private ITreenumerable<string> _deepTree;

    [GlobalSetup]
    public void Setup()
    {
      _wideString = Enumerable.Range(0, Width).ToTrivialForest().SerializeDepthFirstTree(value => value.ToString());
      _deepString = Enumerable.Range(0, Depth).ToDegenerateTree().SerializeDepthFirstTree(value => value.ToString());
      _wideTree = TreeSerializer.DeserializeDepthFirstTree(_wideString);
      _deepTree = TreeSerializer.DeserializeDepthFirstTree(_deepString);
      // Deserialization is lazy; force the shared stores to parse fully so the Serialize rows
      // measure pure serializer work.
      _wideTree.Consume(TreeTraversalStrategy.DepthFirst);
      _deepTree.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public string Serialize_Wide_1M() => _wideTree.SerializeDepthFirstTree();

    [Benchmark]
    public string Serialize_Deep_100K() => _deepTree.SerializeDepthFirstTree();

    // Deserialization is lazy (composition parses nothing), so these drain: full parse + one
    // depth-first pass over the result.
    [Benchmark]
    public void Deserialize_Wide_1M() => TreeSerializer.DeserializeDepthFirstTree(_wideString).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Deserialize_Deep_100K() => TreeSerializer.DeserializeDepthFirstTree(_deepString).Consume(TreeTraversalStrategy.DepthFirst);

    // Span demonstration: parse the same source into ints. The string map materializes 1M throwaway
    // value strings; the span map parses straight off the source with int.Parse(ReadOnlySpan<char>).
    [Benchmark]
    public void Deserialize_Wide_ToInt_StringMap()
      => TreeSerializer.DeserializeDepthFirstTree(_wideString, (string s) => int.Parse(s)).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Deserialize_Wide_ToInt_SpanMap()
      => TreeSerializer.DeserializeDepthFirstTree(_wideString, (ReadOnlySpan<char> s) => int.Parse(s)).Consume(TreeTraversalStrategy.DepthFirst);
  }
}
