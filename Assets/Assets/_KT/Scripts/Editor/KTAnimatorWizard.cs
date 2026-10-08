using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Мастер аниматора: меню KT → Мастер аниматора.
/// Берёт готовые клипы (или кадры, из которых сам сделает клипы) и собирает Animator Controller
/// со всеми состояниями, переходами и параметрами. В окне Animator руками ничего соединять не нужно.
///   Игрок: подключает PlayerAnimator2D, а если включён удар — ещё и PlayerAttack2D.
///   Враг:  подключает EnemyAnimator2D; удар играет, когда враг задевает игрока (Hazard2D),
///          урон и смерть — когда его бьют (Damageable2D).
/// </summary>
public class KTAnimatorWizard : EditorWindow
{
    private enum GameView
    {
        Platformer,
        TopDown
    }

    private enum TargetKind
    {
        Player,
        Enemy
    }

    private enum DirectionMode
    {
        One,
        Three,
        Four
    }

    [Serializable]
    private class AnimSource
    {
        public string key;
        public AnimationClip clip;
        public Sprite[] frames = new Sprite[0];
    }

    private struct ActionInfo
    {
        public string id;
        public string label;
        public bool directional;
        public bool loop;
    }

    private static readonly string[] ViewLabels =
    {
        "Платформер (вид сбоку)",
        "Вид сверху"
    };

    private static readonly string[] TargetLabels =
    {
        "Игрок",
        "Враг"
    };

    private static readonly string[] DirectionLabels =
    {
        "Одно: только вбок (влево — зеркально)",
        "Три: вниз, вверх, вбок (влево — зеркально)",
        "Четыре: вниз, вверх, влево, вправо"
    };

    [SerializeField] private GameObject player;
    [SerializeField] private TargetKind targetKind = TargetKind.Player;
    [SerializeField] private bool hasDie;
    [SerializeField] private GameView view = GameView.Platformer;
    [SerializeField] private DirectionMode directions = DirectionMode.Three;
    [SerializeField] private float framesPerSecond = 10f;
    [SerializeField] private bool sortFramesByName = true;
    [SerializeField] private bool hasAttack;
    [SerializeField] private bool hasHurt;
    [SerializeField] private List<AnimSource> sources = new List<AnimSource>();

    private SerializedObject serializedWindow;
    private Vector2 scroll;

    private bool IsTopDown => view == GameView.TopDown;
    private bool IsEnemy => targetKind == TargetKind.Enemy;

    [MenuItem("KT/Мастер аниматора (игрок и враги)", priority = 1)]
    private static void Open()
    {
        KTAnimatorWizard window = GetWindow<KTAnimatorWizard>("Мастер аниматора");
        window.minSize = new Vector2(400, 500);
    }

    private void OnEnable()
    {
        serializedWindow = new SerializedObject(this);
        if (player == null && Selection.activeGameObject != null && Selection.activeGameObject.scene.IsValid())
        {
            player = Selection.activeGameObject;
        }
    }

    // ============================== Окно ==============================

    private void OnGUI()
    {
        serializedWindow.Update();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "1. Выберите, для кого аниматор, и перетащите персонажа со сцены в поле «Персонаж».\n" +
            "2. Выберите вид игры и, для вида сверху, сколько направлений нарисовано.\n" +
            "3. Отметьте галочками, есть ли удар и анимация получения урона.\n" +
            "4. Для каждой анимации перетащите ГОТОВЫЙ КЛИП или кадры (выделите кадры в Project и бросьте на заголовок «…или кадры»).\n" +
            "5. Нажмите «Создать аниматор».\n\n" +
            "Обязательна только анимация «Стоит». Пустые направления заполнятся соседними.",
            MessageType.Info);

        SerializedProperty targetProperty = serializedWindow.FindProperty("targetKind");
        targetProperty.enumValueIndex = EditorGUILayout.Popup("Для кого", targetProperty.enumValueIndex, TargetLabels);
        EditorGUILayout.PropertyField(serializedWindow.FindProperty("player"), new GUIContent("Персонаж"));

        SerializedProperty viewProperty = serializedWindow.FindProperty("view");
        viewProperty.enumValueIndex = EditorGUILayout.Popup("Вид игры", viewProperty.enumValueIndex, ViewLabels);

        if (viewProperty.enumValueIndex == (int)GameView.TopDown)
        {
            SerializedProperty directionsProperty = serializedWindow.FindProperty("directions");
            directionsProperty.enumValueIndex = EditorGUILayout.Popup("Направления", directionsProperty.enumValueIndex, DirectionLabels);
        }

        EditorGUILayout.PropertyField(serializedWindow.FindProperty("framesPerSecond"), new GUIContent("Кадров в секунду (для кадров)"));
        EditorGUILayout.PropertyField(serializedWindow.FindProperty("sortFramesByName"), new GUIContent("Сортировать кадры по имени"));
        bool enemySelected = targetProperty.enumValueIndex == (int)TargetKind.Enemy;
        EditorGUILayout.PropertyField(serializedWindow.FindProperty("hasAttack"),
            new GUIContent(enemySelected ? "Есть удар (когда задевает игрока)" : "Есть удар"));
        EditorGUILayout.PropertyField(serializedWindow.FindProperty("hasHurt"), new GUIContent("Есть анимация получения урона"));
        if (enemySelected)
        {
            EditorGUILayout.PropertyField(serializedWindow.FindProperty("hasDie"), new GUIContent("Есть анимация смерти"));
        }

        serializedWindow.ApplyModifiedProperties();

        // Список анимаций зависит от настроек выше, поэтому строим его после них.
        EnsureSources();
        serializedWindow.Update();
        DrawSources();
        serializedWindow.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        string problem = FindProblem();
        if (problem != null) EditorGUILayout.HelpBox(problem, MessageType.Warning);

        using (new EditorGUI.DisabledScope(problem != null))
        {
            if (GUILayout.Button("Создать аниматор", GUILayout.Height(36)))
            {
                Build();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSources()
    {
        SerializedProperty list = serializedWindow.FindProperty("sources");

        foreach (ActionInfo action in GetActions())
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(action.label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            foreach (string direction in GetDirections(action))
            {
                int index = FindIndex(Key(action, direction));
                if (index < 0) continue;

                if (direction.Length > 0)
                {
                    EditorGUILayout.LabelField(DirectionLabel(direction), EditorStyles.miniBoldLabel);
                }

                SerializedProperty source = list.GetArrayElementAtIndex(index);
                EditorGUILayout.PropertyField(source.FindPropertyRelative("clip"), new GUIContent("Готовый клип"));
                EditorGUILayout.PropertyField(source.FindPropertyRelative("frames"), new GUIContent("…или кадры"), true);
            }

            EditorGUI.indentLevel--;
        }
    }

    private string FindProblem()
    {
        if (player == null) return "Не выбран персонаж.";
        if (!player.scene.IsValid()) return "Перетащите персонажа со СЦЕНЫ (из окна Hierarchy), а не префаб из Project.";
        if (player.GetComponentInChildren<SpriteRenderer>() == null) return "У персонажа нет Sprite Renderer.";
        if (framesPerSecond <= 0f) return "Кадров в секунду должно быть больше нуля.";

        foreach (ActionInfo action in GetActions())
        {
            if (action.id == "Idle" && !ActionHasContent(action))
                return "Добавьте клип или кадры для «Стоит» (хотя бы одно направление).";
            if (action.id == "Attack" && !ActionHasContent(action))
                return "Включён удар, но нет ни клипа, ни кадров удара.";
            if (action.id == "Hurt" && !ActionHasContent(action))
                return "Включена анимация урона, но нет ни клипа, ни кадров.";
            if (action.id == "Die" && !ActionHasContent(action))
                return "Включена анимация смерти, но нет ни клипа, ни кадров.";
        }
        return null;
    }

    // ============================== Что рисуем ==============================

    private List<ActionInfo> GetActions()
    {
        bool platformer = !IsTopDown;
        List<ActionInfo> actions = new List<ActionInfo>
        {
            new ActionInfo { id = "Idle", label = "Стоит", directional = true, loop = true },
            new ActionInfo { id = "Move", label = platformer ? "Бежит" : "Идёт", directional = true, loop = true }
        };

        if (platformer && !IsEnemy)
        {
            actions.Add(new ActionInfo { id = "Jump", label = "Прыгает (летит вверх)", directional = false, loop = false });
            actions.Add(new ActionInfo { id = "Fall", label = "Падает (летит вниз)", directional = false, loop = true });
        }
        if (hasAttack)
        {
            actions.Add(new ActionInfo { id = "Attack", label = "Удар", directional = true, loop = false });
        }
        if (hasHurt)
        {
            actions.Add(new ActionInfo { id = "Hurt", label = "Получил урон", directional = false, loop = false });
        }
        if (IsEnemy && hasDie)
        {
            actions.Add(new ActionInfo { id = "Die", label = "Смерть", directional = false, loop = false });
        }
        return actions;
    }

    private string[] GetDirections(ActionInfo action)
    {
        if (!action.directional || !IsTopDown || directions == DirectionMode.One) return new[] { "" };
        if (directions == DirectionMode.Three) return new[] { "Down", "Up", "Side" };
        return new[] { "Down", "Up", "Left", "Right" };
    }

    private static string DirectionLabel(string direction)
    {
        switch (direction)
        {
            case "Down": return "Вниз (к игроку)";
            case "Up": return "Вверх (спиной)";
            case "Side": return "Вбок (нарисован смотрящим вправо)";
            case "Left": return "Влево";
            case "Right": return "Вправо";
            default: return "";
        }
    }

    private static string Key(ActionInfo action, string direction)
    {
        return direction.Length == 0 ? action.id : action.id + "_" + direction;
    }

    private void EnsureSources()
    {
        foreach (ActionInfo action in GetActions())
        {
            foreach (string direction in GetDirections(action))
            {
                string key = Key(action, direction);
                if (FindIndex(key) < 0) sources.Add(new AnimSource { key = key });
            }
        }
    }

    private int FindIndex(string key)
    {
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].key == key) return i;
        }
        return -1;
    }

    private AnimSource FindSource(string key)
    {
        int index = FindIndex(key);
        return index >= 0 ? sources[index] : null;
    }

    private bool ActionHasContent(ActionInfo action)
    {
        foreach (string direction in GetDirections(action))
        {
            if (HasContent(FindSource(Key(action, direction)))) return true;
        }
        return false;
    }

    private static bool HasContent(AnimSource source)
    {
        return source != null && (source.clip != null || Clean(source.frames).Length > 0);
    }

    // ============================== Сборка ==============================

    private void Build()
    {
        SpriteRenderer spriteRenderer = player.GetComponentInChildren<SpriteRenderer>();

        // Animator должен стоять на спрайте или на одном из его родителей.
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator == null || !spriteRenderer.transform.IsChildOf(animator.transform))
        {
            animator = player.GetComponent<Animator>();
            if (animator == null) animator = Undo.AddComponent<Animator>(player);
        }

        string spritePath = AnimationUtility.CalculateTransformPath(spriteRenderer.transform, animator.transform);
        string safeName = MakeSafeName(player.name);
        string folder = EnsureFolder("Assets/_KT/Generated/" + safeName);

        string controllerPath = folder + "/" + safeName + "_Animator.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AddParameters(controller);

        // Готовим «движения» (клип или дерево направлений) для каждого действия.
        Dictionary<string, Motion> motions = new Dictionary<string, Motion>();
        Dictionary<string, float> lengths = new Dictionary<string, float>();
        foreach (ActionInfo action in GetActions())
        {
            Motion motion = BuildMotion(controller, action, folder, safeName, spritePath, out float length);
            if (motion == null) continue;
            motions[action.id] = motion;
            lengths[action.id] = length;
        }

        BuildStateMachine(controller, motions);

        // Подключаем к игроку.
        Undo.RecordObject(animator, "KT: аниматор игрока");
        animator.runtimeAnimatorController = controller;

        Component driver;
        if (IsEnemy)
        {
            driver = player.GetComponent<EnemyAnimator2D>();
            if (driver == null) driver = Undo.AddComponent<EnemyAnimator2D>(player);
        }
        else
        {
            driver = player.GetComponent<PlayerAnimator2D>();
            if (driver == null) driver = Undo.AddComponent<PlayerAnimator2D>(player);
        }

        SerializedObject driverObject = new SerializedObject(driver);
        driverObject.FindProperty("animator").objectReferenceValue = animator;
        driverObject.FindProperty("spriteToFlip").objectReferenceValue = spriteRenderer;
        driverObject.FindProperty("flipSprite").boolValue = !(IsTopDown && directions == DirectionMode.Four);
        driverObject.ApplyModifiedProperties();

        string attackNote = "";
        if (IsEnemy)
        {
            attackNote = BuildEnemyNotes(motions, lengths);
        }
        else if (motions.ContainsKey("Attack"))
        {
            PlayerAttack2D attack = player.GetComponent<PlayerAttack2D>();
            bool added = attack == null;
            if (added) attack = Undo.AddComponent<PlayerAttack2D>(player);

            SerializedObject attackObject = new SerializedObject(attack);
            if (lengths["Attack"] > 0.05f) attackObject.FindProperty("attackCooldown").floatValue = lengths["Attack"];
            if (added) attackObject.FindProperty("movementWhileAttacking").enumValueIndex = (int)PlayerAttack2D.MoveWhileAttacking.Stop;
            attackObject.ApplyModifiedProperties();

            attackNote = "\n\nУдар: J или левая кнопка мыши. Скрипт PlayerAttack2D " +
                         (added ? "добавлен на игрока." : "уже был на игроке.") +
                         " Бьёт всё, на чём есть Damageable2D.";
        }

        EditorUtility.SetDirty(player);
        AssetDatabase.SaveAssets();
        EditorGUIUtility.PingObject(controller);

        EditorUtility.DisplayDialog("Готово",
            "Аниматор создан и подключён к «" + player.name + "».\n" +
            "Состояния: " + string.Join(", ", motions.Keys) + ".\n" +
            "Файлы лежат в " + folder + "." + attackNote +
            "\n\nНажмите Play и проверьте.",
            "Отлично");
    }

    private string BuildEnemyNotes(Dictionary<string, Motion> motions, Dictionary<string, float> lengths)
    {
        List<string> notes = new List<string>();

        if (motions.ContainsKey("Attack") && player.GetComponentInChildren<Hazard2D>() == null)
            notes.Add("Удар играет, когда враг задевает игрока, — добавьте врагу Hazard2D.");

        if ((motions.ContainsKey("Hurt") || motions.ContainsKey("Die")) && player.GetComponent<Damageable2D>() == null)
            notes.Add("Урон и смерть играют, когда врага бьют, — добавьте врагу Damageable2D.");

        Damageable2D damageable = player.GetComponent<Damageable2D>();
        if (damageable != null && lengths.TryGetValue("Die", out float dieLength))
        {
            SerializedObject damageableObject = new SerializedObject(damageable);
            damageableObject.FindProperty("deathAnimationTime").floatValue = dieLength;
            damageableObject.ApplyModifiedProperties();
            notes.Add($"В Damageable2D подставлено время анимации смерти: {dieLength:0.##} с.");
        }

        return notes.Count > 0 ? "\n\n" + string.Join("\n", notes) : "";
    }

    private void AddParameters(AnimatorController controller)
    {
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        if (hasAttack) controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        if (hasHurt) controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
        if (IsEnemy) controller.AddParameter("IsChasing", AnimatorControllerParameterType.Bool);
        if (IsEnemy && hasDie) controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        // Значения на старте: стоит на земле, смотрит вниз (вид сверху) или вправо (платформер).
        AnimatorControllerParameter[] parameters = controller.parameters;
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            if (parameter.name == "IsGrounded") parameter.defaultBool = true;
            if (parameter.name == "MoveX") parameter.defaultFloat = IsTopDown ? 0f : 1f;
            if (parameter.name == "MoveY") parameter.defaultFloat = IsTopDown ? -1f : 0f;
        }
        controller.parameters = parameters;
    }

    private void BuildStateMachine(AnimatorController controller, Dictionary<string, Motion> motions)
    {
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        bool platformer = !IsTopDown;

        AnimatorState idle = machine.AddState("Idle", new Vector3(300, 0, 0));
        idle.motion = motions["Idle"];
        machine.defaultState = idle;

        List<AnimatorState> groundStates = new List<AnimatorState> { idle };

        if (motions.TryGetValue("Move", out Motion moveMotion))
        {
            AnimatorState move = machine.AddState(platformer ? "Run" : "Walk", new Vector3(300, 120, 0));
            move.motion = moveMotion;
            groundStates.Add(move);

            AnimatorStateTransition toMove = Instant(idle.AddTransition(move));
            toMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            if (platformer) toMove.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");

            Instant(move.AddTransition(idle)).AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        // Прыжок и падение (только платформер). Если есть только одно из них — используем его для обоих.
        motions.TryGetValue("Jump", out Motion jumpMotion);
        motions.TryGetValue("Fall", out Motion fallMotion);
        if (platformer && (jumpMotion != null || fallMotion != null))
        {
            AnimatorState jump = machine.AddState("Jump", new Vector3(600, 0, 0));
            jump.motion = jumpMotion != null ? jumpMotion : fallMotion;
            AnimatorState fall = machine.AddState("Fall", new Vector3(600, 120, 0));
            fall.motion = fallMotion != null ? fallMotion : jumpMotion;

            foreach (AnimatorState ground in groundStates)
            {
                AnimatorStateTransition toJump = Instant(ground.AddTransition(jump));
                toJump.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                toJump.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VerticalSpeed");

                AnimatorStateTransition toFall = Instant(ground.AddTransition(fall));
                toFall.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
                toFall.AddCondition(AnimatorConditionMode.Less, -0.1f, "VerticalSpeed");
            }

            Instant(jump.AddTransition(fall)).AddCondition(AnimatorConditionMode.Less, -0.1f, "VerticalSpeed");

            AnimatorStateTransition bounce = Instant(fall.AddTransition(jump));
            bounce.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
            bounce.AddCondition(AnimatorConditionMode.Greater, 0.1f, "VerticalSpeed");

            Instant(jump.AddTransition(idle)).AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            Instant(fall.AddTransition(idle)).AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
        }

        // Удар и урон запускаются триггерами из любого состояния и по окончании клипа возвращаются в Idle.
        // Урон добавляем первым — он важнее удара.
        if (motions.TryGetValue("Hurt", out Motion hurtMotion))
        {
            AnimatorState hurt = machine.AddState("Hurt", new Vector3(0, 240, 0));
            hurt.motion = hurtMotion;
            AnimatorStateTransition toHurt = Instant(machine.AddAnyStateTransition(hurt));
            toHurt.canTransitionToSelf = true;
            toHurt.AddCondition(AnimatorConditionMode.If, 0f, "Hurt");
            AfterClip(hurt.AddTransition(idle));
        }

        if (motions.TryGetValue("Attack", out Motion attackMotion))
        {
            AnimatorState attack = machine.AddState("Attack", new Vector3(300, 240, 0));
            attack.motion = attackMotion;
            AnimatorStateTransition toAttack = Instant(machine.AddAnyStateTransition(attack));
            toAttack.canTransitionToSelf = true;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            AfterClip(attack.AddTransition(idle));
        }

        // Смерть: из любого состояния, обратно не возвращается — враг исчезнет после клипа.
        if (motions.TryGetValue("Die", out Motion dieMotion))
        {
            AnimatorState die = machine.AddState("Die", new Vector3(600, 240, 0));
            die.motion = dieMotion;
            AnimatorStateTransition toDie = Instant(machine.AddAnyStateTransition(die));
            toDie.canTransitionToSelf = false;
            toDie.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        }
    }

    /// <summary>
    /// Одно действие → либо просто клип, либо «дерево направлений» (Blend Tree),
    /// которое по MoveX/MoveY выбирает клип вниз/вверх/вбок.
    /// </summary>
    private Motion BuildMotion(AnimatorController controller, ActionInfo action, string folder,
                               string safeName, string spritePath, out float length)
    {
        length = 0f;
        string[] actionDirections = GetDirections(action);

        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        foreach (string direction in actionDirections)
        {
            AnimationClip clip = GetOrCreateClip(Key(action, direction), action.loop, folder, safeName, spritePath);
            if (clip == null) continue;
            clips[direction] = clip;
            length = Mathf.Max(length, clip.length);
        }

        if (clips.Count == 0) return null;
        if (actionDirections.Length == 1) return clips[actionDirections[0]];

        BlendTree tree = new BlendTree
        {
            name = action.id,
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            hideFlags = HideFlags.HideInHierarchy
        };
        AssetDatabase.AddObjectToAsset(tree, controller);

        tree.AddChild(Pick("Down", clips), new Vector2(0f, -1f));
        tree.AddChild(Pick("Up", clips), new Vector2(0f, 1f));

        if (directions == DirectionMode.Three)
        {
            // «Вбок» стоит и справа, и слева: влево его зеркалит PlayerAnimator2D.
            AnimationClip side = Pick("Side", clips);
            tree.AddChild(side, new Vector2(1f, 0f));
            tree.AddChild(side, new Vector2(-1f, 0f));
        }
        else
        {
            tree.AddChild(Pick("Left", clips), new Vector2(-1f, 0f));
            tree.AddChild(Pick("Right", clips), new Vector2(1f, 0f));
        }

        return tree;
    }

    /// <summary>Клип для направления. Если его нет — берём самый похожий из имеющихся.</summary>
    private static AnimationClip Pick(string direction, Dictionary<string, AnimationClip> clips)
    {
        if (clips.TryGetValue(direction, out AnimationClip exact)) return exact;

        string[] fallbacks;
        switch (direction)
        {
            case "Down": fallbacks = new[] { "Side", "Right", "Left", "Up" }; break;
            case "Up": fallbacks = new[] { "Down", "Side", "Right", "Left" }; break;
            case "Side": fallbacks = new[] { "Down", "Right", "Left", "Up" }; break;
            case "Left": fallbacks = new[] { "Right", "Side", "Down", "Up" }; break;
            default: fallbacks = new[] { "Left", "Side", "Down", "Up" }; break;
        }

        foreach (string fallback in fallbacks)
        {
            if (clips.TryGetValue(fallback, out AnimationClip clip)) return clip;
        }

        foreach (AnimationClip any in clips.Values) return any;
        return null;
    }

    private AnimationClip GetOrCreateClip(string key, bool loop, string folder, string safeName, string spritePath)
    {
        AnimSource source = FindSource(key);
        if (source == null) return null;
        if (source.clip != null) return source.clip; // готовый клип студента используем как есть

        Sprite[] frames = Clean(source.frames);
        if (frames.Length == 0) return null;

        if (sortFramesByName)
        {
            Array.Sort(frames, (a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        }

        AnimationClip clip = new AnimationClip { frameRate = framesPerSecond };
        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(spritePath, typeof(SpriteRenderer), "m_Sprite");

        // Последний кадр повторяем в конце, чтобы он показывался столько же, сколько остальные.
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i / framesPerSecond, value = frames[i] };
        }
        keys[frames.Length] = new ObjectReferenceKeyframe
        {
            time = frames.Length / framesPerSecond,
            value = frames[frames.Length - 1]
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, folder + "/" + safeName + "_" + key + ".anim");
        return clip;
    }

    // ============================== Помощники ==============================

    private static AnimatorStateTransition Instant(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.exitTime = 0f;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        return transition;
    }

    private static AnimatorStateTransition AfterClip(AnimatorStateTransition transition)
    {
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        return transition;
    }

    private static Sprite[] Clean(Sprite[] frames)
    {
        List<Sprite> result = new List<Sprite>();
        if (frames == null) return result.ToArray();
        foreach (Sprite frame in frames)
        {
            if (frame != null) result.Add(frame);
        }
        return result.ToArray();
    }

    private static string MakeSafeName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }
        return string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
    }

    private static string EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
        return current;
    }
}
