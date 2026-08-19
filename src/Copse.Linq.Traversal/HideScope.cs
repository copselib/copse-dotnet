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
    /// composition barrier -- and the right choice unless a caller specifically needs the
    /// concrete treenumerator type concealed too.
    /// </para>
    /// </summary>
    Treenumerable,

    /// <summary>
    /// Hide the <b>treenumerator</b> as well (and therefore the treenumerable above it): the
    /// visit stream is forwarded through a wrapper, so the concrete machine type is concealed
    /// from a caller who reaches past the treenumerable to inspect it.
    /// <para>
    /// That wrapper is a real layer on every <c>MoveNext</c>. Nothing in Copse itself sniffs a
    /// treenumerator type -- every probe in the library is at the treenumerable layer -- so this
    /// scope is for defending against foreign code that does. Historical default: the no-argument
    /// <c>Hide()</c> overload selects it.
    /// </para>
    /// </summary>
    Treenumerator,
  }
}
