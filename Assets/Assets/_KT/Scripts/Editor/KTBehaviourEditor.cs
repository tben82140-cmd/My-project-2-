using UnityEditor;
using UnityEngine;

/// <summary>
/// Карточка «что это за скрипт» вверху инспектора у каждого скрипта КТ.
/// Короткое описание видно всегда, подробности — по стрелке «Подробнее».
/// </summary>
[CustomEditor(typeof(KTBehaviour), true)]
[CanEditMultipleObjects]
public class KTBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        KTScriptCard card = KTScriptHelp.Find(target.GetType().Name);
        if (card != null) DrawCard(card);

        DrawDefaultInspector();
    }

    private static void DrawCard(KTScriptCard card)
    {
        string prefsKey = "KT.Help.Open." + card.Type;
        bool open = EditorPrefs.GetBool(prefsKey, false);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(card.Title, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Вариант: " + card.Variant, EditorStyles.miniLabel, GUILayout.Width(90));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField(card.Summary, EditorStyles.wordWrappedLabel);

        bool newOpen = EditorGUILayout.Foldout(open, "Подробнее", true);
        if (newOpen != open) EditorPrefs.SetBool(prefsKey, newOpen);

        if (newOpen)
        {
            EditorGUI.indentLevel++;
            KTScriptHelp.DrawDetails(card);
            EditorGUI.indentLevel--;

            if (GUILayout.Button("Открыть справочник всех скриптов", EditorStyles.miniButton))
            {
                KTScriptGuide.Open(card.Type);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }
}
