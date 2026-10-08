using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Уровни высоты (холмы и лестницы при виде сверху): меню KT → Уровни высоты.
///
/// 1. «Подготовить проект» — создаёт Layers и Sorting Layers Level1–Level3 и запрещает уровням сталкиваться.
/// 2. «Построить коллизии и лестницы» — по нарисованному холму сам строит стены для игрока внизу и наверху
///    и накрывает каждую лестницу зоной смены уровня (ElevationTrigger2D).
/// 3. Кнопки «Уровень 1/2/3» — переносят выделенные объекты (игрока, монеты, врагов) на нужный уровень.
/// </summary>
public class KTHeightLevels : EditorWindow
{
    private const int MaxLevel = 3;
    private const string ArtFolder = "Assets/_KT/Art/Collision";
    private const string CollisionSpritePath = ArtFolder + "/Collision_Full.png";
    private const string CollisionTilePath = ArtFolder + "/CollisionTile.asset";
    private const string CollisionPrefix = "Collision_";
    private const string StairsPrefix = "Stairs_";
    private const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    private static readonly string[] PairLabels =
    {
        "Внизу уровень 1, наверху 2",
        "Внизу уровень 2, наверху 3"
    };

    private static readonly Vector3Int[] FourDirections =
    {
        Vector3Int.up, Vector3Int.down, Vector3Int.right, Vector3Int.left
    };

    [SerializeField] private Tilemap hillTop;
    [SerializeField] private Tilemap hillCliffs;
    [SerializeField] private Tilemap hillStairs;
    [SerializeField] private int pairIndex;
    [SerializeField] private bool setupSorting = true;
    [SerializeField] private float stairsExtendDown = 0.5f;

    private Vector2 scroll;
    private readonly List<string> diagnostics = new List<string>();

    [MenuItem("KT/Уровни высоты (холмы и лестницы)", priority = 3)]
    private static void Open()
    {
        KTHeightLevels window = GetWindow<KTHeightLevels>("Уровни высоты");
        window.minSize = new Vector2(400, 560);
    }

    private static string LevelName(int level) => "Level" + level;

    private void OnSelectionChange() => Repaint();
    private void OnFocus() => Repaint();

    // ============================== Окно ==============================

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        // ---------- 1 ----------
        EditorGUILayout.LabelField("1. Подготовить проект (один раз)", EditorStyles.boldLabel);
        List<string> missing = FindMissingProjectSettings();
        bool projectReady = missing.Count == 0;

        if (projectReady)
        {
            EditorGUILayout.HelpBox("Проект готов: есть уровни Level1–Level3, и они не сталкиваются друг с другом.", MessageType.Info);

            int unlitLights = CountLightsMissingLevels();
            if (unlitLights > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Свет на сцене ({unlitLights} шт.) не освещает слои Level1–Level3 — всё на них будет ЧЁРНЫМ. Нажмите кнопку.",
                    MessageType.Error);
                if (GUILayout.Button("Починить свет", GUILayout.Height(28))) FixLights();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Не хватает:\n• " + string.Join("\n• ", missing), MessageType.Warning);
            if (GUILayout.Button("Подготовить проект", GUILayout.Height(30))) PrepareProject();
        }

        // ---------- 2 ----------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("2. Холм", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Нарисуйте холм на трёх отдельных Tilemap: верх (по нему ходят), обрыв (стенка спереди) и лестницы. " +
            "Перетащите их сюда и нажмите кнопку. Можно нарисовать сразу несколько холмов — обработаются все.\n\n" +
            "Перерисовали холм — просто нажмите кнопку ещё раз.",
            MessageType.Info);

        hillTop = (Tilemap)EditorGUILayout.ObjectField("Верх холма", hillTop, typeof(Tilemap), true);
        hillCliffs = (Tilemap)EditorGUILayout.ObjectField("Обрыв", hillCliffs, typeof(Tilemap), true);
        hillStairs = (Tilemap)EditorGUILayout.ObjectField("Лестницы", hillStairs, typeof(Tilemap), true);
        pairIndex = EditorGUILayout.Popup("Уровни", pairIndex, PairLabels);
        setupSorting = EditorGUILayout.ToggleLeft("Настроить отрисовку (игрок прячется за холмом)", setupSorting);
        stairsExtendDown = EditorGUILayout.Slider(
            new GUIContent("Продлить лестницу вниз, клеток",
                "Насколько зона лестницы выходит за нижнюю ступеньку. Больше — игрок раньше «поднимается» и не прячется головой за холмом."),
            stairsExtendDown, 0f, 2f);

        string hillProblem = FindHillProblem(projectReady);
        if (hillProblem != null) EditorGUILayout.HelpBox(hillProblem, MessageType.Warning);

        using (new EditorGUI.DisabledScope(hillProblem != null))
        {
            if (GUILayout.Button("Построить коллизии и лестницы", GUILayout.Height(34))) BuildHill();
        }

        // ---------- 3 ----------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("3. Кто на каком уровне", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Выделите объекты на сцене и нажмите кнопку уровня, на котором они стоят. Меняются и физика, и отрисовка.\n" +
            "• Игрок, который начинает внизу, — уровень 1.\n" +
            "• Монеты, враги, сундуки, деревья на холме — уровень 2.",
            MessageType.Info);

        GameObject[] selected = Selection.gameObjects;
        EditorGUILayout.LabelField(selected.Length == 0 ? "Ничего не выделено" : "Выделено объектов: " + selected.Length, EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(!projectReady || selected.Length == 0))
        {
            EditorGUILayout.BeginHorizontal();
            for (int level = 1; level <= MaxLevel; level++)
            {
                if (GUILayout.Button("Уровень " + level, GUILayout.Height(26))) MoveSelectionToLevel(level);
            }
            EditorGUILayout.EndHorizontal();
        }

        bool hasObjectsLayer = SortingLayerExists("Objects");
        using (new EditorGUI.DisabledScope(!projectReady || !hasObjectsLayer))
        {
            if (GUILayout.Button("Всё со слоя отрисовки Objects → Level1"))
            {
                MoveSortingLayerContents("Objects", LevelName(1));
            }
        }
        EditorGUILayout.LabelField("Нужно, чтобы игрок на уровне 1 по-прежнему заходил за деревья внизу.", EditorStyles.wordWrappedMiniLabel);

        // ---------- 4 ----------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("4. Видимость стен", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Красные — стены для игрока внизу, синие — для игрока наверху. Перед записью видео скройте их.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Показать стены")) SetCollisionVisible(true);
        if (GUILayout.Button("Скрыть стены")) SetCollisionVisible(false);
        EditorGUILayout.EndHorizontal();

        // ---------- 5 ----------
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("5. Лестница не работает?", EditorStyles.boldLabel);
        if (GUILayout.Button("Проверить лестницы", GUILayout.Height(28))) RunStairsDiagnostics();

        foreach (string line in diagnostics)
        {
            MessageType type = line.StartsWith("✘") ? MessageType.Error
                : line.StartsWith("⚠") ? MessageType.Warning
                : MessageType.Info;
            EditorGUILayout.HelpBox(line, type);
        }

        EditorGUILayout.EndScrollView();
    }

    // ============================== 5. Диагностика ==============================

    private void RunStairsDiagnostics()
    {
        diagnostics.Clear();
        int errorsBefore = 0;

        // Проект
        foreach (string missing in FindMissingProjectSettings())
        {
            diagnostics.Add($"✘ Проект: не хватает — {missing}. Нажмите «Подготовить проект».");
        }

        // Игрок
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            diagnostics.Add("✘ Нет объекта с тегом Player.");
        }
        else
        {
            string playerLayer = LayerMask.LayerToName(player.layer);
            if (!playerLayer.StartsWith("Level"))
                diagnostics.Add($"✘ Игрок стоит на Layer «{playerLayer}», а должен на Level1. Выделите игрока → раздел 3 → «Уровень 1».");

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body != null && body.gravityScale != 0f)
                diagnostics.Add("⚠ У игрока Gravity Scale не 0. Уровни высоты сделаны для вида сверху (вариант Б).");

            bool hasFeet = false;
            foreach (Collider2D col in player.GetComponentsInChildren<Collider2D>())
            {
                if (!col.isTrigger) { hasFeet = true; break; }
            }
            if (!hasFeet) diagnostics.Add("✘ У игрока нет обычного коллайдера (не триггера) — лестница не видит его ноги.");
        }

        // Tilemap холма
        if (hillTop == null) diagnostics.Add("✘ В разделе 2 не указан «Верх холма».");
        if (hillStairs == null) diagnostics.Add("✘ В разделе 2 не указаны «Лестницы» — без этого Tilemap переходов не будет.");

        foreach (Tilemap map in new[] { hillTop, hillCliffs, hillStairs })
        {
            if (map != null && map.GetComponent<TilemapCollider2D>() != null)
                diagnostics.Add($"✘ На Tilemap «{map.name}» висит свой коллайдер — он мешает. Нажмите «Построить коллизии и лестницы», он уберётся.");
        }

        if (hillTop != null && hillStairs != null)
        {
            HashSet<Vector3Int> top = CellsOf(hillTop);
            HashSet<Vector3Int> stairs = CellsOf(hillStairs);
            List<HashSet<Vector3Int>> regions = SplitIntoRegions(stairs);

            if (stairs.Count == 0)
                diagnostics.Add($"✘ На Tilemap «{hillStairs.name}» нет ни одного тайла. Возможно, лестница нарисована на обрыве или на верхе холма — перенесите её.");

            // Лестница должна касаться верха холма.
            for (int i = 0; i < regions.Count; i++)
            {
                bool touchesTop = false;
                foreach (Vector3Int cell in regions[i])
                {
                    foreach (Vector3Int direction in FourDirections)
                    {
                        if (top.Contains(cell + direction)) { touchesTop = true; break; }
                    }
                    if (touchesTop) break;
                }
                if (!touchesTop)
                    diagnostics.Add($"✘ Лестница {i + 1} не касается верха холма — между ними пустая клетка или обрыв. Дорисуйте лестницу вплотную к верху.");
            }

            // Зоны построены?
            string holderName = $"{StairsPrefix}Level{pairIndex + 1}-{pairIndex + 2}";
            GameObject holder = GameObject.Find(holderName);
            int zones = holder != null ? holder.GetComponentsInChildren<ElevationTrigger2D>().Length : 0;
            if (zones == 0 && regions.Count > 0)
                diagnostics.Add("✘ Зоны лестниц не построены. Нажмите «Построить коллизии и лестницы».");
            else if (zones != regions.Count)
                diagnostics.Add($"⚠ Лестниц нарисовано {regions.Count}, а зон {zones}. Холм перерисован? Нажмите «Построить коллизии и лестницы» ещё раз.");

            // Посторонние коллайдеры на лестнице и на клетке выхода с неё.
            Grid grid = hillTop.GetComponentInParent<Grid>();
            if (grid != null) FindForeignColliders(grid, top, stairs, regions);
        }

        // Лишние ElevationTrigger2D, поставленные руками.
        foreach (ElevationTrigger2D trigger in Object.FindObjectsByType<ElevationTrigger2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Transform parent = trigger.transform.parent;
            if (parent == null || !parent.name.StartsWith(StairsPrefix))
                diagnostics.Add($"⚠ «{trigger.name}» — лестничный триггер, поставленный вручную. Если это старая версия, удалите его: окно ставит свои сам.");
        }

        foreach (string line in diagnostics) if (line.StartsWith("✘")) errorsBefore++;
        if (errorsBefore == 0)
        {
            diagnostics.Add("✔ Ошибок в настройке не найдено. Нажмите Play и пройдите по лестнице: в Console при каждой смене уровня " +
                            "появляется строчка «[Лестница] … игрок Level1 → Level2». Если строчек нет или они странные — пришлите их.");
        }
        Repaint();
    }

    private void FindForeignColliders(Grid grid, HashSet<Vector3Int> top, HashSet<Vector3Int> stairs, List<HashSet<Vector3Int>> regions)
    {
        Physics2D.SyncTransforms();
        ContactFilter2D solidOnly = new ContactFilter2D();
        solidOnly.useTriggers = false;
        List<Collider2D> hits = new List<Collider2D>();
        HashSet<Collider2D> reported = new HashSet<Collider2D>();

        // Клетки лестниц + клетки верха, на которые с лестниц выходят.
        HashSet<Vector3Int> cellsToCheck = new HashSet<Vector3Int>(stairs);
        foreach (HashSet<Vector3Int> region in regions)
        {
            foreach (Vector3Int cell in region)
            {
                foreach (Vector3Int direction in FourDirections)
                {
                    if (top.Contains(cell + direction)) cellsToCheck.Add(cell + direction);
                }
            }
        }

        foreach (Vector3Int cell in cellsToCheck)
        {
            hits.Clear();
            Physics2D.OverlapPoint(grid.GetCellCenterWorld(cell), solidOnly, hits);
            foreach (Collider2D hit in hits)
            {
                if (!reported.Add(hit)) continue;
                if (hit.attachedRigidbody != null && hit.attachedRigidbody.CompareTag("Player")) continue;

                string layerName = LayerMask.LayerToName(hit.gameObject.layer);
                if (hit.gameObject.name.StartsWith(CollisionPrefix))
                {
                    diagnostics.Add($"✘ Стена «{hit.gameObject.name}» стоит на лестнице или на выходе с неё. Нажмите «Построить коллизии и лестницы» ещё раз.");
                }
                else
                {
                    diagnostics.Add($"✘ На лестнице или на выходе с неё стоит посторонний коллайдер «{hit.gameObject.name}» (Layer {layerName}). " +
                                    "Он не пускает игрока ни на одном уровне. Уберите его или сотрите его тайлы с лестницы.");
                }
            }
        }
    }

    private string FindHillProblem(bool projectReady)
    {
        if (!projectReady) return "Сначала нажмите «Подготовить проект».";
        if (hillTop == null) return "Перетащите Tilemap с верхом холма.";
        if (hillTop == hillCliffs || hillTop == hillStairs || (hillCliffs != null && hillCliffs == hillStairs))
            return "Верх, обрыв и лестницы должны быть на РАЗНЫХ Tilemap.";

        Grid grid = hillTop.GetComponentInParent<Grid>();
        if (grid == null) return "Tilemap верха холма должен лежать внутри Grid.";
        if (hillCliffs != null && hillCliffs.GetComponentInParent<Grid>() != grid) return "Обрыв должен быть в том же Grid, что и верх холма.";
        if (hillStairs != null && hillStairs.GetComponentInParent<Grid>() != grid) return "Лестницы должны быть в том же Grid, что и верх холма.";
        return null;
    }

    // ============================== 1. Проект ==============================

    private static List<string> FindMissingProjectSettings()
    {
        List<string> missing = new List<string>();

        for (int level = 1; level <= MaxLevel; level++)
        {
            if (LayerMask.NameToLayer(LevelName(level)) < 0) missing.Add($"физический Layer «{LevelName(level)}»");
        }
        for (int level = 1; level <= MaxLevel; level++)
        {
            if (!SortingLayerExists(LevelName(level))) missing.Add($"Sorting Layer «{LevelName(level)}»");
        }

        if (missing.Count == 0)
        {
            for (int a = 1; a <= MaxLevel; a++)
            {
                for (int b = a + 1; b <= MaxLevel; b++)
                {
                    int layerA = LayerMask.NameToLayer(LevelName(a));
                    int layerB = LayerMask.NameToLayer(LevelName(b));
                    if (!Physics2D.GetIgnoreLayerCollision(layerA, layerB))
                    {
                        missing.Add("уровни не должны сталкиваться друг с другом (Layer Collision Matrix)");
                        return missing;
                    }
                }
            }
        }
        return missing;
    }

    private static void PrepareProject()
    {
        Object tagAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        SerializedObject tags = new SerializedObject(tagAsset);

        // Физические Layers.
        SerializedProperty layers = tags.FindProperty("layers");
        for (int level = 1; level <= MaxLevel; level++)
        {
            string name = LevelName(level);
            if (LayerSlotIndex(layers, name) >= 0) continue;

            int freeSlot = -1;
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { freeSlot = i; break; }
            }
            if (freeSlot < 0)
            {
                EditorUtility.DisplayDialog("Нет места", "Все пользовательские Layers заняты. Освободите три слота в Project Settings → Tags and Layers.", "Понятно");
                return;
            }
            layers.GetArrayElementAtIndex(freeSlot).stringValue = name;
        }

        // Sorting Layers (добавляются в конец списка — рисуются поверх остальных).
        SerializedProperty sortingLayers = tags.FindProperty("m_SortingLayers");
        for (int level = 1; level <= MaxLevel; level++)
        {
            string name = LevelName(level);
            if (SortingSlotIndex(sortingLayers, name) >= 0) continue;

            int index = sortingLayers.arraySize;
            sortingLayers.InsertArrayElementAtIndex(index);
            SerializedProperty entry = sortingLayers.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("name").stringValue = name;
            entry.FindPropertyRelative("uniqueID").longValue = NewSortingLayerId(sortingLayers);
            SerializedProperty locked = entry.FindPropertyRelative("locked");
            if (locked != null) locked.intValue = 0;
        }

        tags.ApplyModifiedProperties();

        // Матрица столкновений: каждый уровень сталкивается только сам с собой (и со всем остальным, кроме других уровней).
        // Пишем прямо в файл настроек (чтобы точно сохранилось) и в текущую физику (чтобы работало сразу).
        Object physicsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0];
        SerializedObject physics = new SerializedObject(physicsAsset);
        SerializedProperty matrix = physics.FindProperty("m_LayerCollisionMatrix");

        for (int a = 1; a <= MaxLevel; a++)
        {
            int layerA = LayerMask.NameToLayer(LevelName(a));
            for (int b = 1; b <= MaxLevel; b++)
            {
                int layerB = LayerMask.NameToLayer(LevelName(b));
                bool collide = a == b;

                if (matrix != null && layerA < matrix.arraySize)
                {
                    SerializedProperty row = matrix.GetArrayElementAtIndex(layerA);
                    long bits = row.longValue & 0xFFFFFFFFL;
                    long bit = 1L << layerB;
                    row.longValue = collide ? (bits | bit) : (bits & ~bit);
                }
            }
        }
        physics.ApplyModifiedProperties();

        for (int a = 1; a <= MaxLevel; a++)
        {
            int layerA = LayerMask.NameToLayer(LevelName(a));
            for (int b = 1; b <= MaxLevel; b++)
            {
                Physics2D.IgnoreLayerCollision(layerA, LayerMask.NameToLayer(LevelName(b)), a != b);
            }
        }

        EditorUtility.SetDirty(physicsAsset);
        EditorUtility.SetDirty(tagAsset);
        AssetDatabase.SaveAssets();

        int fixedLights = FixLights();

        EditorUtility.DisplayDialog("Готово",
            "Созданы Layers и Sorting Layers Level1, Level2, Level3.\nУровни больше не сталкиваются друг с другом." +
            (fixedLights > 0 ? $"\nСвет на сцене ({fixedLights} шт.) теперь освещает новые слои." : ""),
            "Отлично");
    }

    private static int LayerSlotIndex(SerializedProperty layers, string name)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
        }
        return -1;
    }

    private static int SortingSlotIndex(SerializedProperty sortingLayers, string name)
    {
        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            if (sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) return i;
        }
        return -1;
    }

    private static long NewSortingLayerId(SerializedProperty sortingLayers)
    {
        HashSet<long> used = new HashSet<long>();
        for (int i = 0; i < sortingLayers.arraySize; i++)
        {
            used.Add(sortingLayers.GetArrayElementAtIndex(i).FindPropertyRelative("uniqueID").longValue);
        }

        long id;
        do { id = Random.Range(1000, int.MaxValue); } while (used.Contains(id));
        return id;
    }

    private static bool SortingLayerExists(string name)
    {
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == name) return true;
        }
        return false;
    }

    // ============================== Свет URP 2D ==============================
    // У каждого 2D-света есть список Target Sorting Layers. Слои, созданные ПОСЛЕ света, в него не попадают,
    // и всё на них рисуется чёрным. Ищем свет по имени типа, чтобы не зависеть от пакета URP.

    private static List<SerializedObject> FindLights()
    {
        List<SerializedObject> lights = new List<SerializedObject>();
        foreach (Behaviour behaviour in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (behaviour == null || behaviour.GetType().Name != "Light2D") continue;
            SerializedObject serialized = new SerializedObject(behaviour);
            if (serialized.FindProperty("m_ApplyToSortingLayers") != null) lights.Add(serialized);
        }
        return lights;
    }

    private static int CountLightsMissingLevels()
    {
        int count = 0;
        foreach (SerializedObject light in FindLights())
        {
            if (MissingLevelIds(light.FindProperty("m_ApplyToSortingLayers")).Count > 0) count++;
        }
        return count;
    }

    private static List<int> MissingLevelIds(SerializedProperty targetLayers)
    {
        List<int> missing = new List<int>();
        for (int level = 1; level <= MaxLevel; level++)
        {
            int id = SortingLayer.NameToID(LevelName(level));
            if (!SortingLayer.IsValid(id)) continue;

            bool found = false;
            for (int i = 0; i < targetLayers.arraySize; i++)
            {
                if (targetLayers.GetArrayElementAtIndex(i).intValue == id) { found = true; break; }
            }
            if (!found) missing.Add(id);
        }
        return missing;
    }

    private static int FixLights()
    {
        int fixedCount = 0;
        foreach (SerializedObject light in FindLights())
        {
            SerializedProperty targetLayers = light.FindProperty("m_ApplyToSortingLayers");
            List<int> missing = MissingLevelIds(targetLayers);
            if (missing.Count == 0) continue;

            foreach (int id in missing)
            {
                int index = targetLayers.arraySize;
                targetLayers.InsertArrayElementAtIndex(index);
                targetLayers.GetArrayElementAtIndex(index).intValue = id;
            }
            light.ApplyModifiedProperties();
            fixedCount++;
        }
        return fixedCount;
    }

    // ============================== 2. Холм ==============================

    private void BuildHill()
    {
        int lower = pairIndex + 1;
        int upper = lower + 1;
        string lowerName = LevelName(lower);
        string upperName = LevelName(upper);
        Grid grid = hillTop.GetComponentInParent<Grid>();

        Tile collisionTile = GetOrCreateCollisionTile();
        if (collisionTile == null) return;

        // Рисованные тайлы холма сами по себе не должны быть стенами — стены строим отдельно.
        bool removedColliders = RemoveColliders(hillTop) | RemoveColliders(hillCliffs) | RemoveColliders(hillStairs);

        HashSet<Vector3Int> top = CellsOf(hillTop);
        HashSet<Vector3Int> cliffs = CellsOf(hillCliffs);
        HashSet<Vector3Int> stairs = CellsOf(hillStairs);

        List<HashSet<Vector3Int>> stairRegions = SplitIntoRegions(stairs);
        List<Vector3Int> stairUps = new List<Vector3Int>();

        foreach (HashSet<Vector3Int> region in stairRegions)
        {
            stairUps.Add(FindUpDirection(region, top));
        }

        // Стены для игрока ВНИЗУ: весь холм (верх + обрыв), кроме лестниц.
        HashSet<Vector3Int> lowerWalls = new HashSet<Vector3Int>(top);
        lowerWalls.UnionWith(cliffs);
        lowerWalls.ExceptWith(stairs);

        // Стены для игрока НАВЕРХУ: кольцо клеток вокруг верха + обрыв, кроме лестниц.
        HashSet<Vector3Int> upperWalls = new HashSet<Vector3Int>();
        foreach (Vector3Int cell in top)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    upperWalls.Add(cell + new Vector3Int(dx, dy, 0));
                }
            }
        }
        upperWalls.UnionWith(cliffs);
        upperWalls.ExceptWith(top);
        upperWalls.ExceptWith(stairs);

        string pairTag = $"{lower}-{upper}";
        Tilemap lowerMap = GetOrCreateCollisionMap(grid, $"{CollisionPrefix}{lowerName}_Hill{pairTag}", lowerName, new Color(1f, 0.25f, 0.25f, 0.45f));
        Tilemap upperMap = GetOrCreateCollisionMap(grid, $"{CollisionPrefix}{upperName}_Hill{pairTag}", upperName, new Color(0.25f, 0.55f, 1f, 0.45f));
        Fill(lowerMap, lowerWalls, collisionTile);
        Fill(upperMap, upperWalls, collisionTile);

        int stairsCount = BuildStairTriggers(grid, stairRegions, stairUps, lowerName, upperName, pairTag, stairsExtendDown, hillTop, upperWalls);

        if (setupSorting)
        {
            SetTilemapSorting(hillTop, upperName, -10);
            SetTilemapSorting(hillCliffs, lowerName, -5);
            SetTilemapSorting(hillStairs, lowerName, -5);
        }

        string stairsWarning = stairsCount == 0
            ? "\n\nЛестниц не найдено — на холм нельзя будет подняться. Нарисуйте лестницу на Tilemap «Лестницы»."
            : "";
        string collidersNote = removedColliders
            ? "\n\nС Tilemap холма убраны старые коллайдеры — теперь стены строятся отдельно."
            : "";

        EditorUtility.DisplayDialog("Готово",
            $"Стены для уровня {lower} (красные): {lowerWalls.Count} клеток.\n" +
            $"Стены для уровня {upper} (синие): {upperWalls.Count} клеток.\n" +
            $"Лестниц: {stairsCount}." + stairsWarning + collidersNote +
            $"\n\nНе забудьте: выделите игрока → раздел 3 → «Уровень {lower}».",
            "Отлично");
    }

    private int BuildStairTriggers(Grid grid, List<HashSet<Vector3Int>> regions, List<Vector3Int> ups,
                                   string lowerName, string upperName, string pairTag, float extendDown, Tilemap top,
                                   HashSet<Vector3Int> upperWalls)
    {
        string holderName = $"{StairsPrefix}Level{pairTag}";
        GameObject oldHolder = GameObject.Find(holderName);
        if (oldHolder != null) Undo.DestroyObjectImmediate(oldHolder);

        if (regions.Count == 0) return 0;

        GameObject holder = new GameObject(holderName);
        Undo.RegisterCreatedObjectUndo(holder, "KT: лестницы");

        for (int i = 0; i < regions.Count; i++)
        {
            CreateStairsZone(holder, $"Лестница {i + 1}", grid, regions[i], ups[i], extendDown, top, lowerName, upperName, upperWalls);
        }
        return regions.Count;
    }

    /// <summary>
    /// Зона лестницы точно по её клеткам: по Box Collider 2D на клетку, склеенные Composite Collider 2D.
    /// Нижние ступеньки продлеваются вниз на extendDown клеток.
    /// </summary>
    private static void CreateStairsZone(GameObject parent, string name, Grid grid, HashSet<Vector3Int> region,
                                         Vector3Int up, float extendDown, Tilemap top, string lowerName, string upperName,
                                         HashSet<Vector3Int> upperWalls)
    {
        GameObject zone = new GameObject(name);
        zone.transform.SetParent(parent.transform, false);
        zone.transform.position = Vector3.zero;

        Rigidbody2D body = zone.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;

        Vector2 cellSize = new Vector2(grid.cellSize.x, grid.cellSize.y);
        Vector2 upStep = new Vector2(up.x * cellSize.x, up.y * cellSize.y);
        Vector2 upAxis = new Vector2(Mathf.Abs(upStep.x), Mathf.Abs(upStep.y));

        foreach (Vector3Int cell in region)
        {
            Vector2 size = cellSize;
            Vector2 offset = grid.GetCellCenterWorld(cell);

            // Нижняя ступенька (под ней уже не лестница) — тянем вниз. Но не туда, где стоит стена
            // верхнего уровня: иначе игрок, переключившись наверх, оказался бы внутри стены.
            bool isBottomStep = !region.Contains(cell - up);
            bool wallBelow = upperWalls.Contains(cell - up);
            if (isBottomStep && !wallBelow && extendDown > 0f)
            {
                size += upAxis * extendDown;
                offset -= upStep * (extendDown * 0.5f);
            }

            BoxCollider2D box = zone.AddComponent<BoxCollider2D>();
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
            box.size = size;
            box.offset = offset;
        }

        CompositeCollider2D composite = zone.AddComponent<CompositeCollider2D>();
        composite.isTrigger = true;
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        ElevationTrigger2D elevation = zone.AddComponent<ElevationTrigger2D>();
        SerializedObject serialized = new SerializedObject(elevation);
        serialized.FindProperty("layerAbove").stringValue = upperName;
        serialized.FindProperty("sortingLayerAbove").stringValue = upperName;
        serialized.FindProperty("layerBelow").stringValue = lowerName;
        serialized.FindProperty("sortingLayerBelow").stringValue = lowerName;
        serialized.FindProperty("hillTop").objectReferenceValue = top;
        serialized.FindProperty("upDirection").vector2Value = new Vector2(up.x, up.y);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>С какой стороны лестница примыкает к верху холма — там у неё «верх».</summary>
    private static Vector3Int FindUpDirection(HashSet<Vector3Int> region, HashSet<Vector3Int> top)
    {
        Vector3Int best = Vector3Int.up;
        int bestCount = 0;
        foreach (Vector3Int direction in FourDirections)
        {
            int count = 0;
            foreach (Vector3Int cell in region)
            {
                if (top.Contains(cell + direction)) count++;
            }
            if (count > bestCount)
            {
                bestCount = count;
                best = direction;
            }
        }
        return best;
    }

    private static List<HashSet<Vector3Int>> SplitIntoRegions(HashSet<Vector3Int> cells)
    {
        List<HashSet<Vector3Int>> regions = new List<HashSet<Vector3Int>>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();

        foreach (Vector3Int start in cells)
        {
            if (!visited.Add(start)) continue;

            HashSet<Vector3Int> region = new HashSet<Vector3Int> { start };
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector3Int cell = queue.Dequeue();
                foreach (Vector3Int direction in FourDirections)
                {
                    Vector3Int next = cell + direction;
                    if (cells.Contains(next) && visited.Add(next))
                    {
                        region.Add(next);
                        queue.Enqueue(next);
                    }
                }
            }
            regions.Add(region);
        }
        return regions;
    }

    private static void GetWorldBounds(Grid grid, HashSet<Vector3Int> region, out Vector3 min, out Vector3 max)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (Vector3Int cell in region)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }
        min = grid.CellToWorld(new Vector3Int(minX, minY, 0));
        max = grid.CellToWorld(new Vector3Int(maxX + 1, maxY + 1, 0));
    }

    private static HashSet<Vector3Int> CellsOf(Tilemap tilemap)
    {
        HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
        if (tilemap == null) return cells;

        tilemap.CompressBounds();
        foreach (Vector3Int position in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(position)) cells.Add(new Vector3Int(position.x, position.y, 0));
        }
        return cells;
    }

    private static Tilemap GetOrCreateCollisionMap(Grid grid, string objectName, string levelName, Color color)
    {
        Transform existing = grid.transform.Find(objectName);
        GameObject mapObject;
        if (existing != null)
        {
            mapObject = existing.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(mapObject, "KT: стены");
        }
        else
        {
            mapObject = new GameObject(objectName, typeof(Tilemap), typeof(TilemapRenderer));
            Undo.RegisterCreatedObjectUndo(mapObject, "KT: стены");
            mapObject.transform.SetParent(grid.transform, false);
        }

        mapObject.layer = LayerMask.NameToLayer(levelName);

        Tilemap tilemap = mapObject.GetComponent<Tilemap>();
        tilemap.ClearAllTiles();
        tilemap.color = color;

        TilemapRenderer tilemapRenderer = mapObject.GetComponent<TilemapRenderer>();
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
        if (unlit != null) tilemapRenderer.sharedMaterial = unlit; // стены не зависят от света и не чернеют
        tilemapRenderer.sortingLayerName = TopSortingLayer();
        tilemapRenderer.sortingOrder = 32000;
        tilemapRenderer.enabled = true;

        Rigidbody2D body = GetOrAdd<Rigidbody2D>(mapObject);
        body.bodyType = RigidbodyType2D.Static;

        TilemapCollider2D tilemapCollider = GetOrAdd<TilemapCollider2D>(mapObject);
        tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

        CompositeCollider2D composite = GetOrAdd<CompositeCollider2D>(mapObject);
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        return tilemap;
    }

    private static void Fill(Tilemap tilemap, HashSet<Vector3Int> cells, TileBase tile)
    {
        Vector3Int[] positions = new Vector3Int[cells.Count];
        cells.CopyTo(positions);
        TileBase[] tiles = new TileBase[positions.Length];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
        tilemap.SetTiles(positions, tiles);
    }

    private static bool RemoveColliders(Tilemap tilemap)
    {
        if (tilemap == null) return false;
        bool removed = false;

        CompositeCollider2D composite = tilemap.GetComponent<CompositeCollider2D>();
        if (composite != null) { Undo.DestroyObjectImmediate(composite); removed = true; }

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null) { Undo.DestroyObjectImmediate(tilemapCollider); removed = true; }

        Rigidbody2D body = tilemap.GetComponent<Rigidbody2D>();
        if (body != null && tilemap.GetComponent<Collider2D>() == null) { Undo.DestroyObjectImmediate(body); removed = true; }

        return removed;
    }

    private static void SetTilemapSorting(Tilemap tilemap, string sortingLayerName, int order)
    {
        if (tilemap == null) return;
        TilemapRenderer tilemapRenderer = tilemap.GetComponent<TilemapRenderer>();
        if (tilemapRenderer == null) return;
        Undo.RecordObject(tilemapRenderer, "KT: отрисовка холма");
        tilemapRenderer.sortingLayerName = sortingLayerName;
        tilemapRenderer.sortingOrder = order;
    }

    private static string TopSortingLayer()
    {
        SortingLayer[] layers = SortingLayer.layers;
        return layers.Length > 0 ? layers[layers.Length - 1].name : "Default";
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    // ============================== Тайл стены ==============================

    private static Tile GetOrCreateCollisionTile()
    {
        Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(CollisionTilePath);
        if (tile != null) return tile;

        EnsureFolder(ArtFolder);
        Sprite sprite = EnsureCollisionSprite();
        if (sprite == null)
        {
            EditorUtility.DisplayDialog("Ошибка", "Не получилось создать картинку для стен в " + ArtFolder, "Понятно");
            return null;
        }

        tile = CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        AssetDatabase.CreateAsset(tile, CollisionTilePath);
        AssetDatabase.SaveAssets();
        return tile;
    }

    private static Sprite EnsureCollisionSprite()
    {
        if (!File.Exists(CollisionSpritePath))
        {
            Texture2D texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(CollisionSpritePath, texture.EncodeToPNG());
            DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CollisionSpritePath);
        }

        TextureImporter importer = AssetImporter.GetAtPath(CollisionSpritePath) as TextureImporter;
        if (importer != null &&
            (importer.textureType != TextureImporterType.Sprite || !Mathf.Approximately(importer.spritePixelsPerUnit, 16f)))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(CollisionSpritePath);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    // ============================== 3. Объекты ==============================

    private static void MoveSelectionToLevel(int level)
    {
        string name = LevelName(level);
        int layer = LayerMask.NameToLayer(name);

        foreach (GameObject target in Selection.gameObjects)
        {
            Undo.RegisterFullObjectHierarchyUndo(target, "KT: перенести на уровень");
            KTUtils.SetLayerRecursively(target, layer);
            KTUtils.SetSortingLayer(target, name);
            EditorUtility.SetDirty(target);
        }
    }

    private static void MoveSortingLayerContents(string from, string to)
    {
        int moved = 0;

        foreach (SpriteRenderer spriteRenderer in FindAll<SpriteRenderer>())
        {
            if (spriteRenderer.sortingLayerName != from) continue;
            Undo.RecordObject(spriteRenderer, "KT: слой отрисовки");
            spriteRenderer.sortingLayerName = to;
            moved++;
        }
        foreach (TilemapRenderer tilemapRenderer in FindAll<TilemapRenderer>())
        {
            if (tilemapRenderer.sortingLayerName != from) continue;
            Undo.RecordObject(tilemapRenderer, "KT: слой отрисовки");
            tilemapRenderer.sortingLayerName = to;
            moved++;
        }

        EditorUtility.DisplayDialog("Готово", $"Перенесено объектов: {moved}.\n" +
            "Префабы в окне Project не меняются — у них поставьте Sorting Layer вручную или переносите расставленные копии.", "Отлично");
    }

    // ============================== 4. Видимость ==============================

    private static void SetCollisionVisible(bool visible)
    {
        foreach (TilemapRenderer tilemapRenderer in FindAll<TilemapRenderer>())
        {
            if (!tilemapRenderer.gameObject.name.StartsWith(CollisionPrefix)) continue;
            Undo.RecordObject(tilemapRenderer, "KT: видимость стен");
            tilemapRenderer.enabled = visible;
        }
    }

    private static T[] FindAll<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
