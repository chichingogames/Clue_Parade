using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public static class UnusedSprites
{
    public static string Run()
    {
        var roots = new List<string> { "Assets/Game Assets/Scenes/Game.unity" };
        roots.AddRange(AssetDatabase.FindAssets("", new[] { "Assets/Resources" }).Select(AssetDatabase.GUIDToAssetPath));
        roots.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath));
        roots.Add("Assets/TilewrapVaried/Generated/TilewrapContentLibrary.asset");   // case art, rigs, map, FX
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        var all = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => !p.StartsWith("Assets/TextMesh Pro/") && !p.StartsWith("Assets/Plugins/") && !p.EndsWith(".asset"))
            .Distinct().OrderBy(p => p).ToList();
        var unused = all.Where(p => !used.Contains(p)).ToList();
        var byFolder = unused.GroupBy(p => p.Substring(0, p.LastIndexOf('/'))).Select(g => $"{g.Key} ({g.Count()})");
        var usedHandoff = all.Where(p => used.Contains(p)).Count();
        return $"textures {all.Count}, used {usedHandoff}, unused {unused.Count}: " + string.Join(" | ", byFolder)
             + " || handoff unused: " + string.Join(", ", unused.Where(p => p.StartsWith("Assets/TilewrapVaried/")).Select(p => p.Substring(p.LastIndexOf('/') + 1)));
    }
}
