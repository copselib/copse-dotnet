using System;
using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// A dag with a flow orientation chosen -- the door <c>Sourcefix()</c> / <c>Sinkfix()</c>
  /// opens -- from which the flow family is reached with ONE type argument: <c>TNode</c>
  /// and <c>TEdge</c> are fixed by the receiver here, so <c>Scan&lt;TResult&gt;</c> and
  /// <c>Dispatch&lt;TDispatch&gt;</c> name only the result type C# cannot infer (it appears
  /// only inside the lambda's parameter types). <c>dag.Sourcefix().Scan&lt;decimal&gt;((node,
  /// inflows) => …)</c> beside <c>dag.SourcefixScan&lt;Entity, decimal, Edge&gt;(…)</c>: the same
  /// operator, the same semantics, the same laws; the prefix has become the door. Every
  /// member delegates to its flat twin.
  /// </summary>
  public readonly struct DagFlow<TNode, TEdge>
  {
    internal DagFlow(IDagnumerable<TNode, TEdge> source, DagFlowOrientation orientation)
    {
      Source = source ?? throw new ArgumentNullException(nameof(source));
      Orientation = orientation;
    }

    public IDagnumerable<TNode, TEdge> Source { get; }
    internal DagFlowOrientation Orientation { get; }

    /// <summary>The fold: each node's accumulate from its inflows in flow order (<c>SourcefixScan</c> / <c>SinkfixScan</c>).</summary>
    public DagBuffer<DagScanResult<TNode, TResult>, TEdge> Scan<TResult>(Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
      => Orientation == DagFlowOrientation.Sourcefix
        ? Source.SourcefixScan(accumulate)
        : Source.SinkfixScan(accumulate);

    /// <summary>The seeded downward survey (<c>SourcefixDispatch</c>); sinkfix has no seed -- see <see cref="Dispatch{TDispatch}(DagDispatchSurvey{TNode, TDispatch, TEdge})"/>.</summary>
    public DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> Dispatch<TDispatch>(TDispatch seed, DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      if (Orientation != DagFlowOrientation.Sourcefix)
        throw new InvalidOperationException("A seed arrives from the virtual source; the sinkfix survey has none (holdings live in the nodes). Use Dispatch(survey).");

      return Source.SourcefixDispatch(seed, survey);
    }

    /// <summary>The unseeded survey: sinkfix natively; sourcefix with a default seed.</summary>
    public DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> Dispatch<TDispatch>(DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
      => Orientation == DagFlowOrientation.Sourcefix
        ? Source.SourcefixDispatch(default(TDispatch), survey)
        : Source.SinkfixDispatch(survey);

    /// <summary>The edge-paired survey: what each survey dispatched, riding the edge beside its original payload (<c>SourcefixDispatchEdges</c> / <c>SinkfixDispatchEdges</c>).</summary>
    public DagBuffer<TNode, DagEdgeResult<TEdge, TDispatch>> DispatchEdges<TDispatch>(DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
      => Orientation == DagFlowOrientation.Sourcefix
        ? Source.SourcefixDispatchEdges(survey)
        : Source.SinkfixDispatchEdges(survey);

    /// <summary>
    /// The seeded downward survey, RETURN-SHAPED: one dispatched value per target, in target
    /// order, count-checked -- the fold speaking the relabel family's convention (arrivals,
    /// node, targets), with the one marked difference that the arrivals carry THIS pass's
    /// results. The slot form stays as the setter-friendly spelling (allocators that write
    /// through callbacks); this form infers, composes, and cannot double- or under-dispatch.
    /// </summary>
    public DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> Dispatch<TDispatch>(
      TDispatch seed,
      Func<IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>>, TNode, IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>>, IReadOnlyList<TDispatch>> survey)
      => Dispatch(seed, AsSlotSurvey(survey));

    /// <summary>The unseeded survey, return-shaped (sinkfix natively; sourcefix with a default seed).</summary>
    public DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> Dispatch<TDispatch>(
      Func<IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>>, TNode, IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>>, IReadOnlyList<TDispatch>> survey)
      => Dispatch(AsSlotSurvey(survey));

    /// <summary>The edge-paired survey, return-shaped: the dispatched values ride the edges beside their original payloads.</summary>
    public DagBuffer<TNode, DagEdgeResult<TEdge, TDispatch>> DispatchEdges<TDispatch>(
      Func<IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>>, TNode, IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>>, IReadOnlyList<TDispatch>> survey)
      => DispatchEdges(AsSlotSurvey(survey));

    private static DagDispatchSurvey<TNode, TDispatch, TEdge> AsSlotSurvey<TDispatch>(
      Func<IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>>, TNode, IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>>, IReadOnlyList<TDispatch>> survey)
      => (subject, arrivals, targets) =>
      {
        var values = survey(arrivals, subject, targets);

        if (values == null || values.Count != targets.Count)
          throw new InvalidOperationException(
            $"A return-shaped survey answered {values?.Count.ToString() ?? "null"} values for {targets.Count} targets; one per target, in target order.");

        for (var index = 0; index < targets.Count; index++)
          targets[index].Dispatch(values[index]);
      };
  }
}
