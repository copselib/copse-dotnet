using System;

namespace Copse.Dags
{
  public static partial class Dagnumerable
  {
    /// <summary>
    /// Replaces each node with a <see cref="DagNodeGraph{TNode, TEdge}"/>: the original's in-edges
    /// reach the fragment's sources, its out-edges leave from every fragment node, and
    /// <see cref="DagNodeGraph{TNode, TEdge}.Drop"/> deletes the node (its children gain no
    /// replacement edges; a node that loses its last inbound path dies with it, and a dead
    /// node's selector is never consulted). Only <see cref="DagNodeGraph{TNode, TEdge}.Keep"/>
    /// occupies the original's seat (<c>SourceOrdinal</c> carries); every other shape is born here.
    /// </summary>
    /// <remarks>
    /// The bind (<see cref="SelectMany{TNode, TResult, TEdge}"/>) whose attachments are the whole
    /// fragment. Attachments from the fragment's own nodes carry no payload and nothing
    /// promotes, so the composer is never invoked.
    /// </remarks>
    public static DagBuffer<TNode, TEdge> ReplaceNodes<TNode, TEdge>(
      this IDagnumerable<TNode, TEdge> source,
      Func<TNode, DagNodeGraph<TNode, TEdge>> selector)
    {
      if (selector == null)
        throw new ArgumentNullException(nameof(selector));

      return source.SelectMany(node => selector(node).AsExpansion(), (upstream, downstream) => upstream);
    }
  }
}
