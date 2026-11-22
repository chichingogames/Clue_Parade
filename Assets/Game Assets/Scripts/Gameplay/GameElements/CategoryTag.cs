using TMPro;
using UnityEngine;

// The label beside a board row ("Hats"). Stays put while the row's tiles move.
public class CategoryTag : MonoBehaviour
{
    [SerializeField] private TextMeshPro _label;

    public void Setup(string category)
    {
        name = $"Tag {category}";
        _label.text = category;
    }
}
