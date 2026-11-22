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
        BuildSound(managers);
        BuildCanvas();
        BuildEventSystem();
        BuildTester();
        UIBuilder.Build();
        UIBuilder.BuildLoading();

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
            GetOrAdd<Manager_Audio>(go),
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
        else
        {
            // An existing list is kept; managers added since it was filled are inserted at their place in `order`.
            for (int i = 0; i < order.Length; i++)
            {
                bool present = false;
                for (int j = 0; j < list.arraySize; j++) present |= list.GetArrayElementAtIndex(j).objectReferenceValue == order[i];
                if (present) continue;
                list.InsertArrayElementAtIndex(Mathf.Min(i, list.arraySize));
                list.GetArrayElementAtIndex(Mathf.Min(i, list.arraySize - 1)).objectReferenceValue = order[i];
            }
            list.serializedObject.ApplyModifiedProperties();
        }

        return go;
    }

    private static void BuildBoard(GameObject managers)
    {
        var gamePlay = GetOrCreateRoot("GamePlay");

        // Booth art behind everything (Manager_Board.FitCamera scales it to cover the view).
        var backgroundGo = GetOrCreateChild(gamePlay.transform, "Background", out bool backgroundCreated);
        var background = GetOrAdd<SpriteRenderer>(backgroundGo.gameObject);
        if (backgroundCreated)
        {
            background.sprite = AssetBuilder.LoadTilewrap("Backgrounds/booth_background");
            background.sortingOrder = SortOrder.Background;
        }
        var board = GetOrCreateChild(gamePlay.transform, "Board");

        // The pack's wooden frame, drawn over the tiles (Manager_Board.Build sets its size).
        var frameGo = GetOrCreateChild(board, "Frame", out bool frameCreated);
        var frame = GetOrAdd<SpriteRenderer>(frameGo.gameObject);
        if (frameCreated)
        {
            frame.sprite = AssetBuilder.LoadTilewrap("UI/board_frame");
            frame.drawMode = SpriteDrawMode.Sliced;
            frame.sortingOrder = SortOrder.Frame;
        }

        // Outline around the active step's row (Manager_Board positions and sizes it).
        var activeRowGo = GetOrCreateChild(board, "Active Row", out bool activeRowCreated);
        var activeRow = GetOrAdd<SpriteRenderer>(activeRowGo.gameObject);
        if (activeRowCreated)
        {
            activeRow.sprite = AssetBuilder.LoadTilewrap("UI/active_row_outline");
            activeRow.drawMode = SpriteDrawMode.Sliced;
            activeRow.sortingOrder = SortOrder.ActiveRow;
        }

        var maskGo = GetOrCreateChild(board, "Mask", out bool maskCreated);
        var mask = GetOrAdd<SpriteMask>(maskGo.gameObject);
        if (maskCreated) mask.sprite = AssetBuilder.LoadSprite(AssetBuilder.SolidSprite);

        var cells = GetOrCreateChild(board, "Cells");
        var tiles = GetOrCreateChild(board, "Tiles");
        var tags = GetOrCreateChild(board, "Tags");

        var so = new SerializedObject(managers.GetComponent<Manager_Board>());
        SetIfEmpty(so, "_boardRoot", board);
        SetIfEmpty(so, "_frame", frame);
        SetIfEmpty(so, "_mask", mask);
        SetIfEmpty(so, "_activeRow", activeRow);
        SetIfEmpty(so, "_background", background);
        SetIfEmpty(so, "_cellsRoot", cells);
        SetIfEmpty(so, "_tilesRoot", tiles);
        SetIfEmpty(so, "_tagsRoot", tags);
        so.ApplyModifiedProperties();
    }

    // Sound/Music and Sound/SFX: one AudioSource each, wired into Manager_Audio. Their Volume is the
    // level to tune (music 0.45 to start). Existing sources keep their settings.
    private static void BuildSound(GameObject managers)
    {
        var sound = GetOrCreateRoot("Sound");

        var musicGo = GetOrCreateChild(sound.transform, "Music", out bool musicCreated);
        var music = GetOrAdd<AudioSource>(musicGo.gameObject);
        if (musicCreated)
        {
            music.playOnAwake = false;
            music.loop = true;
            music.volume = 0.45f;
        }

        var sfxGo = GetOrCreateChild(sound.transform, "SFX", out bool sfxCreated);
        var sfx = GetOrAdd<AudioSource>(sfxGo.gameObject);
        if (sfxCreated) sfx.playOnAwake = false;

        var so = new SerializedObject(managers.GetComponent<Manager_Audio>());
        SetIfEmpty(so, "_music", music);
        SetIfEmpty(so, "_sfx", sfx);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x6E, 0x41, 0x55, 0xFF);   // the booth wall, until the background art is in
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
        var container = GetOrCreateUI(canvasGo.transform, "Container");
        GetOrAdd<SafeArea>(container.gameObject);
        GetOrCreateUI(container, "InGame");                               // UI_InGame (UIBuilder builds its Reference HUD)
        GetOrCreateUI(container, "PopUps");                               // Manager_PopUp._popUpParent

        var darkArea = GetOrCreateUI(canvasGo.transform, "Dark Area", out bool createdDarkArea);
        if (createdDarkArea)
        {
            var image = darkArea.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.7f);
            darkArea.gameObject.SetActive(false);
        }

        GetOrCreateUI(canvasGo.transform, "Loading Screen");             // UI_Loading (UIBuilder.BuildLoading)
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

    // Cheat panel. Tagged EditorOnly so Unity strips the whole object from builds.
    private static void BuildTester()
    {
        var go = GetOrCreateRoot("Tester");
        go.tag = "EditorOnly";
        GetOrAdd<TestManager>(go);
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
