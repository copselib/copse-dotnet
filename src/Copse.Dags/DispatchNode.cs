using System;

namespace Copse.Dags
{
  /// <summary>
  /// The read-side pair <c>RootfixDispatch</c> produces: a source value together with what the
  /// dispatch pass delivered to it (the merge of one inflow per in-edge; the seed for roots).
  /// The traversal wavefront is visible in the types -- by the time a node is surveyed it is
  /// resolved into this pair, while its children are still unresolved write-handles
  /// (<see cref="DispatchTarget{TValue, TEdge, TDispatch}"/>).
  ///
  /// <para>The operator DECORATES rather than replaces, so the flavors are compositions:
  /// immutable values project the pair away with <c>Select</c>; mutable values apply it with
  /// <c>Do</c> and then unwrap.</para>
  /// </summary>
  public readonly struct DispatchNode<TValue, TDispatch>
  {
    public DispatchNode(TValue value, TDispatch dispatched)
    {
      Value = value;
      Dispatched = dispatched;
    }

    public readonly TValue Value;
    public readonly TDispatch Dispatched;

    public override string ToString() => $"{Value} <- {Dispatched}";
  }

  /// <summary>
  /// The write-side handle: one per out-edge, handed to the survey. Carries the child and THIS
  /// edge's payload (a shared child is a different target, with a different payload, under each
  /// parent), and accepts exactly one <see cref="Dispatch"/> -- the shape a setter-callback
  /// allocator plugs into directly: <c>(child, amount) =&gt; child.Dispatch(amount)</c>.
  /// A second Dispatch throws immediately; a target left unset throws when the survey returns --
  /// the operator-level analogue of a strict allocator's no-penny-lost validation.
  /// </summary>
  public sealed class DispatchTarget<TValue, TEdge, TDispatch>
  {
    internal DispatchTarget(DagNode<TValue, TEdge> node, TEdge edge)
    {
      Node = node;
      Edge = edge;
    }

    /// <summary>The child this edge points to.</summary>
    public DagNode<TValue, TEdge> Node { get; }

    /// <summary>This edge's payload (e.g. the ownership fraction of <see cref="Node"/> under the surveyed parent).</summary>
    public TEdge Edge { get; }

    private bool _HasDispatched;
    private TDispatch _Dispatched;

    public void Dispatch(TDispatch value)
    {
      if (_HasDispatched)
        throw new InvalidOperationException(
          $"'{Node}' was dispatched to twice; each target accepts exactly one Dispatch per survey.");

      _HasDispatched = true;
      _Dispatched = value;
    }

    internal TDispatch GetDispatchedOrThrow()
    {
      if (!_HasDispatched)
        throw new InvalidOperationException(
          $"The survey completed without dispatching to '{Node}'; every out-edge must receive exactly one Dispatch.");

      return _Dispatched;
    }
  }
}
