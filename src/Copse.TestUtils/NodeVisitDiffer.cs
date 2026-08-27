using Copse.Core;
using Copse.Linq;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Copse.TestUtils
{
  public static class NodeVisitDiffer
  {
    public static IEnumerable<string> Diff<TNode>(NodeVisit<TNode>[] expectedResults, NodeVisit<TNode>[] actualResults)
    {
      if (expectedResults.Length != actualResults.Length)
        throw new Exception("The expected results and actual results should have the same number of items");

      var diffBuilder = new SideBySideDiffBuilder(new Differ());

      var expectedText = string.Join($"{Environment.NewLine}", expectedResults.Select(x => x.ToString()));
      var actualText = string.Join($"{Environment.NewLine}", actualResults.Select(x => x.ToString()));

      var diff = diffBuilder.BuildDiffModel(expectedText, actualText);

      var results = new List<string[]>
      {
        new string[] { " ", "S", "N", "VC", "P" }
      };

      if (diff.OldText.Lines.Count != diff.NewText.Lines.Count)
        throw new Exception("Unexpected line count differences, I had not planned for this scenario.");

      var expectedPointer = 0;
      var actualPointer = 0;

      for (int i = 0; i < diff.OldText.Lines.Count; i++)
      {
        var expectedDiff = diff.OldText.Lines[i];
        var actualDiff = diff.NewText.Lines[i];

        var expected =
          expectedPointer < expectedResults.Length
          ? expectedResults[expectedPointer]
          : default(NodeVisit<TNode>?);

        var actual =
          actualPointer < actualResults.Length
          ? actualResults[actualPointer]
          : default(NodeVisit<TNode>?);

        if (expectedDiff.Type == ChangeType.Unchanged)
        {
          results.Add(WriteNodeVisitDiffs(" ", expected.Value, actual.Value));
          expectedPointer++;
          actualPointer++;
        }
        else if (expectedDiff.Type == ChangeType.Modified)
        {
          results.Add(WriteNodeVisitDiffs("M", expected.Value, actual.Value));
          expectedPointer++;
          actualPointer++;
        }
        else if (expectedDiff.Type == ChangeType.Inserted)
        {
          results.Add(CreateNodeVisitStringArray("+", expected.Value));
          expectedPointer++;
        }
        else if (expectedDiff.Type == ChangeType.Imaginary)
        {
          results.Add(CreateNodeVisitStringArray("I", actual.Value));
          actualPointer++;
        }
        else if (expectedDiff.Type == ChangeType.Deleted)
        {
          if (actual == null)
          {
            results.Add(CreateNodeVisitStringArray("-", expected.Value));
            actualPointer++;
          }
          else
          {
            results.Add(CreateNodeVisitStringArray("-", actual.Value));
            expectedPointer++;
          }
        }
        else
        {
          throw new Exception("Unexpected ChangeType.");
        }
      }

      var maxColumnWidths =
        results[0]
        .Select((_, column) => results.Max(result => result[column].Length))
        .ToArray();

      var horizontalRule = "".PadRight(maxColumnWidths.Sum() + 6, '-');

      yield return string.Join(" ", results[0].Select((x, i) => x.PadRight(maxColumnWidths[i], ' ')));
      yield return horizontalRule;

      foreach (var result in results.Skip(1))
        yield return string.Join(" ", result.Select((x, i) => x.PadRight(maxColumnWidths[i], ' ')));
    }

    private static string[] CreateNodeVisitStringArray<TNode>(string prefix, NodeVisit<TNode> result)
    {
      return
        new string[]
        {
          prefix,
          TreenumeratorModeMap.ToChar(result.Mode).ToString(),
          result.Node.ToString(),
          result.VisitCount.ToString(),
          result.Position.ToString(),
        };
    }

    private static string[] WriteNodeVisitDiffs<TNode>(
      string prefix,
      NodeVisit<TNode> expected,
      NodeVisit<TNode> actual)
    {
      var modeDiff = WriteValueDiffs(expected.Mode, actual.Mode);
      var nodeDiff = WriteValueDiffs(expected.Node, actual.Node);
      var visitCountDiff = WriteValueDiffs(expected.VisitCount, actual.VisitCount);
      var positionDiff = WriteValueDiffs(expected.Position, actual.Position);

      return new string[] { prefix, modeDiff, nodeDiff, visitCountDiff, positionDiff };
    }

    private static string WriteValueDiffs<TNode>(TNode expected, TNode actual)
    {
      if (expected.Equals(actual))
        return PropertyToString(expected);

      return $"{PropertyToString(expected)}|{PropertyToString(actual)}";
    }

    private static string PropertyToString<TNode>(TNode value)
    {
      if (value is TreenumeratorMode mode)
        return TreenumeratorModeMap.ToChar(mode).ToString();

      return value.ToString();
    }
  }
}
