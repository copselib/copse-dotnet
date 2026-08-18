using Copse.Core;
using Copse.Linq;
using Copse.Treenumerables;
using BenchmarkDotNet.Attributes;

namespace Copse.Benchmarks
{
  // The cross-operator composition sentinel (design-docs/OPERATOR_COMPOSITION_DESIGN.md): mixed
  // Select/Where chains collapse to ONE SelectWhereTreenumerable, and these rows watch that
  // machinery -- the general Compose path, the composed law closure, and FuncResultSelector
  // chains under both drivers. (Projection-only composition -- the light fast path -- is
  // covered by the Select family's Composition rows, in place since the reorg.)
  //
  // Rows come in composed/stacked ratio PAIRS, the AsyncOverhead convention: the stacked
  // control forces real layers by interposing Tree.Defer (a delegating wrapper nothing can
  // compose across), so the composed:stacked ratio IS the collapse win. A machinery
  // regression shows twice -- absolute drift on the composed row, and the ratio closing
  // toward 1.
  //
  // NARROW-dimension composed chains have no rows of their own: the narrow drivers are
  // generated from these composite driver sources (CompositeToNarrow), so these rows cover
  // them BY SHARING -- if the narrow lattice ever grows its own machinery, this claim
  // expires (the coverage-by-sharing convention, CHANGELOG_BENCHMARKS 2026-08-16).
  [MemoryDiagnoser]
  [BenchmarkCategory("Streaming", "Compose")]
  public class Compose
  {
    [Benchmark]
    public void Dft_Triangle_SelectWhere_Composed() =>
      SelectWhereComposed().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_SelectWhere_Composed() =>
      SelectWhereComposed().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_SelectWhere_Stacked() =>
      SelectWhereStacked().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_SelectWhere_Stacked() =>
      SelectWhereStacked().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_FiveOperators_Composed() =>
      FiveOperatorsComposed().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_FiveOperators_Composed() =>
      FiveOperatorsComposed().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_FiveOperators_Stacked() =>
      FiveOperatorsStacked().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_FiveOperators_Stacked() =>
      FiveOperatorsStacked().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_ScanWhere_Composed() =>
      ScanWhereComposed().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_ScanWhere_Composed() =>
      ScanWhereComposed().Consume(TreeTraversalStrategy.BreadthFirst);

    [Benchmark]
    public void Dft_Triangle_ScanWhere_Stacked() =>
      ScanWhereStacked().Consume(TreeTraversalStrategy.DepthFirst);

    [Benchmark]
    public void Bft_Triangle_ScanWhere_Stacked() =>
      ScanWhereStacked().Consume(TreeTraversalStrategy.BreadthFirst);

    // The headline WhereSelect case: one projection, one ~50% filter, one wrapper.
    private static ITreenumerable<int> SelectWhereComposed() =>
      CanonicalTrees.MegaTriangleTree()
      .Select(n => n + 1)
      .Where(projected => (projected & 1) == 0);

    private static ITreenumerable<int> SelectWhereStacked() =>
      Tree.Defer(() => CanonicalTrees.MegaTriangleTree().Select(n => n + 1))
      .Where(projected => (projected & 1) == 0);

    // THE FOURTH-CELL WITNESSES (SCAN_TIER_DESIGN.md, seeded 2026-08-18 while the spelling
    // is TWO machines: the rootfix scan engine feeding a separate Where driver). The
    // spelling never changes -- when the ancestor composer lands (the fold-carrying driver;
    // a rejecting operator over the scan citizen composes into ONE machine), the composed
    // row's route flips and this series shows the step; the Defer-broken stacked control
    // never collapses. Predicate consumes the ACCUMULATE (depth), so the fold is genuinely
    // load-bearing; the pair survives to the output (the erasure rule's emission case).
    private static ITreenumerable<NodeAccumulation<int, int>> ScanWhereComposed() =>
      CanonicalTrees.MegaTriangleTree()
      .RootfixScan(0, (depth, _) => depth + 1)
      .Where(pair => (pair.Accumulate & 1) == 0);

    private static ITreenumerable<NodeAccumulation<int, int>> ScanWhereStacked() =>
      Tree.Defer(() => CanonicalTrees.MegaTriangleTree().RootfixScan(0, (depth, _) => depth + 1))
      .Where(pair => (pair.Accumulate & 1) == 0);

    // The closure property: five operators in any order stay one wrapper.
    private static ITreenumerable<int> FiveOperatorsComposed() =>
      CanonicalTrees.MegaTriangleTree()
      .Where(n => n != -1)
      .Select(n => n + 1)
      .Where(projected => (projected & 1) == 0)
      .Select(projected => projected * 2)
      .Where(doubled => doubled != -3);

    private static ITreenumerable<int> FiveOperatorsStacked()
    {
      var whereLayer = CanonicalTrees.MegaTriangleTree().Where(n => n != -1);
      var selectLayer = Tree.Defer(() => whereLayer).Select(n => n + 1);
      var secondWhereLayer = Tree.Defer(() => selectLayer).Where(projected => (projected & 1) == 0);
      var secondSelectLayer = Tree.Defer(() => secondWhereLayer).Select(projected => projected * 2);

      return Tree.Defer(() => secondSelectLayer).Where(doubled => doubled != -3);
    }
  }
}
