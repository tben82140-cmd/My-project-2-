using UnityEngine;

/// <summary>
/// Чекпоинт (флажок, костёр, статуя). Вешается на объект с коллайдером Is Trigger.
/// Когда игрок проходит через него, PlayerHealth2D запоминает эту точку и после смерти игрок появится здесь.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint2D : KTBehaviour
{
    [Tooltip("Где появится игрок относительно чекпоинта. Поднимите чуть выше, чтобы не застрять в земле.")]
    [SerializeField] private Vector2 respawnOffset = new Vector2(0f, 0.5f);

    [Tooltip("Картинка активированного чекпоинта, например поднятый флаг (необязательно).")]
    [SerializeField] private Sprite activeSprite;

    [Tooltip("Объект, который включится при активации: частицы, свет (необязательно).")]
    [SerializeField] private GameObject activeEffect;

    [SerializeField] private AudioClip activateSound;

    private bool activated;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        if (activeEffect != null) activeEffect.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!KTUtils.IsPlayer(other)) return;

        PlayerHealth2D health = other.GetComponentInParent<PlayerHealth2D>();
        if (health == null) return;

        health.SetCheckpoint(transform.position + (Vector3)respawnOffset);

        if (activated) return;
        activated = true;

        if (activeSprite != null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.sprite = activeSprite;
        }
        if (activeEffect != null) activeEffect.SetActive(true);
        KTUtils.PlaySound(activateSound);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + (Vector3)respawnOffset, 0.2f);
    }
}
