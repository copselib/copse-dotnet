using Copse.Treenumerables;
using System.Collections.Generic;

namespace Copse.TestUtils
{
  // The conformance ORACLE: an engine-backed tree parsed from the terse grammar with its own
  // tiny parser -- deliberately independent of TreeSerializer, whose deserialization now rides
  // the flat family's playback machinery (the very thing the conformance suites referee; an
  // oracle must not route through the implementation under test). PreorderTree lives here, out
  // of the product, precisely to keep the DFS/BFS engine available as that reference
  // implementation.
  public static class EngineTree
  {
    // The terse dft grammar ("a(b(d,e),c)"): a value followed by '(' is a parent; ',' separates
    // siblings; ')' closes a subtree.
    public static PreorderTree<string> Parse(string tree)
    {
      var values = new List<string>();
      var subtreeSizes = new List<int>();
      var open = new Stack<int>();
      var valueStart = -1;

      void Commit(int end, bool asParent)
      {
        if (valueStart < 0)
          return;

        var index = values.Count;
        values.Add(tree.Substring(valueStart, end - valueStart));
        subtreeSizes.Add(asParent ? 0 : 1);
        valueStart = -1;

        if (asParent)
          open.Push(index);
      }

      for (int i = 0; i < tree.Length; i++)
      {
        switch (tree[i])
        {
          case '(':
            Commit(i, asParent: true);
            break;

          case ')':
            Commit(i, asParent: false);
            var closed = open.Pop();
            subtreeSizes[closed] = values.Count - closed;
            break;

          case ',':
            Commit(i, asParent: false);
            break;

          default:
            if (valueStart < 0)
              valueStart = i;
            break;
        }
      }

      Commit(tree.Length, asParent: false);

      return new PreorderTree<string>(values.ToArray(), subtreeSizes.ToArray());
    }
  }
}
