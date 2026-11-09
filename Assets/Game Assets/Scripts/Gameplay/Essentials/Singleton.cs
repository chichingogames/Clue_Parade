using UnityEngine;

public interface ISingleton
{
    void Initialize();
}

// Scene singleton. Put init code in Awake2(), never in Awake().
// Initialized either by SingletonManager (ordered) or by Unity's Awake, whichever comes first.
// The guard is per instance (not static like Color Farm's), so a duplicate is detected and removed.
public abstract class Singleton<T> : MonoBehaviour, ISingleton where T : Singleton<T>
{
    public static T Instance { get; private set; }

    private bool _initialized;

    private void Awake() => Initialize();

    public void Initialize()
    {
        if (_initialized) return;

        if (Instance != null && Instance != this)
        {
            // Managers share one GameObject, so remove only the duplicate component.
            Debug.LogWarning($"[Singleton] Duplicate {typeof(T).Name} on '{name}' removed.");
            Destroy(this);
            return;
        }

        _initialized = true;
        Instance = (T)this;
        Awake2();
    }

    public virtual void Awake2() { }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
