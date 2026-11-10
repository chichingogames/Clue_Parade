using DG.Tweening;
using UnityEngine;

// The game's boot. Loads the save once, then initializes singletons in the Inspector order
// (ORDER MATTERS), before any default-order Awake runs.
[DefaultExecutionOrder(-900)]
public class SingletonManager : MonoBehaviour
{
    [SerializeField] private MonoBehaviour[] _singletonsToInitialize = new MonoBehaviour[0];

    private void Awake()
    {
        OfflineData.Load();     // once, before any Awake2 reads it

        foreach (var entry in _singletonsToInitialize)
        {
            if (entry is ISingleton singleton) singleton.Initialize();
            else Debug.LogError($"[SingletonManager] '{entry}' is not an ISingleton.", this);
        }

        Application.targetFrameRate = 60;
        DOTween.SetTweensCapacity(500, 100);
    }
}
