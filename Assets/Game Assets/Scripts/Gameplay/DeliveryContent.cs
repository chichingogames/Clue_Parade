using System;
using System.Collections.Generic;
using UnityEngine;

// The 30 deduction cases (Assets/Game Assets/Data/delivery_cases.json, converted from the
// Tilewrap_Deduction_30_Level_Playtest delivery, "Follow the Evidence"). Item sprites and rig parts
// are the 30-case content pack's sprites (FileRef.Sprite) plus the evidence props in
// Sprites/Evidence. Parsed once by FileRef.Cases.
//
// A case: three subjects (one per board column, each drawn from its rig's layers) and a list of
// sets. A set names one item per column; it is delivered when its three items stand under their
// owners in ANY row. The board is 3 columns x rows (3 or 4). Clues: a set's two visibleClues, or
// for the first three sets of a case with a cluePool, the shared pool (paged two at a time). start
// is the first board (row-major, "" = empty); refills drop in after a delivery, one triple per
// delivery while they last. orderMode "staged": sets in order; "choice": up to two available sets;
// "hybrid": the first three in order, then choice.
[Serializable]
public class DeliveryContent
{
    public string version;
    public string title;
    public string sourcePack;
    public DeliveryEconomy economy;
    public DeliveryChapter[] chapters;
    public DeliveryCase[] cases;

    public static DeliveryContent Parse(string json)
    {
        var content = JsonUtility.FromJson<DeliveryContent>(json);
        if (content?.cases == null) throw new FormatException("no cases");
        content.economy ??= new DeliveryEconomy();
        content.chapters ??= new DeliveryChapter[0];
        foreach (var c in content.cases) c.Index();
        return content;
    }
}

// Help prices and ad pacing (the playtest's; the ads themselves are not in the game).
[Serializable]
public class DeliveryEconomy
{
    public int hintCoins = 30;
    public int continueCoins = 80;
    public int continueMoves = 5;
    public int maxContinues = 1;
    public int freeHelpThroughLevel = 8;
}

[Serializable]
public class DeliveryChapter
{
    public int id;
    public string title;
    public int firstLevel;
    public int lastLevel;
}

[Serializable]
public class DeliveryCase
{
    public int id;
    public string title;
    public string subtitle;
    public string question;               // the case question under the title
    public string setupText;
    public string completionText;
    public string nextLead;
    public string evidenceReveal;
    public string evidenceIcon;
    public string reasoningTip;           // coach line for cases 6-14
    public int rows = 3;
    public string orderMode;              // "staged", "choice" or "hybrid"
    public string difficulty;             // "practice" or "expert"
    public int chapterId;
    public int moveLimit;                 // 0 = unlimited
    public int firstClearCoins;
    public int referenceMoveCount;
    public DeliverySubject[] subjects;
    public DeliveryRig[] rigs;            // by column
    public DeliveryItem[] items;
    public DeliverySet[] sets;
    public DeliveryClue[] cluePool;       // shared clues of the first three sets (may be empty)
    public string[] start;
    public DeliveryRefill[] refills;
    public DeliveryExtraLayer[] extraLayers;
    public bool hasTutorial;
    public DeliveryTutorial tutorial;     // case 1: the guided first swap
    public bool hasDossier;
    public DeliveryDossier dossier;       // a badge value per profile (age, price, speed...)
    public DeliveryResolution caseResolution;
    public bool hasDeduction;
    public DeliveryDeduction deduction;   // milestone cases: a question before the win

    [NonSerialized] private Dictionary<string, DeliveryItem> _items;

    public bool HasMoveLimit => moveLimit > 0;
    public bool IsExpert => difficulty == "expert";
    public int Cells => rows * 3;

    public DeliveryItem Item(string id) => id != null && _items.TryGetValue(id, out var item) ? item : null;

    // An item's sprite key, or the key itself (art that is not an item: evidence props).
    public string SpriteKey(string id) => Item(id)?.sprite ?? id;

    // The clues shown for a set: the shared pool for the first three sets when the case has one.
    public DeliveryClue[] CluesFor(int set) =>
        set >= 0 && set < 3 && cluePool.Length > 0 ? cluePool : set >= 0 && set < sets.Length ? sets[set].visibleClues : new DeliveryClue[0];

    internal void Index()
    {
        _items = new Dictionary<string, DeliveryItem>(StringComparer.Ordinal);
        foreach (var item in items) _items[item.id] = item;
        if (rows < 3) rows = 3;
        refills ??= new DeliveryRefill[0];
        extraLayers ??= new DeliveryExtraLayer[0];
        cluePool ??= new DeliveryClue[0];
        rigs ??= new DeliveryRig[0];
        for (int i = 0; i < start.Length; i++) if (start[i] == "") start[i] = null;
    }
}

[Serializable]
public class DeliverySubject
{
    public string id;
    public string name;
    public int column;
    public string rigId;
}

// A subject's picture: layers stacked by order on a width x height canvas. A layer with an itemId
// appears once that item is delivered; the others (the base) always show.
[Serializable]
public class DeliveryRig
{
    public string id;
    public float width;
    public float height;
    public DeliveryRigLayer[] layers;
}

[Serializable]
public class DeliveryRigLayer
{
    public string sprite;
    public float[] rect;                  // x, y, w, h in rig pixels, top-left origin
    public int goalIndex;                 // -1 = base
    public string itemId;
    public int order;
}

[Serializable]
public class DeliverySet
{
    public string label;                  // "Hats"
    public string action;
    public string[] items;                // by column
    public DeliveryClue[] visibleClues;   // the set's own two clues
}

// A clue card: icon (an item id or art key), text, the rule it states and the items it names
// (refs: tapping it highlights them; it is CONFIRMED once they are all delivered).
[Serializable]
public class DeliveryClue
{
    public string icon;
    public string text;
    public DeliveryRule rule;
    public string[] refs;
    public int column;                    // the profile it names, or -1
}

// kind: at (a in column), notAt, same (a and b share a profile), different, left (a left of b),
// next, notNext, lowest / highest (a has the dossier's lowest / highest value), less.
[Serializable]
public class DeliveryRule
{
    public string kind;
    public string a;
    public string b;
    public int column = -1;
}

[Serializable]
public class DeliveryItem
{
    public string id;
    public string label;
    public string sprite;
    public bool native = true;            // false: a found object (shown as a packed prop, not on the rig)
}

[Serializable]
public class DeliveryRefill
{
    public string[] items;                // by column
}

// A delivered refill item drawn on a subject's rig: rect = x, y, w, h in the rig's native pixels
// (top-left origin), stacked by order over the rig's own layers.
[Serializable]
public class DeliveryExtraLayer
{
    public string id;
    public int column;
    public float[] rect;
    public int order;
}

[Serializable]
public class DeliveryTutorial
{
    public int from;
    public int to;
    public string text;
}

[Serializable]
public class DeliveryDossier
{
    public string label;                  // "Speed"
    public float[] values;                // by column
    public string[] labels;               // "40 km/h"
    public string low;
    public string high;
}

[Serializable]
public class DeliveryResolution
{
    public string question;
    public string answer;
    public string icon;
    public string[] evidence;
    public string[] statements;           // witness statements (one is wrong)
}

[Serializable]
public class DeliveryDeduction
{
    public string question;
    public string[] options;
    public int answer;
    public string[] evidence;
}
