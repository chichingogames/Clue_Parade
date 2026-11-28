using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chichingo.TilewrapMystery
{
    [Serializable] public sealed class SwapData { public int a, b; }
    [Serializable] public sealed class SubjectData { public string id, name, rigId; public int column; }
    [Serializable] public sealed class ItemData { public string id, label, sprite; public bool native; }
    [Serializable] public sealed class RuleData { public string kind, a, b; public int column; }
    [Serializable] public sealed class ClueData { public string icon, text; public RuleData rule; public string[] refs; }
    [Serializable] public sealed class SetData { public string label, action, clueType; public string[] items; public ClueData[] visibleClues; }
    [Serializable] public sealed class RefillData { public string[] items; }
    [Serializable] public sealed class DossierData { public string label, low, high; public float[] values; public string[] labels; }
    [Serializable] public sealed class ResolutionData { public string question, answer, icon; public string[] evidence, statements; }
    [Serializable] public sealed class TutorialData { public int from, to; public string text; }
    [Serializable] public sealed class DeductionData { public string question; public string[] evidence, options; public int answer; }
    [Serializable] public sealed class LevelData
    {
        public int schemaVersion, id, rows, columns, moveLimit, referenceMoveCount, firstClearCoins, chapterId, nextLevelId;
        public string title, subtitle, question, setupText, completionText, orderMode, difficulty, storyQuestion, reasoningTip, evidenceIcon, evidenceReveal, nextLead;
        public SubjectData[] subjects; public ItemData[] items; public SetData[] sets;
        public string[] start; public RefillData[] refills; public SwapData[] referenceSolution;
        public ClueData[] cluePool; public DossierData dossier; public ResolutionData caseResolution;
        public string[] coaching;
        public TutorialData tutorial; public DeductionData deduction;
        public ItemData GetItem(string id)
        {
            foreach (var item in items) if (item.id == id) return item;
            throw new KeyNotFoundException("Unknown item: " + id);
        }
        public void Validate()
        {
            if (schemaVersion != 5 || columns != 3 || rows < 3 || rows > 4 || start == null || start.Length != rows * columns)
                throw new FormatException("Unsupported level geometry: " + id);
            if (subjects == null || subjects.Length != 3 || sets == null || sets.Length < 3 || moveLimit < 0)
                throw new FormatException("Missing subjects or sets: " + id);
            var all = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items) if (item == null || String.IsNullOrEmpty(item.id) || !all.Add(item.id))
                throw new FormatException("Duplicate or missing item ID: " + id);
            var queued = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in start) if (!String.IsNullOrEmpty(item) && !queued.Add(item)) throw new FormatException("Duplicate starting item.");
            foreach (var refill in refills) foreach (var item in refill.items)
                if (refill.items.Length != 3 || !queued.Add(item)) throw new FormatException("Invalid refill inventory.");
            var goals = new HashSet<string>(StringComparer.Ordinal);
            foreach (var set in sets)
            {
                if (set.items.Length != 3 || set.visibleClues == null || set.visibleClues.Length != 2)
                    throw new FormatException("Every request needs three items and two pinned clues.");
                foreach (var item in set.items) if (!goals.Add(item)) throw new FormatException("Repeated goal item.");
            }
            if (!all.SetEquals(queued) || !all.SetEquals(goals)) throw new FormatException("Inconsistent inventory in level " + id);
        }
    }
    [Serializable] public sealed class ChapterData { public int id, firstLevel, lastLevel; public string title; }
    [Serializable] public sealed class EconomyData
    {
        public int hintCoins, continueCoins, continueMoves, maxContinues, freeHelpThroughLevel;
        public int firstForcedAdEligibleLevel, forcedAdWinSpacing, forcedAdCooldownSeconds;
    }
    [Serializable] public sealed class CampaignData { public int schemaVersion; public string contentId, title; public LevelData[] levels; public ChapterData[] chapters; public EconomyData economy; }
    [Serializable] public sealed class SpriteRecord { public string key, path; public int width, height, maxSize; public float[] pivot; public int[] border; }
    [Serializable] public sealed class RigLayer { public string sprite, itemId; public int[] rect; public int order; public bool alwaysVisible; }
    [Serializable] public sealed class RigData { public string id; public int width, height; public RigLayer[] layers; }
    [Serializable] public sealed class AudioRecord { public string key, path, loadType; public float volume; public bool loop; }
    [Serializable] public sealed class FontRecord { public string key, path; }
    [Serializable] public sealed class ContentData { public int schemaVersion; public string contentId, levelsPath, presentationPath; public SpriteRecord[] sprites; public RigData[] rigs; public AudioRecord[] audio; public FontRecord[] fonts; }
    [Serializable] public sealed class AmbientLayerData { public string sprite; public float[] rect; public float opacity, dx, dy, period, phase; }
    [Serializable] public sealed class BackgroundData { public string baseSprite; public string[] chapterTints; public AmbientLayerData[] layers; }
    [Serializable] public sealed class PresentationData { public int referenceWidth, referenceHeight; public BackgroundData background; }

    public sealed class TilewrapContentLibrary : ScriptableObject
    {
        public TextAsset contentJson, campaignJson, presentationJson;
        private ContentData content;
        private CampaignData campaign;
        public void ResetCaches() { content=null;campaign=null; }
        public ContentData Content { get { return content ?? (content = JsonUtility.FromJson<ContentData>(contentJson.text)); } }
        public CampaignData Campaign { get { return campaign ?? (campaign = JsonUtility.FromJson<CampaignData>(campaignJson.text)); } }
        public LevelData GetLevel(int id)
        {
            foreach (var level in Campaign.levels) if (level.id == id) return level;
            throw new KeyNotFoundException("Unknown level: " + id);
        }
        public Sprite GetSprite(string key)
        {
            foreach (var record in Content.sprites) if (record.key == key)
            {
                const string marker = "/Resources/"; int start = record.path.IndexOf(marker, StringComparison.Ordinal);
                string resource = record.path.Substring(start + marker.Length);
                resource = resource.Substring(0, resource.LastIndexOf('.'));
                var sprite = Resources.Load<Sprite>(resource);
                if (sprite == null) throw new InvalidOperationException("Missing sprite: " + key);
                return sprite;
            }
            throw new KeyNotFoundException("Unknown sprite key: " + key);
        }
        public TilewrapRigView CreateRig(string id, RectTransform frame)
        {
            var prefab = Resources.Load<TilewrapRigView>("TilewrapMystery/Rigs/" + id);
            if (prefab == null) throw new InvalidOperationException("Run Build current content first: " + id);
            var rig = Instantiate(prefab, frame, false);
            rig.Fit(frame.rect.size); rig.ApplyDelivered(new string[0]); return rig;
        }
    }
}
