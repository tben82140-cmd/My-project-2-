using UnityEngine;

/// <summary>
/// Телепорт, портал, труба, дверь в другую комнату. Вешается на объект с коллайдером Is Trigger.
/// Игрок, зашедший в триггер, переносится в точку Destination.
/// Чтобы сделать пару порталов, поставьте два телепорта и укажите их друг другу — обратно сразу не перекинет.
/// Уровни высоты: если точка назначения стоит на уровне (Layer Level1/Level2/…), игрок перейдёт на него.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Teleporter2D : KTBehaviour
{
    [Tooltip("Куда переносит игрока. Можно указать другой телепорт.")]
    [SerializeField] private Transform destination;

    [Tooltip("Сохранять скорость игрока после переноса.")]
    [SerializeField] private bool keepVelocity = true;

    [Tooltip("Эффект на входе и выходе (необязательно).")]
    [SerializeField] private GameObject effectPrefab;

    [SerializeField] private AudioClip teleportSound;

    private const float Cooldown = 0.5f;
    private static float lastTeleportTime = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        lastTeleportTime = -10f;
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (destination == null || !KTUtils.IsPlayer(other)) return;
        if (Time.time - lastTeleportTime < Cooldown) return;

        Rigidbody2D body = other.attachedRigidbody;
        if (body == null) return;

        lastTeleportTime = Time.time;

        KTUtils.SpawnEffect(effectPrefab, body.position);
        KTUtils.SpawnEffect(effectPrefab, destination.position);
        KTUtils.PlaySound(teleportSound);

        body.position = destination.position;
        body.transform.position = destination.position;

        string destinationLevel = LayerMask.LayerToName(destination.gameObject.layer);
        if (destinationLevel.StartsWith("Level"))
        {
            int sortingId = SortingLayer.NameToID(destinationLevel);
            if (SortingLayer.IsValid(sortingId)) KTUtils.ApplyLevel(body.gameObject, destination.gameObject.layer, sortingId);
        }
        if (!keepVelocity) body.linearVelocity = Vector2.zero;

        CameraFollow2D.SnapToTarget();
    }
}
