using UnityEngine;

// Plays UI_Confetti when Manager_Flow celebrates a win. Kept apart so the confetti view stays a
// plain effect with no manager reference.
[RequireComponent(typeof(UI_Confetti))]
public class UI_ConfettiListener : MonoBehaviour
{
    private UI_Confetti _confetti;

    private void Awake() => _confetti = GetComponent<UI_Confetti>();

    private void OnEnable()
    {
        if (Manager_Flow.Instance != null) Manager_Flow.Instance.OnWinCelebration += _confetti.Play;
    }

    private void OnDisable()
    {
        if (Manager_Flow.Instance != null) Manager_Flow.Instance.OnWinCelebration -= _confetti.Play;
    }
}
