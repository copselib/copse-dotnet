namespace Copse.Linq
{
  // Hand-written anchor for the class-level doc: every operator partial is generated from its
  // async source, and the async class doc's prose is async-specific, so the sync doc lives here.
  /// <summary>
  /// LINQ-style tree operators over <see cref="Copse.Core.ITreenumerable{TNode}"/>. Deferred
  /// operators compose without materializing (each returns a lazy tree); operators that
  /// capture say so in their return type and docs. Each operator lives in its own
  /// <c>Treenumerable.&lt;Op&gt;.cs</c> file.
  /// </summary>
  public static partial class Treenumerable
  {
  }
}
