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
  /// inflows) => …)</c> is the family's one spelling of the sourcefix scan: the doors are the
  /// surface, the three-argument engines behind them are internal (ruled: never both -- one
  /// name, one meaning).
  /// </summary>
  public readonly struct DagFlow<TNode, TEdge>
  {
    internal DagFlow(IDagnumerable<TNode, TEdge> source, DagFlowOrientation orientation)
    {
      _Source = source ?? throw new ArgumentNullException(nameof(source));
      _Orientation = orientation;
    }

    private readonly IDagnumerable<TNode, TEdge> _Source;
    private readonly DagFlowOrientation _Orientation;

    private DagBuffer<TNode, TEdge> Buffer => DagBuffer<TNode, TEdge>.From(_Source);

    /// <summary>The fold: each node's accumulate from its inflows in flow order -- the sourcefix scan downward, the sinkfix scan upward.</summary>
    public DagBuffer<DagScanResult<TNode, TResult>, TEdge> Scan<TResult>(Func<TNode, IReadOnlyList<DagInflow<TResult, TEdge>>, TResult> accumulate)
      => DagFlowEngines.ScanBuffer(Buffer, _Orientation, accumulate);

    /// <summary>The seeded downward survey; sinkfix has no seed -- see <see cref="Dispatch{TDispatch}(DagDispatchSurvey{TNode, TDispatch, TEdge})"/>.</summary>
    public DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> Dispatch<TDispatch>(TDispatch seed, DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
    {
      if (_Orientation != DagFlowOrientation.Sourcefix)
        throw new InvalidOperationException("A seed arrives from the virtual source; the sinkfix survey has none (holdings live in the nodes). Use Dispatch(survey).");

      return DagFlowEngines.DispatchBuffer(Buffer, seed, _Orientation, survey);
    }

    /// <summary>The unseeded survey: sinkfix natively; sourcefix with a default seed.</summary>
    public DagBuffer<DagDispatchResult<TNode, TDispatch>, TEdge> Dispatch<TDispatch>(DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
      => DagFlowEngines.DispatchBuffer(Buffer, default(TDispatch), _Orientation, survey);

    /// <summary>The edge-paired survey: what each survey dispatched, riding the edge beside its original payload.</summary>
    public DagBuffer<TNode, DagEdgeResult<TEdge, TDispatch>> DispatchEdges<TDispatch>(DagDispatchSurvey<TNode, TDispatch, TEdge> survey)
      => DagFlowEngines.DispatchEdgesBuffer(Buffer, _Orientation, survey);

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
