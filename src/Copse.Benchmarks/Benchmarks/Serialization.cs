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
  // Trees are settled buffers built in [GlobalSetup] (Deserialize + Materialize), so the timed
  // Serialize methods measure pure serializer work, not tree generation or parsing.
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
      // Deserialize has Defer semantics (every treenumerator acquisition re-parses), so the
      // Serialize rows serialize a settled buffer to keep measuring pure serializer work.
      // Materialize is deferred (2026-08-10): the Consume settles each capture here, in setup.
      _forestTree = TreeSerializer.DeserializeDepthFirstTree(_forestString).Materialize();
      _chainTree = TreeSerializer.DeserializeDepthFirstTree(_chainString).Materialize();
      _forestTree.Consume();
      _chainTree.Consume();
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
