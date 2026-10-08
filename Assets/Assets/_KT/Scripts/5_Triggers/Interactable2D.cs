using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// «Подойди и нажми E»: рычаг, сундук, NPC, дверь. Вешается на объект с коллайдером Is Trigger
/// (сделайте его чуть больше самого объекта — это зона, в которой работает кнопка).
/// Что сделать — выбирается мышкой в событии On Interact.
///
/// Примеры:
///   • Сундук: On Interact → CoinBurst.Burst (у CoinBurst снимите Burst On Start)
///   • Рычаг: On Interact → Door2D.Toggle
///   • NPC: On Interact → Панель диалога.SetActive (галочка)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Interactable2D : KTBehaviour
{
    [Tooltip("Подсказка над объектом, например текст «E». Показывается, когда игрок рядом (необязательно).")]
    [SerializeField] private GameObject prompt;

    [Tooltip("Можно использовать только один раз.")]
    [SerializeField] private bool onlyOnce = false;

    [Tooltip("Звук при нажатии (необязательно).")]
    [SerializeField] private AudioClip sound;

    [Space]
    [SerializeField] private UnityEvent onInteract;

    private InputAction interactAction;
    private int playerCollidersInside;
    private bool used;

    private bool CanUse => playerCollidersInside > 0 && !(onlyOnce && used);

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Awake()
    {
        interactAction = new InputAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonNorth");
        UpdatePrompt();
    }

    private void OnEnable() => interactAction.Enable();
    private void OnDisable() => interactAction.Disable();
    private void OnDestroy() => interactAction.Dispose();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!KTUtils.IsPlayer(other)) return;
        playerCollidersInside++;
        UpdatePrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!KTUtils.IsPlayer(other)) return;
        playerCollidersInside = Mathf.Max(0, playerCollidersInside - 1);
        UpdatePrompt();
    }

    private void Update()
    {
        if (!CanUse || !interactAction.WasPressedThisFrame()) return;

        used = true;
        KTUtils.PlaySound(sound);
        onInteract.Invoke();
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        if (prompt != null) prompt.SetActive(CanUse);
    }
}
