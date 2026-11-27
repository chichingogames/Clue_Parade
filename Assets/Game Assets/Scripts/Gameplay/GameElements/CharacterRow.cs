using System;
using TMPro;
using UnityEngine;

// The customers above the board (one per column, left to right) and the name tabs under them
// (symbol + name, sitting on the board's top edge), in world space (ShiftSort > Build Scene builds it as Board/Characters).
// Its local space is the reference screen (Manager_Board keeps it at the world origin), so its parts
// sit at ReferenceLayout's positions. Each slot has a Stand (the feet's position, scaled so a
// customer prefab fits the portrait height) that Manager_Board instantiates the column's Customer
// variant into. Pure view: Manager_Board places the slots and hands over names and customers.
public class CharacterRow : MonoBehaviour
{
    [Serializable]
    public struct Slot
    {
        public Transform Stand;            // the feet, behind the name tab; parent of the Customer
        public Transform Plate;            // the name tab
        public SpriteRenderer Symbol;      // the person's symbol (heart, circle, diamond) on the tab
        public TextMeshPro Name;
    }

    [SerializeField] private Slot[] _slots = new Slot[0];

    public int Count => _slots.Length;

    public Transform StandOf(int index) => _slots[index].Stand;

    // x: the column's centre in this row's local space.
    public void Show(int index, float x, string characterName, Sprite symbol)
    {
        var slot = _slots[index];
        SetActive(slot, true);
        slot.Name.text = characterName;
        slot.Symbol.sprite = symbol;
        slot.Symbol.gameObject.SetActive(symbol != null);
        SetX(slot.Stand, x);
        SetX(slot.Plate, x);
    }

    public void Hide(int index) => SetActive(_slots[index], false);

    private static void SetActive(Slot slot, bool active)
    {
        slot.Stand.gameObject.SetActive(active);
        slot.Plate.gameObject.SetActive(active);
    }

    private static void SetX(Transform t, float x) => t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);
}
