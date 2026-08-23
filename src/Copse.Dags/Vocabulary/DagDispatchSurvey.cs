using System.Collections.Generic;

namespace Copse.Dags
{
  /// <summary>
  /// The dispatch survey's seats, destructured (the seat rule -- no bundled
  /// node-plus-arrivals parameter): the SUBJECT (kept in both directions, forced by n-ary
  /// in-flow -- with multiple parents no single authoring site holds a node's whole arrival,
  /// so node-grained facts are underivable at any dispatch site; every DAG node is input-side
  /// sinkfix-like), the ARRIVALS (edge-paired, provenance-carrying -- the callback view is the
  /// Dispatcher's home), and the TARGETS (exactly-once write-handles). For the VIRTUAL SOURCE
  /// FAMILY's invocation (the seeded <c>Sourcefix().Dispatch</c> only) the subject is
  /// <c>default</c> -- the virtual source has no value, semantically true for exactly that one
  /// invocation -- and the single arrival is the seed, dispatcher-less (the seed has no author
  /// inside the dag; a dispatcher-less arrival is the in-band arrived-from-outside test). The
  /// slot-shaped survey of <see cref="DagFlow{TNode, TEdge}"/>'s dispatches; the return-shaped
  /// overloads adapt into it.
  /// </summary>
  public delegate void DagDispatchSurvey<TNode, TDispatch, TEdge>(
    TNode subject,
    IReadOnlyList<DagDispatchInflow<TNode, TDispatch, TEdge>> arrivals,
    IReadOnlyList<DagDispatchTarget<TNode, TDispatch, TEdge>> targets);
}
