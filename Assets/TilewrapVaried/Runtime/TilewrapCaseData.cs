using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chichingo.TilewrapVaried
{
    [Serializable] public sealed class MoveData
    {
        public string axis;
        public int line;
        public int direction;
    }
    [Serializable] public sealed class SubjectData
    {
        public string id, name, rigId;
        public int column;
    }
    [Serializable] public sealed class CategoryData
    {
        public string id, label;
        public int row;
    }
    [Serializable] public sealed class ItemData
    {
        public string id, label, sprite, categoryId, clue;
        public int targetCell;
    }
    [Serializable] public sealed class TutorialData
    {
        public string entryText, supportText, focus;
        public float guideLoopSeconds;
    }
    [Serializable] public sealed class TilewrapCaseData
    {
        public int schemaVersion, id, rows, columns, moveLimit, minimumMoves, firstClearCoins, nextLevelId;
        public string title, goalText, setupText, completionText, difficulty;
        public int chapterId, designSeed;
        public string[] startingCells, goalCells;
        public SubjectData[] subjects;
        public CategoryData[] categories;
        public ItemData[] items;
        public MoveData[] solution;
        public MoveData requiredFirstSwipe;
        public TutorialData tutorial;

        public static TilewrapCaseData Parse(TextAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var data = JsonUtility.FromJson<TilewrapCaseData>(source.text);
            if (data == null) throw new FormatException("Empty case JSON: " + source.name);
            data.Validate();
            return data;
        }
        public void Validate()
        {
            if (schemaVersion != 2 || rows != 3 || columns != 3 || moveLimit < 0)
                throw new FormatException("Unsupported Tilewrap case format.");
            if (startingCells == null || goalCells == null || startingCells.Length != 9 || goalCells.Length != 9)
                throw new FormatException("A case must contain exactly nine starting and goal cells.");
            var unique = new HashSet<string>(goalCells, StringComparer.Ordinal);
            if (unique.Count != 9 || unique.Contains(null) || unique.Contains(""))
                throw new FormatException("Goal item IDs must be nonempty and unique.");
            if (!unique.SetEquals(startingCells) || new HashSet<string>(startingCells).Count != 9)
                throw new FormatException("The starting board must be a permutation of the goal items.");
            if (subjects == null || subjects.Length != 3 || categories == null || categories.Length != 3 || items == null || items.Length != 9)
                throw new FormatException("A case requires three subjects, three categories and nine items.");
            var cells = new HashSet<int>();
            foreach (var item in items)
                if (item == null || item.targetCell < 0 || item.targetCell >= 9 || !cells.Add(item.targetCell) || goalCells[item.targetCell] != item.id)
                    throw new FormatException("Invalid item target binding.");
        }
    }

    [Serializable] public sealed class SpriteImportData
    {
        public string key, path;
        public int width, height;
        public float[] pivot;
        public int[] border;
    }
    [Serializable] public sealed class RigLayerData
    {
        public string sprite, itemId;
        public int[] rect;
        public int goalIndex, order;
    }
    [Serializable] public sealed class RigData
    {
        public string id;
        public int width, height;
        public RigLayerData[] layers;
    }
    [Serializable] public sealed class FontImportData { public string key, path; }
    [Serializable] public sealed class AudioImportData { public string key, path; public float volume; }
    [Serializable] public sealed class ContentManifest
    {
        public int schemaVersion;
        public string contentId, uiLayoutPath, hintTablePath, campaignPath;
        public SpriteImportData[] sprites;
        public RigData[] rigs;
        public string[] levelPaths;
        public FontImportData[] fonts;
        public AudioImportData[] audio;
    }

    [Serializable] public sealed class CampaignChapterData
    {
        public int id, firstLevel, lastLevel;
        public string title, thumbnailItem;
    }
    [Serializable] public sealed class CampaignLevelData
    {
        public int id, chapterId, minimumMoves, moveLimit;
        public string title, difficulty;
    }
    [Serializable] public sealed class MapNodeData { public int levelId; public float x, y; }
    [Serializable] public sealed class MapSectionData { public int chapterId; public float y; public string title; }
    [Serializable] public sealed class CampaignMapData
    {
        public int referenceWidth, totalHeight, sectionHeight, nodeDiameter;
        public string pathSprite, currentSprite, openSprite, lockedSprite;
        public MapNodeData[] nodes;
        public MapSectionData[] sections;
    }
    [Serializable] public sealed class CampaignData
    {
        public string contentId, endingText;
        public int levelCount, firstClearCoins, replayCoins;
        public int[] hardLevels;
        public CampaignChapterData[] chapters;
        public CampaignLevelData[] levels;
        public CampaignMapData map;
    }
}
