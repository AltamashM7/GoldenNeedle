using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace GoldenNeedle.Editor.Gameplay
{
    // Pipeline remains available to the Editor; product Players do not use its runtime or compiler.
    public sealed class ExcludeDevelopmentToolingFromPlayer : IFilterBuildAssemblies
    {
        public int callbackOrder => 0;

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            var excluded = assemblies.Where(IsDevelopmentTool).ToArray();
            if (excluded.Length > 0)
                UnityEngine.Debug.Log("Excluded Editor development tooling from Player: " +
                          string.Join(", ", excluded.Select(Path.GetFileName)));
            return assemblies.Where(path => !IsDevelopmentTool(path)).ToArray();
        }

        public static bool IsDevelopmentTool(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return name.Equals("Unity.Pipeline", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("Unity.Pipeline.", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Microsoft.CodeAnalysis.CSharp", StringComparison.OrdinalIgnoreCase);
        }
    }
}
