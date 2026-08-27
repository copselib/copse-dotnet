using Copse.Core;
using Copse.Linq.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Copse.Linq
{
  public static partial class AsyncTreenumerable
  {
    /// <summary>The tree's root-to-leaf paths (each as a node array), as a lazy async sequence.</summary>
    public static async IAsyncEnumerable<TNode[]> GetBranches<TNode>(this IAsyncDepthFirstTreenumerable<TNode> source, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
      var branch = new List<NodeAndPosition<TNode>>();

      var treenumerator = source.GetAsyncDepthFirstTreenumerator();
      await using (treenumerator.ConfigureAwait(false))
      {
        if (!await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
          yield break;

        branch.Add(treenumerator.ToNodeAndPosition());

        while (await treenumerator.MoveNextAsync(NodeTraversalStrategies.TraverseAll).ConfigureAwait(false))
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (treenumerator.Mode != TreenumeratorMode.SchedulingNode)
            continue;

          var depth = treenumerator.Position.Depth;

          if (depth > branch.Count - 1)
          {
            branch.Add(treenumerator.ToNodeAndPosition());
          }
          else
          {
            yield return branch.Select(nodeAndPosition => nodeAndPosition.Node).ToArray();

            branch.RemoveRange(depth, branch.Count - depth);
            branch.Add(treenumerator.ToNodeAndPosition());
          }
        }

        if (branch.Count > 0)
          yield return branch.Select(nodeAndPosition => nodeAndPosition.Node).ToArray();
      }
    }
  }
}
