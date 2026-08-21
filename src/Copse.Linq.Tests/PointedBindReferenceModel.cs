using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.Linq.Tests
{
  // The pointed bind's reference model -- the oracle the law suites verify against and the
  // operator suite conforms to. Forests of labeled nodes; the SLOT is a phantom leaf, at most
  // one per selector forest; attachment splices the rewritten children where it sits; a
  // slotless forest drops them. Deliberately naive and materialized: it is the semantics,
  // not an implementation.
  internal static class PointedBindReferenceModel
  {
    public const string Slot = "▷";

    public sealed class TreeModel
    {
      public TreeModel(string value) { Value = value; Children = new List<TreeModel>(); }
      public string Value { get; }
      public List<TreeModel> Children { get; }
    }

    public static List<TreeModel> ParseForest(string text)
    {
      var position = 0;
      var forest = new List<TreeModel>();

      while (position < text.Length)
      {
        forest.Add(ParseTree(text, ref position));
        if (position < text.Length && text[position] == ',')
          position++;
      }

      return forest;
    }

    private static TreeModel ParseTree(string text, ref int position)
    {
      var start = position;
      while (position < text.Length && text[position] != '(' && text[position] != ')' && text[position] != ',')
        position++;

      var node = new TreeModel(text.Substring(start, position - start));

      if (position < text.Length && text[position] == '(')
      {
        position++;
        while (text[position] != ')')
        {
          node.Children.Add(ParseTree(text, ref position));
          if (text[position] == ',')
            position++;
        }
        position++;
      }

      return node;
    }

    public static string Print(List<TreeModel> forest)
      => string.Join(",", forest.Select(PrintTree));

    private static string PrintTree(TreeModel tree)
      => tree.Children.Count == 0 ? tree.Value : $"{tree.Value}({Print(tree.Children)})";

    public static List<TreeModel> BindForest(List<TreeModel> forest, Func<string, List<TreeModel>> selector)
      => forest.SelectMany(tree => BindTree(tree, selector)).ToList();

    private static List<TreeModel> BindTree(TreeModel tree, Func<string, List<TreeModel>> selector)
    {
      if (tree.Value == Slot)
        return new List<TreeModel> { tree };

      var rewrittenChildren = tree.Children.SelectMany(child => BindTree(child, selector)).ToList();
      var expansion = selector(tree.Value);

      SpliceAtSlot(expansion, rewrittenChildren);

      return expansion;
    }

    public static void SpliceAtSlot(List<TreeModel> forest, List<TreeModel> spliced)
    {
      for (var index = 0; index < forest.Count; index++)
      {
        if (forest[index].Value == Slot)
        {
          forest.RemoveAt(index);
          forest.InsertRange(index, spliced);
          return;
        }

        SpliceAtSlot(forest[index].Children, spliced);
      }
    }

    public static Func<string, List<TreeModel>> Compose(Func<string, List<TreeModel>> first, Func<string, List<TreeModel>> second)
      => value => BindForest(first(value), second);

    public static List<TreeModel> ReturnPointed(string value) => ParseForest($"{value}({Slot})");

    public static List<TreeModel> EmptyPointed() => ParseForest(Slot);

    public static List<TreeModel> SlotlessEmpty() => new List<TreeModel>();

    public static List<TreeModel> SlotlessLeaf(string value) => ParseForest(value);
  }
}
