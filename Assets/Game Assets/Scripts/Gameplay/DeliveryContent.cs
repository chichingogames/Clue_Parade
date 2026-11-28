using System;
using System.Collections.Generic;
using UnityEngine;

// The any-row cases (Assets/Game Assets/Data/delivery_cases.json, converted from the
// Tilewrap_Any_Row_5_Case_Playtest delivery). Item sprites and subject rigs are the 30-case content
// pack's (FileRef.Sprite / FileRef.Rig). Parsed once by FileRef.Cases.
//
// A case: three subjects (one per board column) and a list of sets. A set names one item per
// column; it is delivered when its three items stand under their owners in ANY row. Two of its
// three clues are shown (visibleClues); a clue with a referenceItem points at an already delivered
// prop in the subjects' scene. start is the first board (row-major, "" = empty), refills drop in
// after a delivery, one triple per delivery while they last. orderMode "staged": sets in order;
// "choice": up to two available sets at once.
[Serializable]
public class DeliveryContent
{
    public string version;
    public string sourcePack;
    public DeliveryCase[] cases;

    public static DeliveryContent Parse(string json)
    {
        var content = JsonUtility.FromJson<DeliveryContent>(json);
        if (content?.cases == null) throw new FormatException("no cases");
        foreach (var c in content.cases) c.Index();
        return content;
    }
}

[Serializable]
public class DeliveryCase
{
    public int id;
    public string title;
    public string subtitle;
    public DeliverySubject[] subjects;
    public DeliverySet[] sets;
    public DeliveryItem[] items;
    public string[] start;
    public DeliveryRefill[] refills;
    public int moveLimit;                 // 0 = unlimited
    public string orderMode;              // "staged" or "choice"
    public int firstClearCoins;
    public DeliveryExtraLayer[] extraLayers;
    public bool hasTutorial;
    public DeliveryTutorial tutorial;     // case 1: the guided first swap
    public string completionText;

    [NonSerialized] private Dictionary<string, DeliveryItem> _items;

    public bool HasMoveLimit => moveLimit > 0;
    public bool IsChoice => orderMode == "choice";

    public DeliveryItem Item(string id) => id != null && _items.TryGetValue(id, out var item) ? item : null;

    internal void Index()
    {
        _items = new Dictionary<string, DeliveryItem>(StringComparer.Ordinal);
        foreach (var item in items) _items[item.id] = item;
        refills ??= new DeliveryRefill[0];
        extraLayers ??= new DeliveryExtraLayer[0];
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

[Serializable]
public class DeliverySet
{
    public string label;                  // "Hats"
    public string action;                 // "Deliver their hats."
    public string[] items;                // by column
    public string[] clues;                // by column
    public DeliveryClue[] visibleClues;   // the two shown
}

[Serializable]
public class DeliveryClue
{
    public int column;
    public string icon;
    public string text;
    public string referenceItem;          // "" or a delivered item the clue points at ("scene clue")

    public bool IsSceneClue => !string.IsNullOrEmpty(referenceItem);
}

[Serializable]
public class DeliveryItem
{
    public string id;
    public string label;
    public string sprite;
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
