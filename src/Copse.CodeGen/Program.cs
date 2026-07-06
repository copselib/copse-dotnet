using Copse.CodeGen;
using System;
using System.IO;

// Regenerate the sync twins from their async sources. The async source is the single source of truth;
// the .g.cs twins are checked in and guarded against drift by GeneratedTwinDriftTests.
//
//   dotnet run --project Copse.CodeGen                 -> regenerate ALL twins in the manifest
//   dotnet run --project Copse.CodeGen -- <src-root>   -> regenerate all, with an explicit src root
//   dotnet run --project Copse.CodeGen -- <in> <out>   -> transcribe a single file (ad hoc)

if (args.Length == 2)
{
  File.WriteAllText(args[1], AsyncToSync.Transform(File.ReadAllText(args[0]), Path.GetFileName(args[0])));
  Console.WriteLine($"generated {args[1]}");
  return 0;
}

var srcRoot = args.Length == 1 ? args[0] : FindSrcRoot();
if (srcRoot is null)
{
  Console.Error.WriteLine("could not locate the src root (a directory containing Copse.sln); pass it as an argument.");
  return 1;
}

foreach (var (asyncSource, twin) in GeneratorManifest.Pairs)
{
  var inPath = Path.Combine(srcRoot, asyncSource);
  var outPath = Path.Combine(srcRoot, twin);
  File.WriteAllText(outPath, AsyncToSync.Transform(File.ReadAllText(inPath), Path.GetFileName(inPath)));
  Console.WriteLine($"generated {twin}");
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
