using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Универсальный триггер: «когда игрок зашёл сюда — сделай вот это». Вешается на объект с коллайдером Is Trigger.
/// Что сделать — выбирается мышкой в инспекторе, в событиях On Player Enter / On Player Exit.
///
/// Примеры:
///   • Ключ: Trigger Once + Destroy After Trigger, On Player Enter → Door2D.Open
///   • Сердечко: Destroy After Trigger, On Player Enter → PlayerHealth2D.Heal (1)
///   • Табличка: On Player Enter → Текст.SetActive (галочка), On Player Exit → Текст.SetActive (без галочки)
///   • Финиш: On Player Enter → SceneLoader.LoadNextScene
///   • Нажимная плита: On Player Enter → Door2D.Open, On Player Exit → Door2D.Close
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TriggerEvent2D : KTBehaviour
{
    [Tooltip("Сработать только один раз.")]
    [SerializeField] private bool triggerOnce = false;

    [Tooltip("Удалить этот объект после срабатывания (для ключей, сердечек, записок).")]
    [SerializeField] private bool destroyAfterTrigger = false;

    [Tooltip("Звук при срабатывании (необязательно).")]
    [SerializeField] private AudioClip sound;

    [Tooltip("Эффект при срабатывании (необязательно).")]
    [SerializeField] private GameObject effectPrefab;

    [Space]
    [SerializeField] private UnityEvent onPlayerEnter;
    [SerializeField] private UnityEvent onPlayerExit;

    private bool fired;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!KTUtils.IsPlayer(other)) return;
        if (triggerOnce && fired) return;
        fired = true;

        KTUtils.PlaySound(sound);
        KTUtils.SpawnEffect(effectPrefab, transform.position);
        onPlayerEnter.Invoke();

        if (destroyAfterTrigger) Destroy(gameObject);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!KTUtils.IsPlayer(other)) return;
        onPlayerExit.Invoke();
    }
}
