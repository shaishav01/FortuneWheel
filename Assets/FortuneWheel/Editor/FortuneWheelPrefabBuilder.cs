using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates dummy sprites and the FortuneWheel / FortuneWheelSlot prefabs.
/// Menu: Tools > Fortune Wheel.
/// </summary>
public static class FortuneWheelPrefabBuilder
{
    private const string Root = "Assets/FortuneWheel";
    private const string SpriteFolder = Root + "/Sprites";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string CirclePath = SpriteFolder + "/WheelCircle.png";
    private const string PointerPath = SpriteFolder + "/WheelPointer.png";
    private const string SlotPrefabPath = PrefabFolder + "/FortuneWheelSlot.prefab";
    private const string WheelPrefabPath = PrefabFolder + "/FortuneWheel.prefab";

    private const float WheelSize = 700f;
    private const float FrameThickness = 24f;

    [MenuItem("Tools/Fortune Wheel/Build Prefab")]
    public static void BuildPrefab()
    {
        EnsureFolder(SpriteFolder);
        EnsureFolder(PrefabFolder);

        Sprite circle = CreateSprite(CirclePath, 512, CircleAlpha);
        Sprite pointer = CreateSprite(PointerPath, 128, PointerAlpha);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        FortuneWheelSlot slotPrefab = BuildSlotPrefab(circle, font);
        GameObject wheelPrefab = BuildWheelPrefab(circle, pointer, font, slotPrefab);

        Selection.activeObject = wheelPrefab;
        EditorGUIUtility.PingObject(wheelPrefab);
        Debug.Log("[FortuneWheel] Prefab built at " + WheelPrefabPath);
    }

    [MenuItem("Tools/Fortune Wheel/Add To Open Scene")]
    public static void AddToScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WheelPrefabPath);
        if (prefab == null)
        {
            BuildPrefab();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WheelPrefabPath);
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        }

        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        Undo.RegisterCreatedObjectUndo(instance, "Add Fortune Wheel");
        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);
    }

    // ---------- Prefabs ----------

    private static FortuneWheelSlot BuildSlotPrefab(Sprite circle, Font font)
    {
        GameObject root = new GameObject("FortuneWheelSlot", typeof(RectTransform));
        Stretch((RectTransform)root.transform);

        UnityEngine.UI.Image segment = CreateImage("Segment", root.transform, circle, Color.white);
        Stretch(segment.rectTransform);
        segment.type = UnityEngine.UI.Image.Type.Filled;
        segment.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        segment.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        segment.fillClockwise = true;
        segment.fillAmount = 0.125f;

        UnityEngine.UI.Text label = CreateText("Label", root.transform, font, "0", 56);
        RectTransform labelRt = label.rectTransform;
        labelRt.anchorMin = labelRt.anchorMax = labelRt.pivot = new Vector2(0.5f, 0.5f);
        labelRt.sizeDelta = new Vector2(140f, 80f);
        labelRt.anchoredPosition = new Vector2(0f, WheelSize * 0.5f * 0.7f);
        var outline = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        FortuneWheelSlot slot = root.AddComponent<FortuneWheelSlot>();
        var so = new SerializedObject(slot);
        so.FindProperty("segment").objectReferenceValue = segment;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        Object.DestroyImmediate(root);
        return asset.GetComponent<FortuneWheelSlot>();
    }

    private static GameObject BuildWheelPrefab(Sprite circle, Sprite pointer, Font font, FortuneWheelSlot slotPrefab)
    {
        // FortuneWheel (root, holds the component)
        // ├── Frame (rim)
        // │   ├── Wheel (rotates)
        // │   │   └── Slots
        // │   ├── Hub
        // │   └── Pointer
        // ├── SpinButton
        // │   └── Label
        // └── ResultLabel
        GameObject root = new GameObject("FortuneWheel", typeof(RectTransform));
        RectTransform rootRt = (RectTransform)root.transform;
        rootRt.sizeDelta = new Vector2(WheelSize + 100f, WheelSize + 320f);

        UnityEngine.UI.Image frame = CreateImage("Frame", root.transform, circle, new Color32(0x3E, 0x27, 0x23, 0xFF));
        RectTransform frameRt = frame.rectTransform;
        frameRt.anchorMin = frameRt.anchorMax = frameRt.pivot = new Vector2(0.5f, 1f);
        frameRt.sizeDelta = Vector2.one * (WheelSize + FrameThickness * 2f);
        frameRt.anchoredPosition = new Vector2(0f, -60f);

        RectTransform wheel = new GameObject("Wheel", typeof(RectTransform)).GetComponent<RectTransform>();
        wheel.SetParent(frameRt, false);
        wheel.sizeDelta = Vector2.one * WheelSize;

        RectTransform slots = new GameObject("Slots", typeof(RectTransform)).GetComponent<RectTransform>();
        slots.SetParent(wheel, false);
        Stretch(slots);

        UnityEngine.UI.Image hub = CreateImage("Hub", frameRt, circle, new Color32(0xFF, 0xC1, 0x07, 0xFF));
        hub.rectTransform.sizeDelta = Vector2.one * 110f;

        UnityEngine.UI.Image pointerImg = CreateImage("Pointer", frameRt, pointer, Color.white);
        RectTransform pointerRt = pointerImg.rectTransform;
        pointerRt.anchorMin = pointerRt.anchorMax = new Vector2(0.5f, 1f);
        pointerRt.pivot = new Vector2(0.5f, 0.5f);
        pointerRt.sizeDelta = new Vector2(70f, 90f);
        pointerRt.anchoredPosition = new Vector2(0f, 0f);
        pointerImg.gameObject.AddComponent<UnityEngine.UI.Shadow>().effectDistance = new Vector2(3f, -3f);

        // Spin button
        UnityEngine.UI.Image buttonImg = CreateImage("SpinButton", root.transform,
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), new Color32(0x2E, 0x7D, 0x32, 0xFF));
        buttonImg.type = UnityEngine.UI.Image.Type.Sliced;
        buttonImg.raycastTarget = true;
        RectTransform buttonRt = buttonImg.rectTransform;
        buttonRt.anchorMin = buttonRt.anchorMax = buttonRt.pivot = new Vector2(0.5f, 0f);
        buttonRt.sizeDelta = new Vector2(320f, 100f);
        buttonRt.anchoredPosition = new Vector2(0f, 90f);
        UnityEngine.UI.Button button = buttonImg.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonImg;
        var colors = button.colors;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        button.colors = colors;
        UnityEngine.UI.Text buttonLabel = CreateText("Label", buttonRt, font, "SPIN", 48);
        Stretch(buttonLabel.rectTransform);

        // Result label
        UnityEngine.UI.Text result = CreateText("ResultLabel", root.transform, font, "Press Spin", 40);
        RectTransform resultRt = result.rectTransform;
        resultRt.anchorMin = resultRt.anchorMax = resultRt.pivot = new Vector2(0.5f, 0f);
        resultRt.sizeDelta = new Vector2(600f, 60f);
        resultRt.anchoredPosition = new Vector2(0f, 10f);

        FortuneWheel fortuneWheel = root.AddComponent<FortuneWheel>();
        var so = new SerializedObject(fortuneWheel);
        so.FindProperty("slotPrefab").objectReferenceValue = slotPrefab;
        so.FindProperty("slotContainer").objectReferenceValue = slots;
        so.FindProperty("wheel").objectReferenceValue = wheel;
        so.FindProperty("spinButton").objectReferenceValue = button;
        so.FindProperty("resultLabel").objectReferenceValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();

        fortuneWheel.RebuildSlots();

        GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, WheelPrefabPath);
        Object.DestroyImmediate(root);
        return asset;
    }

    // ---------- UI helpers ----------

    private static UnityEngine.UI.Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static UnityEngine.UI.Text CreateText(string name, Transform parent, Font font, string value, int size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text));
        go.transform.SetParent(parent, false);
        UnityEngine.UI.Text text = go.GetComponent<UnityEngine.UI.Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ---------- Dummy sprite generation ----------

    private delegate float AlphaFunc(float x, float y, int size);

    // Anti-aliased filled disc.
    private static float CircleAlpha(float x, float y, int size)
    {
        float r = size * 0.5f;
        float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
        return Mathf.Clamp01(r - 1f - d + 0.5f);
    }

    // Downward-pointing triangle (tip at the bottom center).
    private static float PointerAlpha(float x, float y, int size)
    {
        float halfWidth = y * 0.5f;
        float distToEdge = halfWidth - Mathf.Abs(x - size * 0.5f);
        return Mathf.Clamp01(distToEdge + 0.5f);
    }

    private static Sprite CreateSprite(string path, int size, AlphaFunc alpha)
    {
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                byte a = (byte)(alpha(x + 0.5f, y + 0.5f, size) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
