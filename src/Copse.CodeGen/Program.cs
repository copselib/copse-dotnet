using Copse.CodeGen;
using System;
using System.IO;

// Regenerate the sync twins from their async sources (the manifest). The async source is the single
// source of truth; the .g.cs twins are checked in and guarded against drift by GeneratedTwinDriftTests.
//
//   dotnet run --project Copse.CodeGen                 -> regenerate ALL twins in the manifest
//   dotnet run --project Copse.CodeGen -- <src-root>   -> regenerate all, with an explicit src root

var srcRoot = args.Length == 1 ? args[0] : FindSrcRoot();
if (srcRoot is null)
{
  Console.Error.WriteLine("could not locate the src root (a directory containing Copse.sln); pass it as an argument.");
  return 1;
}

foreach (var entry in GeneratorManifest.Entries)
{
  var inPath = Path.Combine(srcRoot, entry.AsyncSource);
  var outPath = Path.Combine(srcRoot, entry.Twin);
  File.WriteAllText(outPath, AsyncToSync.Transform(entry, File.ReadAllText(inPath)));
  Console.WriteLine($"generated {entry.Twin}");
}
return 0;

// Walk up from the running assembly to the src directory (the one holding Copse.sln).
static string FindSrcRoot()
{
  for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
    if (File.Exists(Path.Combine(dir.FullName, "Copse.sln")))
      return dir.FullName;
  return null;
}
