using System.Linq;
using UnityEditor;
using UnityEngine;

// Checks every case in FileRef.Cases: three subjects whose fitted rig prefabs exist with every part's sprite, every item,
// clue, evidence and answer sprite, a 3 x rows start, each set names three known items, every clue's
// rule and refs name known items, the inventory adds up (start + refills = every item once), and
// following the hint from the start delivers every set (DeliveryRules.Solve), within the move limit
// when there is one (the scope asks practice limits to allow twice a verified route: reported).
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
        {
            var rig = fileRef.Rig(subject.rigId);
            if (rig == null) return $"rig prefab '{subject.rigId}' is missing (run Build current 30-level content)";
            foreach (var binding in rig.bindings)
            {
                var image = binding.layer != null ? binding.layer.GetComponent<UnityEngine.UI.Image>() : null;
                if (image == null || image.sprite == null) return $"rig '{subject.rigId}' has a part without a sprite";
                if (!binding.alwaysVisible && !string.IsNullOrEmpty(binding.itemId) && level.Item(binding.itemId) == null)
                    return $"rig '{subject.rigId}' fits unknown item '{binding.itemId}'";
            }
        }
        foreach (var item in level.items)
            if (fileRef.Sprite(item.sprite) == null) return $"item sprite '{item.sprite}' is missing";
        if (level.start == null || level.start.Length != level.Cells) return $"start must have {level.Cells} cells";
        foreach (var set in level.sets)
        {
            if (set.items.Length != 3) return $"set '{set.label}' needs three items";
            foreach (var id in set.items) if (level.Item(id) == null) return $"set '{set.label}' names unknown item '{id}'";
        }
        for (int i = 0; i < level.sets.Length; i++)
        {
            var clues = level.CluesFor(i);
            if (clues.Length < 2) return $"set '{level.sets[i].label}' shows fewer than two clues";
            foreach (var clue in clues)
            {
                if (fileRef.Sprite(level.SpriteKey(clue.icon)) == null) return $"clue icon '{clue.icon}' is missing";
                if (clue.rule == null || level.Item(clue.rule.a) == null) return $"clue \"{clue.text}\" has no known item";
                if (!string.IsNullOrEmpty(clue.rule.b) && level.Item(clue.rule.b) == null) return $"clue \"{clue.text}\" names unknown '{clue.rule.b}'";
                if (clue.refs.Any(r => level.Item(r) == null)) return $"clue \"{clue.text}\" refers to an unknown item";
            }
        }
        foreach (var key in new[] { level.evidenceIcon, level.caseResolution.icon }.Where(k => !string.IsNullOrEmpty(k)))
            if (fileRef.Sprite(level.SpriteKey(key)) == null) return $"evidence sprite '{key}' is missing";
        if (level.hasDeduction && (level.deduction.answer < 0 || level.deduction.answer >= level.deduction.options.Length))
            return "the deduction answer is not one of its options";
        try
        {
            var start = DeliveryRules.Create(level);
            DeliveryRules.CheckInvariant(start, level);
            var parked = DeliveryRules.Ready(start, level);
            if (parked.HasValue && DeliveryRules.Eligible(start, level).Contains(parked.Value.index))
                return "the open request is already complete at the start";
            var route = DeliveryRules.Solve(level);
            if (level.HasMoveLimit && route.Count > level.moveLimit)
                return $"the hint route takes {route.Count} swaps, the limit is {level.moveLimit}";
            note = $"{level.rows} rows, hint route {route.Count} swaps" +
                   (level.HasMoveLimit ? $", limit {level.moveLimit}{(route.Count * 2 > level.moveLimit ? " (under twice the route)" : "")}" : "") +
                   (parked.HasValue ? $"; set '{level.sets[parked.Value.index].label}' is complete at the start and clears on the first swap" : "");
        }
        catch (System.Exception e)
        {
            return e.Message;
        }
        return null;
    }
}
