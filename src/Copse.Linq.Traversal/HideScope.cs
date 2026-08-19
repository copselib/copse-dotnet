namespace Copse.Linq
{
  // How far Hide's barrier reaches. An ORDERED SCOPE, deliberately NOT [Flags]: each value names
  // the DEEPEST layer hidden, and the two layers are not independent -- hiding the treenumerator
  // necessarily hides the treenumerable above it, because you cannot intercept acquisition
  // without owning the factory that performs it. A flag set would spell a combination that
  // cannot exist.
  //
  // The distinction is worth a parameter because the two scopes have different COSTS, and the
  // cheaper one is what most callers want. See the per-value remarks.
  //
  // ONE neutral enum, not a codegen pair: pure vocabulary values have no color, and both colors'
  // Hide overloads surface the same type. Lives in Copse.Linq.Traversal, the Linq-level neutral
  // project, like BufferLayout.
  public enum HideScope
  {
    /// <summary>
    /// Hide the <b>treenumerable</b> only: the result claims no capability beyond the plain
    /// contract, so nothing downstream can feature-test it, compose into it, or reroute on it.
    /// The treenumerator is forwarded as-is, so this costs ONE virtual call per acquisition and
    /// <b>nothing per pull</b>.
    /// <para>
    /// This is the scope that serves what <c>Hide</c> is normally reached for -- forcing a
    /// composition barrier -- but it is an OPT-OUT, not the default: choosing it accepts that the
    /// concrete machine type stays visible, and that a future treenumerator-level probe would see
    /// through it. Choose it where the per-pull cost is what is being measured or where the
    /// caller controls both ends (the benchmark corpus takes every tree through it).
    /// </para>
    /// </summary>
    Treenumerable,

    /// <summary>
    /// Hide the <b>treenumerator</b> as well (and therefore the treenumerable above it): the
    /// visit stream is forwarded through a wrapper, so the concrete machine type is concealed
    /// from a caller who reaches past the treenumerable to inspect it.
    /// <para>
    /// That wrapper is a real layer on every <c>MoveNext</c>, and <b>this is the default</b> -- the
    /// no-argument <c>Hide()</c> overload selects it. Deliberately, as policy: <c>Hide</c> is a
    /// DEFENSIVE operator, so it conceals everything by default and a caller opts OUT of the part
    /// they do not need.
    /// </para>
    /// <para>
    /// The reason is forward compatibility, not present need. Nothing in Copse sniffs a
    /// treenumerator today -- every probe in the library is at the treenumerable layer -- so this
    /// scope currently defends only against foreign code. But if a later version adds a
    /// treenumerator-level probe (a bulk-pull fast path, a chunked drain), a shallow default would
    /// silently demote every existing <c>Hide()</c> call site from a complete barrier at upgrade
    /// time, with no diagnostic. Under this default they stay correct, and adding such a probe
    /// stays a non-breaking change. Cost has an explicit opt-out; correctness cannot be opted into
    /// retroactively.
    /// </para>
    /// </summary>
    Treenumerator,
  }
}
