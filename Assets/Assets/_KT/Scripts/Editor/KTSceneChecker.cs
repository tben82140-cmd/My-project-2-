using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Проверка сцены: меню KT → Проверить сцену.
/// Ищет типичные ошибки настройки и подсказывает, как их исправить. Сама ничего не меняет.
/// Кнопка «Показать» выделяет объект с проблемой.
/// </summary>
public class KTSceneChecker : EditorWindow
{
    private enum Level
    {
        Ok,
        Warning,
        Error
    }

    private struct Entry
    {
        public Level level;
        public string message;
        public Object context;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly HashSet<string> reportedKeys = new HashSet<string>();
    private Vector2 scroll;

    [MenuItem("KT/Проверить сцену", priority = 2)]
    private static void Open()
    {
        KTSceneChecker window = GetWindow<KTSceneChecker>("Проверка сцены");
        window.minSize = new Vector2(420, 300);
        window.Run();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Проверить ещё раз", GUILayout.Height(28))) Run();

        int errors = 0;
        int warnings = 0;
        foreach (Entry entry in entries)
        {
            if (entry.level == Level.Error) errors++;
            if (entry.level == Level.Warning) warnings++;
        }

        EditorGUILayout.LabelField($"Ошибок: {errors}    Предупреждений: {warnings}", EditorStyles.boldLabel);
        if (errors == 0 && warnings == 0)
        {
            EditorGUILayout.HelpBox("Всё основное настроено. Нажмите Play и запишите видео!", MessageType.Info);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (Entry entry in entries)
        {
            EditorGUILayout.BeginHorizontal();

            MessageType type = entry.level == Level.Error ? MessageType.Error
                : entry.level == Level.Warning ? MessageType.Warning
                : MessageType.Info;
            EditorGUILayout.HelpBox(entry.message, type);

            if (entry.context != null && GUILayout.Button("Показать", GUILayout.Width(80), GUILayout.Height(38)))
            {
                Selection.activeObject = entry.context;
                EditorGUIUtility.PingObject(entry.context);
            }

            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void Run()
    {
        entries.Clear();
        reportedKeys.Clear();

        GameObject player = CheckPlayer();
        bool platformer = player != null && player.GetComponent<PlatformerMovement2D>() != null;
        bool topDown = player != null && player.GetComponent<TopDownMovement2D>() != null;

        CheckTilemaps();
        CheckCoins(platformer);
        if (platformer) CheckBounce();
        if (topDown) CheckStream();
        CheckCamera();
        CheckLights();
        CheckProject(topDown);

        entries.Sort((a, b) => b.level.CompareTo(a.level));
        Repaint();
    }

    // ---------- Игрок ----------

    private GameObject CheckPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0)
        {
            Add(Level.Error, "Нет объекта с тегом Player. Выберите игрока → вверху инспектора Tag → Player.");
            return null;
        }
        if (players.Length > 1)
        {
            Add(Level.Warning, "Тег Player стоит на нескольких объектах. Оставьте его только на самом игроке.", players[1]);
        }

        GameObject player = players[0];
        int errorsBefore = CountErrors();

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            Add(Level.Error, "У игрока нет Rigidbody 2D. Add Component → Rigidbody 2D.", player);
            return player;
        }
        if (body.bodyType != RigidbodyType2D.Dynamic)
        {
            Add(Level.Error, "У Rigidbody 2D игрока Body Type должен быть Dynamic.", player);
        }

        if (!HasSolidCollider(player))
        {
            Add(Level.Error, "У игрока нет обычного коллайдера (не триггера) — он провалится сквозь землю.", player);
        }

        bool platformer = player.GetComponent<PlatformerMovement2D>() != null;
        bool topDown = player.GetComponent<TopDownMovement2D>() != null;

        if (!platformer && !topDown)
        {
            Add(Level.Error, "У игрока нет скрипта движения: PlatformerMovement2D (вариант А) или TopDownMovement2D (вариант Б).", player);
        }
        if (platformer && topDown)
        {
            Add(Level.Error, "На игроке оба скрипта движения сразу. Оставьте один — для своего варианта.", player);
        }

        if (topDown)
        {
            if (body.gravityScale != 0f)
                Add(Level.Error, "Вид сверху: у игрока Gravity Scale должен быть 0, иначе он уедет вниз.", player);
            if (body.linearDamping <= 0f)
                Add(Level.Error, "Вид сверху: у игрока Linear Damping = 0 — он не сможет остановиться. Поставьте около 8.", player);
            if (!body.freezeRotation)
                Add(Level.Warning, "Вид сверху: включите Constraints → Freeze Rotation Z, иначе игрок будет крутиться от ударов.", player);
        }

        if (platformer)
        {
            if (body.gravityScale <= 0f)
                Add(Level.Error, "Платформер: у игрока Gravity Scale 0 — он не будет падать. Поставьте 3–4.", player);
            else if (body.gravityScale < 2f)
                Add(Level.Warning, $"Платформер: у игрока Gravity Scale {body.gravityScale} — падение будет плавным, «лунным». Для аркады ставят 3–4 (высота прыжка не изменится).", player);
            if (body.linearDamping > 0.5f)
                Add(Level.Warning, $"Платформер: у игрока Linear Damping {body.linearDamping} — он падает и прыгает как в воде. Поставьте 0.", player);
            if (!(player.GetComponent<Collider2D>() is CircleCollider2D) && !body.freezeRotation)
                Add(Level.Warning, "Платформер: персонаж может упасть набок. Включите Constraints → Freeze Rotation Z (шарику можно не включать).", player);
        }

        if (body.interpolation == RigidbodyInterpolation2D.None)
            Add(Level.Warning, "У Rigidbody 2D игрока Interpolate = None — камера будет дрожать. Поставьте Interpolate.", player);

        if (player.GetComponentInChildren<Animator>() != null && player.GetComponent<PlayerAnimator2D>() == null)
            Add(Level.Warning, "У игрока есть Animator, но нет PlayerAnimator2D — анимации не будут переключаться. Запустите KT → Мастер аниматора.", player);

        PlayerAttack2D attack = player.GetComponent<PlayerAttack2D>();
        if (attack != null && FindAll<Damageable2D>().Length == 0)
            Add(Level.Warning, "У игрока есть удар (PlayerAttack2D), но бить некого: добавьте Damageable2D врагам, ящикам или горшкам.", player);

        Animator playerAnimator = player.GetComponentInChildren<Animator>();
        AnimatorController playerController = playerAnimator != null ? playerAnimator.runtimeAnimatorController as AnimatorController : null;
        if (playerController != null && attack == null)
        {
            foreach (AnimatorControllerParameter parameter in playerController.parameters)
            {
                if (parameter.name == "Attack")
                {
                    Add(Level.Warning, "В аниматоре есть удар, но на игроке нет PlayerAttack2D — анимация удара не запустится.", player);
                    break;
                }
            }
        }

        foreach (Animator enemyAnimator in FindAll<Animator>())
        {
            Rigidbody2D ownerBody = enemyAnimator.GetComponentInParent<Rigidbody2D>();
            GameObject owner = ownerBody != null ? ownerBody.gameObject : enemyAnimator.gameObject;
            bool isEnemy = owner.GetComponent<EnemyPatrol2D>() != null || owner.GetComponent<EnemyChaser2D>() != null;
            if (isEnemy && owner.GetComponentInChildren<EnemyAnimator2D>() == null)
                Add(Level.Warning, $"{owner.name}: у врага есть Animator, но нет EnemyAnimator2D — анимации не будут переключаться. KT → Мастер аниматора → «Враг».", owner);
        }

        foreach (Damageable2D damageable in FindAll<Damageable2D>())
        {
            if (damageable.GetComponentInChildren<Collider2D>() == null)
                Add(Level.Error, $"{damageable.name}: у Damageable2D нет коллайдера — по нему невозможно попасть.", damageable);
        }

        if (FindAll<Hazard2D>().Length > 0 && player.GetComponent<PlayerHealth2D>() == null)
            Add(Level.Warning, "На сцене есть опасности (Hazard2D), но у игрока нет PlayerHealth2D — урон проходить не будет.", player);

        if (CountErrors() == errorsBefore) Add(Level.Ok, "Игрок: основные настройки в порядке.", player);
        return player;
    }

    // ---------- Тайлмапы ----------

    private void CheckTilemaps()
    {
        if (FindAll<Tilemap>().Length == 0)
        {
            Add(Level.Error, "На сцене нет ни одного Tilemap. Уровень нужно собрать из тайлов: Hierarchy → 2D Object → Tilemap → Rectangular.");
            return;
        }

        TilemapCollider2D[] colliders = FindAll<TilemapCollider2D>();
        if (colliders.Length == 0)
        {
            Add(Level.Error, "Ни у одного Tilemap нет Tilemap Collider 2D — игроку не на чем стоять и не во что упираться.");
            return;
        }

        foreach (TilemapCollider2D tilemapCollider in colliders)
        {
            string name = tilemapCollider.name;
            CompositeCollider2D composite = tilemapCollider.GetComponent<CompositeCollider2D>();

            if (composite == null)
            {
                Add(Level.Warning, $"{name}: нет Composite Collider 2D — игрок будет цепляться за стыки тайлов.", tilemapCollider);
                continue;
            }

            if (tilemapCollider.compositeOperation == Collider2D.CompositeOperation.None)
                Add(Level.Warning, $"{name}: у Tilemap Collider 2D поставьте Composite Operation = Merge, иначе Composite не работает.", tilemapCollider);

            Rigidbody2D body = tilemapCollider.GetComponent<Rigidbody2D>();
            if (body != null && body.bodyType != RigidbodyType2D.Static)
                Add(Level.Error, $"{name}: Rigidbody 2D не Static — при запуске Tilemap упадёт вниз! Поставьте Body Type = Static.", tilemapCollider);
        }
    }

    // ---------- Монеты ----------

    private void CheckCoins(bool platformer)
    {
        CoinPickup[] coins = FindAll<CoinPickup>();
        CoinBurst[] bursts = FindAll<CoinBurst>();

        int expected = coins.Length;
        foreach (CoinBurst burst in bursts)
        {
            SerializedObject burstObject = new SerializedObject(burst);
            expected += burstObject.FindProperty("coinCount").intValue;

            GameObject prefab = burstObject.FindProperty("coinPrefab").objectReferenceValue as GameObject;
            if (prefab == null)
            {
                Add(Level.Error, $"{burst.name}: в CoinBurst не назначен префаб монеты.", burst);
                continue;
            }

            CoinPickup prefabPickup = prefab.GetComponentInChildren<CoinPickup>(true);
            if (prefabPickup == null)
            {
                Add(Level.Error, $"В префабе {prefab.name} нет CoinPickup — монеты не будут собираться.", prefab);
            }
            else
            {
                CheckCoin(prefabPickup, false, prefab);
            }

            Rigidbody2D prefabBody = prefab.GetComponent<Rigidbody2D>();
            if (prefabBody != null && prefabBody.gravityScale != 0f)
                Add(Level.Warning, $"Префаб {prefab.name}: для вида сверху Gravity Scale должен быть 0.", prefab);
            if (prefabBody != null && prefabBody.linearDamping <= 0f)
                Add(Level.Warning, $"Префаб {prefab.name}: Linear Damping = 0 — монеты не остановятся. Поставьте 2–4.", prefab);
        }

        foreach (CoinPickup coin in coins)
        {
            CheckCoin(coin, platformer, coin);
        }

        if (expected == 0)
            Add(Level.Error, "На сцене нет монет: ни объектов с CoinPickup, ни сундука с CoinBurst.");
        else if (expected < 6 || expected > 8)
            Add(Level.Warning, $"Монет на уровне: {expected}. По заданию нужно 6–8.");
        else
            Add(Level.Ok, $"Монет на уровне: {expected}.");

        CoinCounter counter = FindAll<CoinCounter>().Length > 0 ? FindAll<CoinCounter>()[0] : null;
        if (counter == null)
        {
            Add(Level.Warning, "Нет CoinCounter — счёт монет не будет виден. Добавьте его на Canvas.");
        }
        else if (new SerializedObject(counter).FindProperty("label").objectReferenceValue == null)
        {
            Add(Level.Warning, "В CoinCounter не назначен Label — перетащите туда текст TextMeshPro.", counter);
        }
    }

    private void CheckCoin(CoinPickup pickup, bool platformer, Object context)
    {
        Collider2D trigger = pickup.GetComponent<Collider2D>();
        if (trigger != null && !trigger.isTrigger)
            AddOnce("coin-trigger", Level.Error, "У коллайдера с CoinPickup не стоит Is Trigger — монета не будет собираться (проверьте и остальные монеты).", context);

        if (pickup.transform.parent == null)
        {
            AddOnce("coin-structure", Level.Warning, "CoinPickup висит на самой монете, а не на дочернем объекте PickupZone. Сделайте структуру Coin → PickupZone, как в задании.", context);
            return;
        }

        Transform root = pickup.transform.parent;
        Rigidbody2D body = root.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            AddOnce("coin-body", Level.Error, "У монеты нет Rigidbody 2D — она не будет падать или разлетаться (проверьте и остальные монеты).", context);
        }
        else if (platformer && body.gravityScale > 0f && body.collisionDetectionMode == CollisionDetectionMode2D.Discrete)
        {
            AddOnce("coin-continuous", Level.Warning, "У Rigidbody 2D монет поставьте Collision Detection = Continuous, чтобы они не проваливались.", context);
        }

        if (!HasSolidCollider(root.gameObject, false))
            AddOnce("coin-solid", Level.Error, "У монеты нет обычного коллайдера (не триггера) — она провалится сквозь пол или улетит сквозь стену.", context);

        if (platformer && body != null && body.gravityScale > 0f && !body.freezeRotation && root.GetComponent<CircleCollider2D>() != null)
            AddOnce("coin-rotation", Level.Warning, "Монеты круглые и не зафиксированы — они будут катиться и падать с платформ. Rigidbody 2D монеты → Constraints → Freeze Rotation Z.", context);

        CircleCollider2D solidCircle = root.GetComponent<CircleCollider2D>();
        CircleCollider2D triggerCircle = pickup.GetComponent<CircleCollider2D>();
        if (solidCircle != null && triggerCircle != null && !solidCircle.isTrigger)
        {
            float solidRadius = solidCircle.radius * Mathf.Abs(root.lossyScale.x);
            float triggerRadius = triggerCircle.radius * Mathf.Abs(pickup.transform.lossyScale.x);
            if (triggerRadius <= solidRadius)
                AddOnce("coin-trigger-size", Level.Warning, "Триггер PickupZone не больше коллайдера монеты — игрок будет сначала толкать монету и только потом собирать. Увеличьте Radius у PickupZone.", context);
        }
    }

    // ---------- Батут и ручей ----------

    private void CheckBounce()
    {
        bool found = false;

        foreach (Collider2D col in FindAll<Collider2D>())
        {
            if (col.isTrigger) continue;

            PhysicsMaterial2D material = col.sharedMaterial;
            if (material == null && col.attachedRigidbody != null) material = col.attachedRigidbody.sharedMaterial;
            if (material != null && material.bounciness >= 0.8f) found = true;

            // Типичная ошибка: материал на Tilemap Collider, а работает Composite.
            if (col is TilemapCollider2D tilemapCollider
                && tilemapCollider.sharedMaterial != null
                && tilemapCollider.compositeOperation != Collider2D.CompositeOperation.None)
            {
                CompositeCollider2D composite = tilemapCollider.GetComponent<CompositeCollider2D>();
                if (composite != null && composite.sharedMaterial == null)
                {
                    Add(Level.Error, $"{tilemapCollider.name}: Physics Material стоит на Tilemap Collider 2D, а форму делает Composite Collider 2D. Перенесите материал в поле Material у Composite Collider 2D.", tilemapCollider);
                }
            }
        }

        if (found) Add(Level.Ok, "Батут: найден коллайдер с упругим Physics Material 2D.");
        else Add(Level.Warning, "Батут не найден: ни у одного коллайдера нет Physics Material 2D с Bounciness от 1.");
    }

    private void CheckStream()
    {
        AreaEffector2D[] effectors = FindAll<AreaEffector2D>();
        if (effectors.Length == 0)
        {
            Add(Level.Warning, "Нет Area Effector 2D — течения ручья нет.");
            return;
        }

        foreach (AreaEffector2D effector in effectors)
        {
            CompositeCollider2D composite = effector.GetComponent<CompositeCollider2D>();
            Collider2D area = composite != null ? composite : effector.GetComponent<Collider2D>();

            if (area == null)
            {
                Add(Level.Error, $"{effector.name}: у Area Effector 2D нет коллайдера.", effector);
                continue;
            }
            if (!area.usedByEffector)
                Add(Level.Error, $"{effector.name}: у коллайдера ({area.GetType().Name}) не включено Used By Effector — течение не работает.", effector);
            if (!area.isTrigger)
                Add(Level.Warning, $"{effector.name}: у коллайдера ({area.GetType().Name}) выключен Is Trigger — игрок будет упираться в воду как в стену.", effector);
            if (composite != null && composite.geometryType == CompositeCollider2D.GeometryType.Outlines)
                Add(Level.Error, $"{effector.name}: у Composite Collider 2D Geometry Type = Outlines — течение работает только по краям. Поставьте Polygons.", effector);
            if (Mathf.Approximately(effector.forceMagnitude, 0f))
                Add(Level.Warning, $"{effector.name}: Force Magnitude = 0 — течение никого не сносит.", effector);
        }
    }

    // ---------- Камера ----------

    private void CheckCamera()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            Add(Level.Error, "Нет камеры с тегом MainCamera.");
            return;
        }
        if (main.GetComponent<CameraFollow2D>() == null)
            Add(Level.Warning, "Камера не следует за игроком: добавьте на Main Camera скрипт CameraFollow2D.", main);
    }

    // ---------- Свет URP 2D ----------

    private void CheckLights()
    {
        // Собираем слои, которые освещает хоть один 2D-свет.
        HashSet<int> litLayers = new HashSet<int>();
        int lightCount = 0;
        foreach (Behaviour behaviour in Object.FindObjectsByType<Behaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (behaviour.GetType().Name != "Light2D") continue;
            SerializedProperty targets = new SerializedObject(behaviour).FindProperty("m_ApplyToSortingLayers");
            if (targets == null) continue;
            lightCount++;
            for (int i = 0; i < targets.arraySize; i++) litLayers.Add(targets.GetArrayElementAtIndex(i).intValue);
        }
        if (lightCount == 0) return;

        // Ищем «освещаемые» спрайты на слоях, которые никто не освещает, — они будут чёрными.
        Dictionary<string, Object> darkLayers = new Dictionary<string, Object>();
        foreach (Renderer sceneRenderer in FindAll<Renderer>())
        {
            if (!(sceneRenderer is SpriteRenderer) && !(sceneRenderer is TilemapRenderer)) continue;
            if (!sceneRenderer.enabled) continue;

            Material material = sceneRenderer.sharedMaterial;
            if (material == null || material.shader == null || material.shader.name.Contains("Unlit")) continue;
            if (litLayers.Contains(sceneRenderer.sortingLayerID)) continue;

            if (!darkLayers.ContainsKey(sceneRenderer.sortingLayerName)) darkLayers.Add(sceneRenderer.sortingLayerName, sceneRenderer);
        }

        foreach (KeyValuePair<string, Object> dark in darkLayers)
        {
            Add(Level.Error,
                $"Спрайты на Sorting Layer «{dark.Key}» будут ЧЁРНЫМИ: его не освещает ни один 2D-свет. " +
                "Выделите Global Light 2D → Target Sorting Layers → выберите All (или отметьте этот слой).",
                dark.Value);
        }
    }

    // ---------- Проект ----------

    private void CheckProject(bool topDown)
    {
        if (AssetDatabase.FindAssets("t:SpriteAtlas").Length == 0)
            Add(Level.Warning, "В проекте нет Sprite Atlas. Project → Create → 2D → Sprite Atlas и добавьте туда спрайты реквизита.");
        else
            Add(Level.Ok, "Sprite Atlas найден.");

        if (SortingLayer.layers.Length < 4)
            Add(Level.Warning, "Мало Sorting Layers. Создайте свои слои: Project Settings → Tags and Layers → Sorting Layers.");

        int onDefault = 0;
        Object firstOnDefault = null;
        foreach (SpriteRenderer spriteRenderer in FindAll<SpriteRenderer>())
        {
            if (spriteRenderer.sortingLayerName != "Default") continue;
            onDefault++;
            if (firstOnDefault == null) firstOnDefault = spriteRenderer;
        }
        if (onDefault > 0)
            Add(Level.Warning, $"Спрайтов на Sorting Layer «Default»: {onDefault}. Разложите их по своим слоям (Background, Decor, Gameplay…).", firstOnDefault);

        if (topDown)
            Add(Level.Ok, "Y-сортировку проверьте глазами: зайдите игроком за дерево и выйдите перед ним.");

        foreach (DeathZone2D zone in FindAll<DeathZone2D>())
        {
            CompositeCollider2D zoneComposite = zone.GetComponent<CompositeCollider2D>();
            Collider2D zoneCollider = zoneComposite != null ? zoneComposite : zone.GetComponent<Collider2D>();
            if (zoneCollider != null && !zoneCollider.isTrigger)
                Add(Level.Error, $"{zone.name}: у смертельной зоны коллайдер не Is Trigger — игрок будет упираться в неё, а не падать.", zone);
            if (zoneComposite != null && zoneComposite.geometryType == CompositeCollider2D.GeometryType.Outlines)
                Add(Level.Error, $"{zone.name}: у Composite Collider 2D смертельной зоны Geometry Type = Outlines — она работает только по краям. Поставьте Polygons.", zone);
        }

        if (FindAll<ElevationTrigger2D>().Length > 0)
        {
            if (LayerMask.NameToLayer("Level1") < 0)
            {
                Add(Level.Error, "На сцене есть лестницы, но проект не подготовлен: KT → Уровни высоты → «Подготовить проект».");
            }
            else
            {
                GameObject levelPlayer = GameObject.FindGameObjectWithTag("Player");
                if (levelPlayer != null && !LayerMask.LayerToName(levelPlayer.layer).StartsWith("Level"))
                    Add(Level.Warning, "Игрок не стоит ни на одном уровне высоты. Выделите игрока → KT → Уровни высоты → «Уровень 1».", levelPlayer);
            }
        }

        bool needsBuildList = FindAll<SceneLoader>().Length > 0 || FindAll<PlayerHealth2D>().Length > 0;
        if (needsBuildList && !IsActiveSceneInBuildList())
            Add(Level.Warning, "Сцена не добавлена в список сцен сборки — перезапуск и переход между уровнями не сработают. File → Build Profiles → Scene List → Add Open Scenes.");
    }

    // ---------- Помощники ----------

    private static T[] FindAll<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private static bool HasSolidCollider(GameObject target, bool includeChildren = true)
    {
        Collider2D[] colliders = includeChildren ? target.GetComponentsInChildren<Collider2D>() : target.GetComponents<Collider2D>();
        foreach (Collider2D col in colliders)
        {
            if (!col.isTrigger) return true;
        }
        return false;
    }

    private static bool IsActiveSceneInBuildList()
    {
        string path = SceneManager.GetActiveScene().path;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && scene.path == path) return true;
        }
        return false;
    }

    private int CountErrors()
    {
        int count = 0;
        foreach (Entry entry in entries)
        {
            if (entry.level == Level.Error) count++;
        }
        return count;
    }

    private void Add(Level level, string message, Object context = null)
    {
        entries.Add(new Entry { level = level, message = message, context = context });
    }

    private void AddOnce(string key, Level level, string message, Object context)
    {
        if (reportedKeys.Add(key)) Add(level, message, context);
    }
}
