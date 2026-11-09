using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Builds the root structure of Game.unity described in skeleton.md section 1 and 9.1.
// Re-runnable: finds existing objects by path and only creates what is missing,
// so authored children and components are never touched.
public static class SceneBuilder
{
    private const string ScenePath = "Assets/Game Assets/Scenes/Game.unity";
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    [MenuItem("ShiftSort/Build Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogError($"[SceneBuilder] Open {ScenePath} first (active scene is '{scene.path}').");
            return;
        }

        var managers = BuildManagers();
        BuildBoard(managers);
        BuildCamera();
        BuildCanvas();
        BuildEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[SceneBuilder] Scene structure is up to date.");
    }

    private static GameObject BuildManagers()
    {
        var go = GetOrCreateRoot("Managers");
        GetOrAdd<GameTuning>(go);
        var singletonManager = GetOrAdd<SingletonManager>(go);

        // Init order: data providers first, Manager_Game last.
        MonoBehaviour[] order =
        {
            GetOrAdd<Manager_Level>(go),
            GetOrAdd<Manager_Board>(go),
            GetOrAdd<Manager_Input>(go),
            GetOrAdd<Manager_Game>(go),
        };

        var list = new SerializedObject(singletonManager).FindProperty("_singletonsToInitialize");
        if (list.arraySize == 0)
        {
            list.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            list.serializedObject.ApplyModifiedProperties();
        }

        return go;
    }

    private static void BuildBoard(GameObject managers)
    {
        var gamePlay = GetOrCreateRoot("GamePlay");
        var board = GetOrCreateChild(gamePlay.transform, "Board");

        var frameGo = GetOrCreateChild(board, "Frame", out bool frameCreated);
        var frame = GetOrAdd<SpriteRenderer>(frameGo.gameObject);
        if (frameCreated)
        {
            frame.sprite = AssetBuilder.LoadSprite(AssetBuilder.SlotSprite);
            frame.color = new Color(0.23f, 0.19f, 0.34f);
            frame.sortingOrder = SortOrder.Board;
        }

        var maskGo = GetOrCreateChild(board, "Mask", out bool maskCreated);
        var mask = GetOrAdd<SpriteMask>(maskGo.gameObject);
        if (maskCreated) mask.sprite = AssetBuilder.LoadSprite(AssetBuilder.SolidSprite);

        var cells = GetOrCreateChild(board, "Cells");
        var tiles = GetOrCreateChild(board, "Tiles");
        var markers = GetOrCreateChild(board, "Markers");

        var so = new SerializedObject(managers.GetComponent<Manager_Board>());
        SetIfEmpty(so, "_boardRoot", board);
        SetIfEmpty(so, "_frame", frame);
        SetIfEmpty(so, "_mask", mask);
        SetIfEmpty(so, "_cellsRoot", cells);
        SetIfEmpty(so, "_tilesRoot", tiles);
        SetIfEmpty(so, "_markersRoot", markers);
        so.ApplyModifiedProperties();
    }

    private static void BuildCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.97f, 0.93f, 0.87f);
    }

    private static Transform GetOrCreateChild(Transform parent, string name) =>
        GetOrCreateChild(parent, name, out _);

    private static Transform GetOrCreateChild(Transform parent, string name, out bool created)
    {
        var child = parent.Find(name);
        created = child == null;
        if (!created) return child;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Scene");
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void SetIfEmpty(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property.objectReferenceValue == null) property.objectReferenceValue = value;
    }

    private static void BuildCanvas()
    {
        var canvasGo = GetOrCreateRoot("Canvas");
        canvasGo.layer = LayerMask.NameToLayer("UI");

        var canvas = GetOrAdd<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = GetOrAdd<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GetOrAdd<GraphicRaycaster>(canvasGo);

        // Order matters: later siblings draw on top.
        var container = GetOrCreateUI(canvasGo.transform, "Container");   // SafeArea component goes here
        GetOrCreateUI(container, "InGame");                               // UI_InGame
        GetOrCreateUI(container, "PopUps");                               // Manager_PopUp._popUpParent

        var darkArea = GetOrCreateUI(canvasGo.transform, "Dark Area", out bool createdDarkArea);
        if (createdDarkArea)
        {
            var image = darkArea.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);
            darkArea.gameObject.SetActive(false);
        }

        GetOrCreateUI(canvasGo.transform, "Loading Screen");             // UI_LoadingScreen
        GetOrCreateUI(canvasGo.transform, "Transition Screen");          // UI_TransitionScreen
    }

    private static void BuildEventSystem()
    {
        var go = GetOrCreateRoot("EventSystem");
        GetOrAdd<EventSystem>(go);

        // New Input System only (activeInputHandler = 1): StandaloneInputModule would throw.
        var legacy = go.GetComponent<StandaloneInputModule>();
        if (legacy != null) Object.DestroyImmediate(legacy);
        GetOrAdd<InputSystemUIInputModule>(go);
    }

    private static GameObject GetOrCreateRoot(string name)
    {
        var existing = GameObject.Find("/" + name);
        if (existing != null) return existing;

        // GameObject.Find skips inactive objects, so check the roots directly too.
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name) return root;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Scene");
        return go;
    }

    private static RectTransform GetOrCreateUI(Transform parent, string name) =>
        GetOrCreateUI(parent, name, out _);

    private static RectTransform GetOrCreateUI(Transform parent, string name, out bool created)
    {
        var child = parent.Find(name);
        if (child != null)
        {
            created = false;
            return (RectTransform)child;
        }

        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build Scene");
        go.layer = LayerMask.NameToLayer("UI");

        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        created = true;
        return rect;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component =>
        go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
}
