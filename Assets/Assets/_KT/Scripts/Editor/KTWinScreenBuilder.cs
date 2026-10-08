using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Меню KT → Создать экран победы. Собирает на Canvas простейший экран победы из стандартных элементов Unity
/// (те же, что в меню GameObject → UI): Panel, два Text - TextMeshPro и две кнопки Button - TextMeshPro.
/// Кнопки уже подключены к SceneLoader. Оформление — на ваш вкус.
/// </summary>
public static class KTWinScreenBuilder
{
    [MenuItem("KT/Создать экран победы", priority = 20)]
    private static void Build()
    {
        if (Object.FindAnyObjectByType<WinScreen2D>() != null)
        {
            EditorUtility.DisplayDialog("Уже есть", "На сцене уже есть экран победы (WinScreen2D).", "Понятно");
            return;
        }

        Canvas canvas = GetOrCreateCanvas();
        EnsureEventSystem();

        DefaultControls.Resources uiResources = UIResources();
        TMP_DefaultControls.Resources tmpResources = TMPResources();

        // Panel — стандартная полупрозрачная панель на весь экран.
        GameObject panel = DefaultControls.CreatePanel(uiResources);
        panel.name = "WinScreen";
        panel.transform.SetParent(canvas.transform, false);

        TMP_Text title = CreateText(tmpResources, panel.transform, "Title", "Уровень пройден", new Vector2(0f, 80f), new Vector2(400f, 60f));
        TMP_Text stats = CreateText(tmpResources, panel.transform, "Stats", "Монеты: 0/0\nВремя: 00:00", new Vector2(0f, 0f), new Vector2(400f, 100f));
        title.alignment = TextAlignmentOptions.Center;
        stats.alignment = TextAlignmentOptions.Center;

        Button restart = CreateButton(tmpResources, panel.transform, "RestartButton", "Заново", new Vector2(-100f, -90f));
        Button next = CreateButton(tmpResources, panel.transform, "NextButton", "Дальше", new Vector2(100f, -90f));

        SceneLoader loader = canvas.GetComponent<SceneLoader>();
        if (loader == null) loader = Undo.AddComponent<SceneLoader>(canvas.gameObject);
        UnityEventTools.AddPersistentListener(restart.onClick, loader.RestartScene);
        UnityEventTools.AddPersistentListener(next.onClick, loader.LoadNextScene);

        WinScreen2D winScreen = Undo.AddComponent<WinScreen2D>(canvas.gameObject);
        SerializedObject serialized = new SerializedObject(winScreen);
        serialized.FindProperty("panel").objectReferenceValue = panel;
        serialized.FindProperty("statsLabel").objectReferenceValue = stats;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursively(panel, LayerMask.NameToLayer("UI"));
        panel.SetActive(false);
        Undo.RegisterCreatedObjectUndo(panel, "KT: экран победы");
        Selection.activeGameObject = panel;

        EditorUtility.DisplayDialog("Готово",
            "Экран победы WinScreen создан на «" + canvas.name + "» и выключен до победы.\n\n" +
            "• Посмотреть или оформить — включите WinScreen галочкой, потом выключите обратно.\n" +
            "• Кнопки уже подключены: «Заново» и «Дальше».\n" +
            "• Победа — когда собраны все монеты. Для финиша на флажке: в WinScreen2D выберите «По команде», " +
            "а на флажке TriggerEvent2D → On Player Enter → WinScreen2D.Win.",
            "Отлично");
    }

    // ============================== Стандартные элементы ==============================

    private static TMP_Text CreateText(TMP_DefaultControls.Resources resources, Transform parent, string name,
                                       string text, Vector2 position, Vector2 size)
    {
        GameObject textObject = TMP_DefaultControls.CreateText(resources);
        textObject.name = name;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        return label;
    }

    private static Button CreateButton(TMP_DefaultControls.Resources resources, Transform parent, string name,
                                       string text, Vector2 position)
    {
        GameObject buttonObject = TMP_DefaultControls.CreateButton(resources);
        buttonObject.name = name;
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<RectTransform>().anchoredPosition = position;

        TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;

        return buttonObject.GetComponent<Button>();
    }

    // Стандартные картинки интерфейса Unity — те же, что использует меню GameObject → UI.
    private static DefaultControls.Resources UIResources()
    {
        return new DefaultControls.Resources
        {
            standard = BuiltinSprite("UI/Skin/UISprite.psd"),
            background = BuiltinSprite("UI/Skin/Background.psd"),
            inputField = BuiltinSprite("UI/Skin/InputFieldBackground.psd"),
            knob = BuiltinSprite("UI/Skin/Knob.psd"),
            checkmark = BuiltinSprite("UI/Skin/Checkmark.psd"),
            dropdown = BuiltinSprite("UI/Skin/DropdownArrow.psd"),
            mask = BuiltinSprite("UI/Skin/UIMask.psd")
        };
    }

    private static TMP_DefaultControls.Resources TMPResources()
    {
        return new TMP_DefaultControls.Resources
        {
            standard = BuiltinSprite("UI/Skin/UISprite.psd"),
            background = BuiltinSprite("UI/Skin/Background.psd"),
            inputField = BuiltinSprite("UI/Skin/InputFieldBackground.psd"),
            knob = BuiltinSprite("UI/Skin/Knob.psd"),
            checkmark = BuiltinSprite("UI/Skin/Checkmark.psd"),
            dropdown = BuiltinSprite("UI/Skin/DropdownArrow.psd"),
            mask = BuiltinSprite("UI/Skin/UIMask.psd")
        };
    }

    private static Sprite BuiltinSprite(string path)
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>(path);
    }

    // ============================== Canvas и EventSystem ==============================

    private static Canvas GetOrCreateCanvas()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas != null) return canvas;

        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(canvasObject, "KT: Canvas");
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        // Без EventSystem кнопки не нажимаются. Модуль — под новый Input System.
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystem, "KT: EventSystem");
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (layer < 0) return;
        target.layer = layer;
        foreach (Transform child in target.transform) SetLayerRecursively(child.gameObject, layer);
    }
}
