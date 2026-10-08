using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Справочник скриптов КТ: меню KT → Справочник скриптов.
/// Слева — все скрипты по разделам, справа — за что отвечает выбранный.
/// </summary>
public class KTScriptGuide : EditorWindow
{
    private string selected;
    private string search = "";
    private Vector2 listScroll;
    private Vector2 cardScroll;

    [MenuItem("KT/Справочник скриптов", priority = 0)]
    private static void OpenFromMenu()
    {
        Open(null);
    }

    public static void Open(string typeName)
    {
        KTScriptGuide window = GetWindow<KTScriptGuide>("Справочник скриптов");
        window.minSize = new Vector2(640, 420);
        if (!string.IsNullOrEmpty(typeName)) window.selected = typeName;
        window.Show();
    }

    private void OnSelectionChange() => Repaint();

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Здесь написано, за что отвечает каждый скрипт КТ. Выберите скрипт слева. " +
            "Короткое описание видно и вверху инспектора у любого скрипта КТ.",
            MessageType.Info);

        search = EditorGUILayout.TextField("Поиск", search);

        EditorGUILayout.BeginHorizontal();
        DrawList();
        DrawSelectedCard();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(230));
        listScroll = EditorGUILayout.BeginScrollView(listScroll);

        string currentGroup = null;
        foreach (KTScriptCard card in KTScriptHelp.Cards)
        {
            if (!Matches(card)) continue;

            if (card.Group != currentGroup)
            {
                currentGroup = card.Group;
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(currentGroup, EditorStyles.boldLabel);
            }

            string label = card.IsScript ? card.Type : card.Title;
            bool isSelected = selected == card.Type;
            if (GUILayout.Toggle(isSelected, label, "Button") && !isSelected)
            {
                selected = card.Type;
                cardScroll = Vector2.zero;
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSelectedCard()
    {
        EditorGUILayout.BeginVertical();
        cardScroll = EditorGUILayout.BeginScrollView(cardScroll);

        KTScriptCard card = string.IsNullOrEmpty(selected) ? null : KTScriptHelp.Find(selected);
        if (card == null)
        {
            EditorGUILayout.LabelField("Выберите скрипт слева.", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField(card.IsScript ? card.Type : card.Title, EditorStyles.largeLabel);
        if (card.IsScript) EditorGUILayout.LabelField(card.Title, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Вариант: " + card.Variant, EditorStyles.miniLabel);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(card.Summary, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(6);

        KTScriptHelp.DrawDetails(card);

        if (card.IsScript) DrawScriptButtons(card);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private static void DrawScriptButtons(KTScriptCard card)
    {
        Type scriptType = FindType(card.Type);
        if (scriptType == null) return;

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();

        GameObject target = Selection.activeGameObject;
        bool canAdd = target != null && target.scene.IsValid() && target.GetComponent(scriptType) == null;
        using (new EditorGUI.DisabledScope(!canAdd))
        {
            string addLabel = target != null ? $"Добавить на «{target.name}»" : "Добавить на выделенный объект";
            if (GUILayout.Button(addLabel)) Undo.AddComponent(target, scriptType);
        }

        if (GUILayout.Button("Найти на сцене"))
        {
            Object[] found = Object.FindObjectsByType(scriptType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            List<Object> objects = new List<Object>();
            foreach (Object item in found) objects.Add(((Component)item).gameObject);
            Selection.objects = objects.ToArray();
            if (objects.Count == 0) EditorUtility.DisplayDialog("Не найдено", $"На сцене нет объектов со скриптом {card.Type}.", "Понятно");
        }

        if (GUILayout.Button("Открыть код"))
        {
            MonoScript script = FindScript(card.Type);
            if (script != null) AssetDatabase.OpenAsset(script);
        }

        EditorGUILayout.EndHorizontal();
    }

    private bool Matches(KTScriptCard card)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;
        string query = search.Trim();
        return Contains(card.Type, query) || Contains(card.Title, query) || Contains(card.Summary, query);
    }

    private static bool Contains(string text, string query)
    {
        return !string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Type FindType(string typeName)
    {
        foreach (Type type in TypeCache.GetTypesDerivedFrom<KTBehaviour>())
        {
            if (type.Name == typeName) return type;
        }
        return null;
    }

    private static MonoScript FindScript(string typeName)
    {
        foreach (string guid in AssetDatabase.FindAssets(typeName + " t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == typeName)
                return AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        }
        return null;
    }
}
