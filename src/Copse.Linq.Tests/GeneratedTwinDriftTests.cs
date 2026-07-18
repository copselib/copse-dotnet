using Copse.CodeGen;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace Copse.Linq.Tests
{
  // The generated sync twins are checked in, so they can silently go stale: an async source is edited
  // without regenerating, or the transform changes. This guard runs the SAME transform in-process and
  // asserts each committed .g.cs matches -- making the transcription itself part of CI. If it fails,
  // regenerate: dotnet run --project Copse.CodeGen
  [TestClass]
  public class GeneratedTwinDriftTests
  {
    [TestMethod]
    public void EveryGeneratedTwinMatchesAFreshTransform()
    {
      var srcRoot = FindSrcRoot();
      Assert.IsNotNull(srcRoot, "could not locate the src root (a directory containing Copse.sln).");

      foreach (var entry in GeneratorManifest.Entries)
      {
        var sourcePath = Path.Combine(srcRoot, entry.AsyncSource);
        var twinPath = Path.Combine(srcRoot, entry.Twin);

        var expected = Normalize(AsyncToSync.Transform(entry, File.ReadAllText(sourcePath)));
        var actual = Normalize(File.ReadAllText(twinPath));

        Assert.AreEqual(expected, actual,
          $"{entry.Twin} is stale relative to {entry.AsyncSource}. Regenerate: dotnet run --project Copse.CodeGen");
      }
    }

    // The narrow phase's guard. The committed narrow twins are ALSO the sync entries' inputs, so
    // this test holding plus the one above proves the whole two-phase chain is fresh.
    [TestMethod]
    public void EveryGeneratedNarrowTwinMatchesAFreshTransform()
    {
      var srcRoot = FindSrcRoot();
      Assert.IsNotNull(srcRoot, "could not locate the src root (a directory containing Copse.sln).");

      foreach (var entry in GeneratorManifest.NarrowEntries)
      {
        var sourcePath = Path.Combine(srcRoot, entry.WideSource);
        var twinPath = Path.Combine(srcRoot, entry.Twin);

        var expected = Normalize(CompositeToNarrow.Transform(entry, File.ReadAllText(sourcePath)));
        var actual = Normalize(File.ReadAllText(twinPath));

        Assert.AreEqual(expected, actual,
          $"{entry.Twin} is stale relative to {entry.WideSource}. Regenerate: dotnet run --project Copse.CodeGen");
      }
    }

    // Compare on content, not line endings (git may normalize checked-in files).
    private static string Normalize(string s) => s.Replace("\r\n", "\n");

    private static string FindSrcRoot()
    {
      for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "Copse.sln")))
          return dir.FullName;
      return null;
    }
  }
}
