using UnityEngine;

/// <summary>
/// Общий «пульт» скриптов движения. Его реализуют PlatformerMovement2D и TopDownMovement2D.
/// Аниматор, здоровье и другие скрипты работают через него и поэтому подходят к обоим вариантам КТ.
/// Этот файл ни на что вешать не нужно — он просто должен лежать в проекте.
/// </summary>
public interface IKTMovement
{
    /// <summary>Можно ли сейчас управлять игроком (выключается при отбрасывании и смерти).</summary>
    bool ControlEnabled { get; set; }

    /// <summary>Стоит ли игрок на земле. При виде сверху всегда true.</summary>
    bool IsGrounded { get; }

    /// <summary>Куда игрок сейчас жмёт кнопками: от -1 до 1 по каждой оси.</summary>
    Vector2 MoveInput { get; }

    /// <summary>Множитель скорости ходьбы: 1 — обычно, 0 — стоит на месте (например, во время удара).</summary>
    float SpeedMultiplier { get; set; }
}

/// <summary>
/// Всё, на чём можно стоять и ехать: движущаяся платформа, плот, рассыпающийся пол.
/// Его реализуют MovingPlatform2D и FallingPlatform2D. Скрипты движения игрока ищут его под ногами.
/// </summary>
public interface IKTPlatform
{
    /// <summary>Скорость платформы — игрок едет вместе с ней.</summary>
    Vector2 Velocity { get; }
}

/// <summary>
/// Маленькие помощники, которыми пользуются все скрипты КТ.
/// Этот файл ни на что вешать не нужно.
/// </summary>
public static class KTUtils
{
    /// <summary>Это игрок? Проверяет тег Player у коллайдера и у его Rigidbody 2D.</summary>
    public static bool IsPlayer(Collider2D other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        return other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player");
    }

    private static AudioSource soundSource;

    /// <summary>
    /// Проиграть звук эффекта (прыжок, монета, удар). Звук двумерный — «в ушах»: он не уплывает в сторону,
    /// когда камера едет за игроком, и не затихает от расстояния. Все звуки КТ играют через один
    /// скрытый источник «KT Sounds», который создаётся сам.
    /// </summary>
    public static void PlaySound(AudioClip clip, float volume = 0.8f)
    {
        if (clip == null) return;

        if (soundSource == null)
        {
            GameObject holder = new GameObject("KT Sounds");
            Object.DontDestroyOnLoad(holder);
            soundSource = holder.AddComponent<AudioSource>();
            soundSource.playOnAwake = false;
            soundSource.spatialBlend = 0f; // 0 = 2D-звук без позиции
        }

        soundSource.PlayOneShot(clip, volume);
    }

    /// <summary>Создать эффект (например, частицы) и удалить его через lifetime секунд.</summary>
    public static void SpawnEffect(GameObject prefab, Vector3 position, float lifetime = 2f)
    {
        if (prefab == null) return;
        GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
        Object.Destroy(effect, lifetime);
    }

    /// <summary>
    /// Подбросить тело вверх импульсом так, чтобы его скорость вверх стала не меньше upSpeed.
    /// Если тело уже летит вверх быстрее — ничего не делает.
    /// </summary>
    public static void LaunchUp(Rigidbody2D body, float upSpeed)
    {
        float missingSpeed = upSpeed - body.linearVelocity.y;
        if (missingSpeed <= 0f) return;
        body.AddForce(Vector2.up * (missingSpeed * body.mass), ForceMode2D.Impulse);
    }

    /// <summary>
    /// Округлить направление до одного из четырёх: вверх, вниз, влево, вправо.
    /// При движении по диагонали выбирается горизонталь.
    /// </summary>
    public static Vector2 SnapToFourDirections(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return Vector2.zero;
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)) return new Vector2(Mathf.Sign(direction.x), 0f);
        return new Vector2(0f, Mathf.Sign(direction.y));
    }

    /// <summary>Платформер или вид сверху? Определяем по гравитации: при виде сверху она выключена.</summary>
    public static bool IsTopDown(Rigidbody2D body)
    {
        return body != null && body.gravityScale == 0f;
    }

    /// <summary>Поставить физический Layer объекту и всем его детям.</summary>
    public static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>Поставить Sorting Layer всем спрайтам объекта и его детей.</summary>
    public static void SetSortingLayer(GameObject target, string sortingLayerName)
    {
        foreach (SpriteRenderer spriteRenderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.sortingLayerName = sortingLayerName;
        }
    }

    /// <summary>Sorting Layer объекта — по его первому спрайту (0 = Default, если спрайтов нет).</summary>
    public static int GetSortingLayerId(GameObject target)
    {
        SpriteRenderer spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        return spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
    }

    /// <summary>Поставить объекту уровень: физический Layer и Sorting Layer всех спрайтов.</summary>
    public static void ApplyLevel(GameObject target, int layer, int sortingLayerId)
    {
        if (target.layer != layer) SetLayerRecursively(target, layer);
        foreach (SpriteRenderer spriteRenderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (spriteRenderer.sortingLayerID != sortingLayerId) spriteRenderer.sortingLayerID = sortingLayerId;
        }
    }

    /// <summary>Есть ли в аниматоре параметр с таким именем и типом.</summary>
    public static bool AnimatorHasParameter(Animator animator, string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type) return true;
        }
        return false;
    }
}
