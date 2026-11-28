using UnityEditor;
using UnityEngine;

// Checks every any-row case in FileRef.Cases: three subjects with rigs, every item sprite, each set
// names three known items and two clues, the inventory adds up (start + refills = every item once),
// and following the hint from the start delivers every set (DeliveryRules.Solve), within the move
// limit when there is one.
public static class LevelValidator
{
    [MenuItem("ShiftSort/Validate Levels")]
    public static string Validate()
    {
        var content = FileRef.Instance.Cases;
        if (content == null)
        {
            Debug.LogError("[LevelValidator] FileRef has no cases. Assign Assets/Game Assets/Data/delivery_cases.json.");
            return "no content";
        }

        int ok = 0, failed = 0;
        foreach (var level in content.cases)
        {
            string error = Check(level, out string note);
            if (error == null)
            {
                ok++;
                Debug.Log($"[LevelValidator] case {level.id} \"{level.title}\": OK ({note}).");
            }
            else
            {
                failed++;
                Debug.LogError($"[LevelValidator] case {level.id} \"{level.title}\": {error}");
            }
        }

        string summary = $"[LevelValidator] {ok} case(s) OK, {failed} failed.";
        if (failed > 0) Debug.LogError(summary);
        else Debug.Log(summary);
        return summary;
    }

    private static string Check(DeliveryCase level, out string note)
    {
        note = "";
        var fileRef = FileRef.Instance;
        if (level.subjects.Length != 3) return "needs three subjects";
        foreach (var subject in level.subjects)
            if (fileRef.Rig(subject.rigId) == null) return $"rig '{subject.rigId}' is not in the content library";
        foreach (var item in level.items)
            if (fileRef.Sprite(item.sprite) == null) return $"item sprite '{item.sprite}' is not in the content library";
        if (level.start == null || level.start.Length != 9) return "start must have 9 cells";
        foreach (var set in level.sets)
        {
            if (set.items.Length != 3 || set.clues.Length != 3) return $"set '{set.label}' needs three items and three clues";
            foreach (var id in set.items) if (level.Item(id) == null) return $"set '{set.label}' names unknown item '{id}'";
            if (set.visibleClues.Length != 2) return $"set '{set.label}' should show two clues";
        }
        try
        {
            var start = DeliveryRules.Create(level);
            DeliveryRules.CheckInvariant(start, level);
            if (DeliveryRules.Ready(start, level).HasValue) return "a set is already delivered at the start";
            var route = DeliveryRules.Solve(level);
            if (level.HasMoveLimit && route.Count > level.moveLimit)
                return $"the hint route takes {route.Count} swaps, the limit is {level.moveLimit}";
            note = $"hint route {route.Count} swaps{(level.HasMoveLimit ? $", limit {level.moveLimit}" : "")}";
        }
        catch (System.Exception e)
        {
            return e.Message;
        }
        return null;
    }
}
