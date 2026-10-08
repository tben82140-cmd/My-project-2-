using System.Collections;
using UnityEngine;

/// <summary>
/// Рассыпающаяся платформа. Вешается на платформу. Работает в обоих вариантах:
///
/// Платформер (обычный коллайдер): встал сверху — трясётся — падает вниз — через время возвращается.
/// Вид сверху (коллайдер Is Trigger): это пол над пропастью или водой. Встал — трясётся — проваливается,
///   и игрок, который не успел уйти, падает в смертельную зону под ней (DeathZone2D). Потом пол возвращается.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FallingPlatform2D : KTBehaviour, IKTPlatform
{
    [Tooltip("Сколько секунд платформа трясётся, прежде чем упасть.")]
    [SerializeField] private float fallDelay = 0.6f;

    [Tooltip("Сила тряски.")]
    [SerializeField] private float shakeStrength = 0.05f;

    [Tooltip("Платформер: насколько быстро падает (Gravity Scale во время падения).")]
    [SerializeField] private float fallGravity = 3f;

    [Tooltip("Через сколько секунд платформа вернётся. 0 — не вернётся.")]
    [SerializeField] private float respawnTime = 3f;

    [Tooltip("Звук треска (необязательно).")]
    [SerializeField] private AudioClip crackSound;

    /// <summary>Пока платформа падает — её настоящая скорость, чтобы игрок падал вместе с ней, а не «парил».</summary>
    public Vector2 Velocity => rb != null && rb.bodyType == RigidbodyType2D.Dynamic ? rb.linearVelocity : Vector2.zero;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private Renderer[] renderers;
    private Vector2 startPosition;
    private float startRotation;
    private Vector3 startScale;
    private bool triggered;

    private bool IsFloor => ownCollider != null && ownCollider.isTrigger;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        ownCollider = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>();
        startPosition = rb.position;
        startRotation = rb.rotation;
        startScale = transform.localScale;
    }

    // Платформер: игрок встал на платформу сверху.
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (triggered || IsFloor || !KTUtils.IsPlayer(collision.collider)) return;
        if (collision.collider.bounds.min.y < ownCollider.bounds.max.y - 0.1f) return; // задел снизу или сбоку
        StartCoroutine(FallRoutine());
    }

    // Вид сверху: игрок наступил на пол.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || !IsFloor || !KTUtils.IsPlayer(other)) return;
        StartCoroutine(FallRoutine());
    }

    private IEnumerator FallRoutine()
    {
        triggered = true;
        KTUtils.PlaySound(crackSound);

        float shakeEnd = Time.time + fallDelay;
        while (Time.time < shakeEnd)
        {
            rb.MovePosition(startPosition + Random.insideUnitCircle * shakeStrength);
            yield return new WaitForFixedUpdate();
        }
        rb.MovePosition(startPosition);

        if (IsFloor)
        {
            yield return SinkFloor();
        }
        else
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = fallGravity;
        }

        if (respawnTime <= 0f)
        {
            if (!IsFloor) Destroy(gameObject, 5f);
            yield break;
        }

        yield return new WaitForSeconds(respawnTime);
        Restore();
    }

    private IEnumerator SinkFloor()
    {
        // Пол «проваливается»: уменьшается и исчезает. Без коллайдера под ногами игрок оказывается над пропастью.
        float duration = 0.25f;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(startScale, startScale * 0.2f, t / duration);
            yield return null;
        }
        ownCollider.enabled = false;
        SetVisible(false);
    }

    private void Restore()
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = startPosition;
        rb.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, Quaternion.Euler(0f, 0f, startRotation));
        transform.localScale = startScale;
        ownCollider.enabled = true;
        SetVisible(true);
        triggered = false;
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer platformRenderer in renderers)
        {
            platformRenderer.enabled = visible;
        }
    }
}
