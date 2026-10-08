using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Дверь, ворота, решётка, мост — всё, что можно открыть и закрыть. Вешается на дверь
/// (подходит и для отдельного спрайта, и для Tilemap).
/// Сама по себе дверь не открывается — её открывают другие скрипты через события:
///   • CoinCounter → On All Coins Collected → Door2D.Open   (дверь откроется, когда собраны все монеты)
///   • TriggerEvent2D на ключе → On Player Enter → Door2D.Open
///   • Interactable2D на рычаге → On Interact → Door2D.Toggle
/// </summary>
public class Door2D : KTBehaviour
{
    [Tooltip("Дверь открыта с самого начала.")]
    [SerializeField] private bool startsOpen = false;

    [Tooltip("Картинка открытой двери. Если пусто — при открытии дверь просто исчезает.")]
    [SerializeField] private Sprite openSprite;

    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    [Header("События")]
    [SerializeField] private UnityEvent onOpened;
    [SerializeField] private UnityEvent onClosed;

    public bool IsOpen => isOpen;

    private bool isOpen;
    private SpriteRenderer spriteRenderer;
    private Sprite closedSprite;
    private Renderer[] renderers;
    private readonly List<Collider2D> solidColliders = new List<Collider2D>();

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        closedSprite = spriteRenderer != null ? spriteRenderer.sprite : null;
        renderers = GetComponentsInChildren<Renderer>();

        foreach (Collider2D col in GetComponentsInChildren<Collider2D>())
        {
            if (!col.isTrigger) solidColliders.Add(col);
        }

        isOpen = startsOpen;
        Apply(isOpen);
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        Apply(true);
        KTUtils.PlaySound(openSound);
        onOpened.Invoke();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        Apply(false);
        KTUtils.PlaySound(closeSound);
        onClosed.Invoke();
    }

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    private void Apply(bool open)
    {
        foreach (Collider2D col in solidColliders)
        {
            col.enabled = !open;
        }

        if (openSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = open ? openSprite : closedSprite;
            return;
        }

        foreach (Renderer doorRenderer in renderers)
        {
            doorRenderer.enabled = !open;
        }
    }
}
