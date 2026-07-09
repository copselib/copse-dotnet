using Copse.Core;
using Copse.Linq;
using Copse.SimpleSerializer;
using BenchmarkDotNet.Attributes;
using System;
using System.Linq;

namespace Copse.Benchmarks
{
  // Baseline for TreeSerializer.Serialize / Deserialize. Two shapes:
  //   * Forest -- a flat forest at the Mega tier (stresses the symbol/comma path: 2^20 values +
  //     2^20 commas).
  //   * Chain  -- a degenerate 100K-deep path (stresses nesting: 100K '(' + 100K ')').
  //     DOCUMENTED TIER EXCEPTION: a Mega-tier chain serialization is a ~10 MB string with
  //     matching per-op allocation, blowing the memory budget for no extra signal; 100K keeps
  //     the row well above the noise floor.
  // Trees are materialized SimpleNode structures in [GlobalSetup] (via Deserialize), so the timed
  // methods measure pure serializer work, not tree generation.
  [MemoryDiagnoser]
  [BenchmarkCategory("Serialization")]
  public class Serialization
  {
    private const int ChainDepth = 100_000;

    private string _forestString;
    private string _chainString;
    private ITreenumerable<string> _forestTree;
    private ITreenumerable<string> _chainTree;

    [GlobalSetup]
    public void Setup()
    {
      _forestString = Enumerable.Range(0, CanonicalTrees.MegaChain).ToTrivialForest().SerializeDepthFirstTree(value => value.ToString());
      _chainString = Enumerable.Range(0, ChainDepth).ToDegenerateTree().SerializeDepthFirstTree(value => value.ToString());
      _forestTree = TreeSerializer.DeserializeDepthFirstTree(_forestString);
      _chainTree = TreeSerializer.DeserializeDepthFirstTree(_chainString);
      // Deserialization is lazy; force the shared stores to parse fully so the Serialize rows
      // measure pure serializer work.
      _forestTree.Consume(TreeTraversalStrategy.DepthFirst);
      _chainTree.Consume(TreeTraversalStrategy.DepthFirst);
    }

    [Benchmark]
    public string Serialize_Forest() => _forestTree.SerializeDepthFirstTree();

    [Benchmark]
    public string Serialize_Chain_100K() => _chainTree.SerializeDepthFirstTree();

    // Deserialization is lazy (composition parses nothing), so these drain: full parse + one
    // depth-first pass over the result.
    [Benchmark]
    public void Deserialize_Forest() => TreeSerializer.DeserializeDepthFirstTree(_forestString).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Deserialize_Chain_100K() => TreeSerializer.DeserializeDepthFirstTree(_chainString).Consume(TreeTraversalStrategy.DepthFirst);

    // Span demonstration: parse the same source into ints. The string map materializes 2^20
    // throwaway value strings; the span map parses straight off the source with
    // int.Parse(ReadOnlySpan<char>).
    [Benchmark]
    public void Deserialize_Forest_ToInt_StringMap()
      => TreeSerializer.DeserializeDepthFirstTree(_forestString, (string s) => int.Parse(s)).Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Deserialize_Forest_ToInt_SpanMap()
      => TreeSerializer.DeserializeDepthFirstTree(_forestString, (ReadOnlySpan<char> s) => int.Parse(s)).Consume(TreeTraversalStrategy.DepthFirst);
  }
}
