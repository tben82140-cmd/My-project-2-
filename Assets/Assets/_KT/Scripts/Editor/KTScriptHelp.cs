using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Карточка-описание одного скрипта КТ (или инструмента, или понятия).</summary>
public class KTScriptCard
{
    public string Type;        // имя класса скрипта
    public string Title;       // коротко по-русски
    public string Group;       // раздел в справочнике
    public string Variant;     // «А и Б», «А», «Б»
    public string Summary;     // что делает — 1–2 предложения
    public string PutOn;       // куда вешать
    public string Needs;       // что нужно рядом
    public string[] Fields;    // «Поле — что делает»
    public string WorksWith;   // с чем сочетается
    public bool IsScript = true;
}

/// <summary>Все описания скриптов КТ. Показываются в инспекторе и в окне KT → Справочник скриптов.</summary>
public static class KTScriptHelp
{
    public static readonly KTScriptCard[] Cards =
    {
        // ============================== 1. Игрок ==============================
        new KTScriptCard
        {
            Type = "PlatformerMovement2D", Title = "Управление в платформере", Group = "1. Игрок", Variant = "А",
            Summary = "Бег влево-вправо и прыжок (A/D и пробел). Умеет двойной прыжок, «время койота» и ездить на движущихся платформах. " +
                      "Двигает героя только силами, поэтому батут и толчки работают поверх управления.",
            PutOn = "Игрок.",
            Needs = "Rigidbody 2D (Dynamic), обычный коллайдер, тег Player.",
            Fields = new[]
            {
                "Max Speed — скорость бега.",
                "Jump Height — высота прыжка в клетках.",
                "Extra Jumps — сколько раз можно подпрыгнуть ещё в воздухе (1 = двойной прыжок).",
                "Air Control — насколько можно рулить в полёте.",
                "Fall Gravity Multiplier — насколько падение резче подъёма."
            },
            WorksWith = "PlayerAnimator2D, PlayerHealth2D, PlayerAttack2D, MovingPlatform2D."
        },
        new KTScriptCard
        {
            Type = "TopDownMovement2D", Title = "Управление при виде сверху", Group = "1. Игрок", Variant = "Б",
            Summary = "Ходьба в 8 направлений (WASD) и рывок на Shift. Умеет кататься на плотах. " +
                      "Двигает героя силами, поэтому течение ручья сносит его поверх управления.",
            PutOn = "Игрок.",
            Needs = "Rigidbody 2D (Dynamic), коллайдер в районе ног, тег Player.",
            Fields = new[]
            {
                "Move Speed — скорость ходьбы.",
                "Dash Enabled — включить рывок.",
                "Dash Power — сила рывка.",
                "Dash Cooldown — пауза между рывками."
            },
            WorksWith = "PlayerAnimator2D, PlayerHealth2D, PlayerAttack2D, MovingPlatform2D (плот), ElevationTrigger2D (лестницы)."
        },
        new KTScriptCard
        {
            Type = "PlayerAnimator2D", Title = "Связь движения с анимацией", Group = "1. Игрок", Variant = "А и Б",
            Summary = "«Переводчик» между движением и Animator. Каждый кадр сообщает аниматору, с какой скоростью идёт герой, стоит ли он на земле и куда смотрит, " +
                      "и разворачивает спрайт влево-вправо. Своих анимаций в нём нет.",
            PutOn = "Игрок. Обычно его добавляет мастер KT → Мастер аниматора.",
            Needs = "Animator на игроке или его дочернем объекте.",
            Fields = new[]
            {
                "Animator — какой аниматор управлять (находится сам).",
                "Flip Sprite — зеркалить спрайт при движении влево.",
                "Sprite Faces Right — в какую сторону смотрит герой на исходной картинке."
            },
            WorksWith = "Любой скрипт движения. Параметры аниматора: Speed, VerticalSpeed, IsGrounded, MoveX, MoveY."
        },
        new KTScriptCard
        {
            Type = "PlayerHealth2D", Title = "Жизни игрока", Group = "1. Игрок", Variant = "А и Б",
            Summary = "Хранит жизни. При уроне игрок мигает и ненадолго неуязвим, его отбрасывает. При смерти появляется на последнем чекпоинте. " +
                      "Сам урон не наносит — его наносят опасности.",
            PutOn = "Игрок.",
            Needs = "Rigidbody 2D. Для текста жизней — Text - TextMeshPro на Canvas.",
            Fields = new[]
            {
                "Max Health — сколько жизней.",
                "Invulnerability Time — сколько секунд мигает после удара.",
                "Knockback Force — сила отбрасывания.",
                "Restart Scene On Death — перезапускать уровень вместо чекпоинта.",
                "Health Label — текст, где показываются жизни.",
                "On Damaged / On Died / On Respawned — события: что сделать при уроне, смерти, появлении."
            },
            WorksWith = "Hazard2D и DeathZone2D (наносят урон), Checkpoint2D (точка появления)."
        },
        new KTScriptCard
        {
            Type = "PlayerAttack2D", Title = "Удар", Group = "1. Игрок", Variant = "А и Б",
            Summary = "Удар по кнопке J или левой кнопке мыши. Бьёт всё, на чём есть Damageable2D, в ту сторону, куда смотрит герой. " +
                      "Сам запускает анимацию удара, если она есть в аниматоре.",
            PutOn = "Игрок. Мастер аниматора добавляет его сам, если включить «Есть удар».",
            Needs = "Rigidbody 2D. Для анимации — триггер Attack в аниматоре.",
            Fields = new[]
            {
                "Damage — урон за удар.",
                "Attack Reach / Attack Radius — где и какого размера зона удара (красный круг в окне Scene).",
                "Attack Cooldown — пауза между ударами.",
                "Hit Delay — через сколько секунд после нажатия проходит урон (под кадр анимации).",
                "Movement While Attacking — что с бегом во время удара: свободно, медленно или стоп (на земле; в прыжке полёт не мешает)."
            },
            WorksWith = "Damageable2D — то, что можно ударить."
        },

        // ============================== 2. Монеты ==============================
        new KTScriptCard
        {
            Type = "CoinPickup", Title = "Сбор монеты", Group = "2. Монеты", Variant = "А и Б",
            Summary = "Когда игрок касается зоны, монета засчитывается ровно один раз и исчезает. " +
                      "Если монета упала в смертельную зону, она возвращается на место.",
            PutOn = "Дочерний объект монеты (PickupZone), а не сама монета.",
            Needs = "Коллайдер с Is Trigger на том же объекте. У самой монеты — Rigidbody 2D и обычный коллайдер.",
            Fields = new[]
            {
                "Coin Root — что удалить при сборе (по умолчанию родитель).",
                "Pickup Effect Prefab — эффект при сборе.",
                "Pickup Sound — звук при сборе."
            },
            WorksWith = "CoinCounter (считает), CoinBurst (высыпает), DeathZone2D (возвращает упавшие)."
        },
        new KTScriptCard
        {
            Type = "CoinCounter", Title = "Счётчик монет", Group = "2. Монеты", Variant = "А и Б",
            Summary = "Считает монеты на уровне (они регистрируются сами) и пишет «Монеты: 3/8». " +
                      "Когда собраны все — показывает надпись и запускает событие.",
            PutOn = "Canvas. Один на сцену.",
            Needs = "Text - TextMeshPro для вывода.",
            Fields = new[]
            {
                "Label — текст, куда писать счёт.",
                "Format — как писать: {0} — собрано, {1} — всего.",
                "Win Object — что показать, когда собраны все.",
                "On All Coins Collected — событие «всё собрано»: открыть дверь, остановить таймер, следующий уровень."
            },
            WorksWith = "CoinPickup, Door2D, LevelTimer, SceneLoader."
        },
        new KTScriptCard
        {
            Type = "CoinBurst", Title = "Сундук с монетами", Group = "2. Монеты", Variant = "А и Б",
            Summary = "Высыпает монеты: при виде сверху — во все стороны, в платформере — фонтаном вверх. " +
                      "Может сработать сразу при старте или по команде (например, по кнопке E).",
            PutOn = "Сундук.",
            Needs = "Префаб монеты с CoinPickup.",
            Fields = new[]
            {
                "Coin Prefab — какую монету выбрасывать.",
                "Coin Count — сколько монет.",
                "Spawn Point — откуда вылетают (поставьте перед сундуком).",
                "Burst On Start — высыпать сразу при запуске.",
                "Opened Sprite — картинка открытого сундука."
            },
            WorksWith = "Interactable2D → Burst (открыть по кнопке E)."
        },

        // ============================== 3. Уровень ==============================
        new KTScriptCard
        {
            Type = "MovingPlatform2D", Title = "Движущаяся платформа", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Ездит по точкам маршрута. С обычным коллайдером — платформа, на которой стоят (А), или толкающее препятствие (Б). " +
                      "С коллайдером Is Trigger — плот, который возит игрока (Б).",
            PutOn = "Платформа.",
            Needs = "Коллайдер. Rigidbody 2D добавится сам.",
            Fields = new[]
            {
                "Waypoints — точки маршрута (пустые объекты).",
                "Speed — скорость.",
                "Wait Time — пауза в каждой точке.",
                "Loop — по кругу или туда-обратно."
            },
            WorksWith = "DeathZone2D (плот над пропастью или водой)."
        },
        new KTScriptCard
        {
            Type = "FallingPlatform2D", Title = "Рассыпающаяся платформа", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Встал на неё — трясётся и падает (А) или проваливается (Б). Через время возвращается.",
            PutOn = "Платформа или плитка пола.",
            Needs = "Коллайдер: обычный — для А, Is Trigger поверх смертельной зоны — для Б.",
            Fields = new[]
            {
                "Fall Delay — сколько трясётся перед падением.",
                "Shake Strength — сила тряски.",
                "Fall Gravity — скорость падения (А).",
                "Respawn Time — через сколько вернётся (0 — никогда)."
            },
            WorksWith = "DeathZone2D под ней."
        },
        new KTScriptCard
        {
            Type = "DeathZone2D", Title = "Смертельная зона", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Игрок, попавший сюда, сразу погибает: пропасть, вода, лава. На плоту или полу над зоной — безопасно. " +
                      "Упавшие монеты возвращаются на место, враги погибают сразу (без анимации смерти), ящики исчезают.",
            PutOn = "Объект с коллайдером или Tilemap.",
            Needs = "Коллайдер с Is Trigger (ставится сам).",
            Fields = new[]
            {
                "Safe On Platforms — не трогать игрока на плоту или полу.",
                "Return Coins — возвращать упавшие монеты.",
                "Destroy Enemies — упавшие враги погибают (On Died у Damageable2D срабатывает).",
                "Destroy Other Objects — упавшие ящики и прочее исчезают."
            },
            WorksWith = "PlayerHealth2D и Checkpoint2D (где появиться), MovingPlatform2D и FallingPlatform2D (по чему пройти)."
        },
        new KTScriptCard
        {
            Type = "Hazard2D", Title = "Опасность", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Отнимает жизни при касании: шипы, пила, враг. Врага можно победить прыжком на голову. " +
                      "Если у врага есть анимация удара, она играет в момент, когда он задевает игрока.",
            PutOn = "Шипы, враг, Tilemap с шипами.",
            Needs = "Коллайдер (обычный или Is Trigger). У игрока — PlayerHealth2D.",
            Fields = new[]
            {
                "Damage — сколько жизней отнимает.",
                "Instant Kill — убивает сразу.",
                "Can Be Stomped — можно победить прыжком сверху.",
                "Stomp Bounce — как высоко подлетает игрок после прыжка на голову."
            },
            WorksWith = "EnemyPatrol2D и EnemyChaser2D (враги, которые ранят)."
        },
        new KTScriptCard
        {
            Type = "Checkpoint2D", Title = "Чекпоинт", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Когда игрок проходит через него, это место становится точкой появления после смерти.",
            PutOn = "Флажок, костёр, статуя.",
            Needs = "Коллайдер с Is Trigger. У игрока — PlayerHealth2D.",
            Fields = new[]
            {
                "Respawn Offset — где появится игрок (зелёный кружок в окне Scene).",
                "Active Sprite — картинка активного чекпоинта.",
                "Active Effect — что включить при активации."
            },
            WorksWith = "PlayerHealth2D, DeathZone2D."
        },
        new KTScriptCard
        {
            Type = "Door2D", Title = "Дверь", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Дверь, ворота, решётка. Сама не открывается — её открывают другие скрипты через события: Open, Close, Toggle.",
            PutOn = "Дверь (спрайт или Tilemap).",
            Needs = "Обычный коллайдер — чтобы закрытая дверь не пускала.",
            Fields = new[]
            {
                "Starts Open — открыта с начала.",
                "Open Sprite — картинка открытой двери (если пусто — дверь исчезает).",
                "On Opened / On Closed — события при открытии и закрытии."
            },
            WorksWith = "CoinCounter (открыть, когда собраны все монеты), TriggerEvent2D (ключ), Interactable2D (рычаг)."
        },
        new KTScriptCard
        {
            Type = "Teleporter2D", Title = "Телепорт", Group = "3. Уровень", Variant = "А и Б",
            Summary = "Переносит игрока в точку назначения. Два телепорта, указанные друг на друга, — пара порталов.",
            PutOn = "Портал, труба, дверь в комнату.",
            Needs = "Коллайдер с Is Trigger.",
            Fields = new[]
            {
                "Destination — куда переносит.",
                "Keep Velocity — сохранять скорость после переноса."
            },
            WorksWith = "Другой Teleporter2D."
        },
        new KTScriptCard
        {
            Type = "ElevationTrigger2D", Title = "Лестница между уровнями высоты", Group = "3. Уровень", Variant = "Б",
            Summary = "Зона лестницы на холм. На лестнице игрок «наверху», сошёл на холм — остался наверху, сошёл на землю — вниз. " +
                      "Меняет, с какими стенами игрок сталкивается и поверх чего рисуется.",
            PutOn = "Ставит окно KT → Уровни высоты само.",
            Needs = "Подготовленный проект (Layers Level1–Level3).",
            Fields = new[]
            {
                "Layer / Sorting Layer Above — уровень наверху.",
                "Layer / Sorting Layer Below — уровень внизу.",
                "Hill Top — Tilemap верха холма.",
                "Log Level Changes — писать смены уровня в Console."
            },
            WorksWith = "Окно KT → Уровни высоты."
        },

        // ============================== 4. Враги ==============================
        new KTScriptCard
        {
            Type = "EnemyPatrol2D", Title = "Враг-патрульный", Group = "4. Враги", Variant = "А и Б",
            Summary = "Ходит туда-обратно по точкам маршрута и разворачивается. Урон сам не наносит. " +
                      "Вместе с EnemyChaser2D — патрулирует, пока не заметит игрока.",
            PutOn = "Враг.",
            Needs = "Rigidbody 2D (станет Kinematic сам). Для урона — Hazard2D.",
            Fields = new[]
            {
                "Waypoints — точки маршрута.",
                "Speed — скорость.",
                "Wait Time — пауза в каждой точке."
            },
            WorksWith = "Hazard2D (ранит), Damageable2D (можно ударить)."
        },
        new KTScriptCard
        {
            Type = "EnemyChaser2D", Title = "Враг-преследователь", Group = "4. Враги", Variant = "А и Б",
            Summary = "Замечает игрока и бежит за ним, потерял — возвращается на место. В платформере бегает только влево-вправо. " +
                      "Вместе с EnemyPatrol2D: патрулирует, пока не заметит, потом гонится.",
            PutOn = "Враг.",
            Needs = "Rigidbody 2D (Dynamic, Freeze Rotation Z). У игрока — тег Player. Для урона — Hazard2D.",
            Fields = new[]
            {
                "Detect Radius — с какого расстояния замечает (жёлтый круг, видно, когда враг выделен).",
                "Lose Radius — на каком теряет (серый круг).",
                "Needs Line Of Sight — не видит игрока сквозь стены.",
                "Alert Indicator — что показать, пока гонится (например, «!» над головой).",
                "Move Speed — скорость погони."
            },
            WorksWith = "EnemyPatrol2D (патруль + погоня), Hazard2D, Damageable2D."
        },
        new KTScriptCard
        {
            Type = "Damageable2D", Title = "То, что можно ударить", Group = "4. Враги", Variant = "А и Б",
            Summary = "Враги, ящики, горшки, кусты. От удара вспыхивает красным и отлетает, без жизней исчезает и может что-то выронить.",
            PutOn = "Враг или разрушаемый предмет.",
            Needs = "Коллайдер.",
            Fields = new[]
            {
                "Max Health — сколько ударов выдерживает.",
                "Knockback Force — отбрасывание от удара.",
                "Death Animation Time — сколько играть анимацию смерти перед исчезновением (0 — сразу).",
                "Drop Prefab — что выпадет (сердечко, ключ).",
                "On Damaged / On Died — события."
            },
            WorksWith = "PlayerAttack2D — тот, кто бьёт."
        },

        new KTScriptCard
        {
            Type = "EnemyAnimator2D", Title = "Связь движения врага с анимацией", Group = "4. Враги", Variant = "А и Б",
            Summary = "«Переводчик» для врага: сообщает аниматору, как быстро и куда он движется, гонится ли он, и разворачивает спрайт. " +
                      "Скорость считает по тому, как враг реально сдвинулся, поэтому работает с патрулём, погоней и отбрасыванием.",
            PutOn = "Враг. Обычно его добавляет мастер KT → Мастер аниматора в режиме «Враг».",
            Needs = "Animator на враге или его дочернем объекте.",
            Fields = new[]
            {
                "Flip Sprite — зеркалить спрайт при движении влево.",
                "Sprite Faces Right — в какую сторону смотрит враг на исходной картинке."
            },
            WorksWith = "EnemyPatrol2D, EnemyChaser2D. Удар запускает Hazard2D, урон и смерть — Damageable2D."
        },

        // ============================== 5. Триггеры ==============================
        new KTScriptCard
        {
            Type = "TriggerEvent2D", Title = "«Зашёл сюда — сделай это»", Group = "5. Триггеры", Variant = "А и Б",
            Summary = "Универсальный триггер. Что сделать — выбирается мышкой в событиях. " +
                      "Ключ, табличка, финиш, нажимная плита, сердечко — всё это он.",
            PutOn = "Любой объект-зона.",
            Needs = "Коллайдер с Is Trigger.",
            Fields = new[]
            {
                "Trigger Once — сработать один раз.",
                "Destroy After Trigger — исчезнуть после срабатывания.",
                "On Player Enter — что сделать, когда игрок зашёл.",
                "On Player Exit — что сделать, когда вышел."
            },
            WorksWith = "Door2D, PlayerHealth2D.Heal, SceneLoader, любой объект через SetActive."
        },
        new KTScriptCard
        {
            Type = "Interactable2D", Title = "«Подойди и нажми E»", Group = "5. Триггеры", Variant = "А и Б",
            Summary = "Когда игрок рядом, показывает подсказку. По кнопке E выполняет событие: открыть сундук, дёрнуть рычаг, поговорить.",
            PutOn = "Сундук, рычаг, NPC.",
            Needs = "Коллайдер с Is Trigger чуть больше объекта.",
            Fields = new[]
            {
                "Prompt — подсказка «E» над объектом.",
                "Only Once — сработать один раз.",
                "On Interact — что сделать по нажатию."
            },
            WorksWith = "CoinBurst.Burst, Door2D.Toggle, панель диалога через SetActive."
        },

        // ============================== 6. Камера ==============================
        new KTScriptCard
        {
            Type = "CameraFollow2D", Title = "Камера за игроком", Group = "6. Камера", Variant = "А и Б",
            Summary = "Плавно следует за игроком (он находится сам), трясётся от ударов, не показывает пустоту за краем уровня.",
            PutOn = "Main Camera.",
            Needs = "Ничего.",
            Fields = new[]
            {
                "Offset — сдвиг камеры от игрока.",
                "Smooth Time — плавность.",
                "Level Bounds — Box Collider 2D с границами уровня."
            },
            WorksWith = "ParallaxLayer2D."
        },
        new KTScriptCard
        {
            Type = "ParallaxLayer2D", Title = "Параллакс фона", Group = "6. Камера", Variant = "А",
            Summary = "Дальний фон едет медленнее уровня — появляется глубина. С галочкой Infinite Horizontal фон бесконечный: " +
                      "копии слева и справа скрипт ставит сам при запуске.",
            PutOn = "Один раз на каждый слой фона — на объект с картинкой. Не на каждую копию.",
            Needs = "Sprite Renderer на объекте или у его детей.",
            Fields = new[]
            {
                "Parallax Factor — 0: стоит с уровнем, 1: едет с камерой. Небо ≈ 0.9, горы ≈ 0.7.",
                "Infinite Horizontal — бесконечный фон: копии по бокам появляются сами, слой незаметно перескакивает.",
                "Copies Each Side — сколько копий с каждой стороны (0 — посчитать по ширине экрана).",
                "Repeat Width Override — ширина одной картинки, если скрипт не угадал сам."
            },
            WorksWith = "CameraFollow2D."
        },

        // ============================== 7. Интерфейс ==============================
        new KTScriptCard
        {
            Type = "SceneLoader", Title = "Перезапуск и уровни", Group = "7. Интерфейс", Variant = "А и Б",
            Summary = "Перезапуск по R, следующий уровень, выход. Его методы удобно вызывать из кнопок и событий.",
            PutOn = "Canvas или любой объект.",
            Needs = "Сцены в списке File → Build Profiles → Scene List.",
            Fields = new[]
            {
                "Restart With R — перезапуск клавишей R.",
                "Методы: RestartScene, LoadNextScene, LoadScene, QuitGame."
            },
            WorksWith = "Кнопки UI, TriggerEvent2D (финиш), CoinCounter."
        },
        new KTScriptCard
        {
            Type = "PauseMenu", Title = "Пауза", Group = "7. Интерфейс", Variant = "А и Б",
            Summary = "Escape ставит игру на паузу и показывает панель. Кнопка «Продолжить» вызывает Resume.",
            PutOn = "Canvas.",
            Needs = "Панель паузы (выключенная).",
            Fields = new[] { "Pause Panel — панель меню паузы." },
            WorksWith = "SceneLoader (кнопки «Заново», «Выйти»)."
        },
        new KTScriptCard
        {
            Type = "LevelTimer", Title = "Секундомер", Group = "7. Интерфейс", Variant = "А и Б",
            Summary = "Показывает время на уровне. Останавливается командой StopTimer — например, когда собраны все монеты.",
            PutOn = "Canvas.",
            Needs = "Text - TextMeshPro.",
            Fields = new[]
            {
                "Label — текст для времени.",
                "Start On Awake — запускать сразу."
            },
            WorksWith = "CoinCounter → On All Coins Collected → StopTimer."
        },

        new KTScriptCard
        {
            Type = "WinScreen2D", Title = "Экран победы", Group = "7. Интерфейс", Variant = "А и Б",
            Summary = "Финал уровня: показывает «Уровень пройден!» со статистикой (монеты, время), останавливает героя и мир, " +
                      "играет звук. Победа — когда собраны все монеты или по команде (флажок финиша).",
            PutOn = "Canvas. Проще всего — кнопкой меню KT → Создать экран победы.",
            Needs = "Панель экрана победы. Для статистики — CoinCounter и LevelTimer на сцене.",
            Fields = new[]
            {
                "Win Condition — когда победа: собраны все монеты или по команде (метод Win).",
                "Panel / Stats Label — что показать и куда написать статистику.",
                "Freeze Player / Stop World — остановить героя и весь мир.",
                "Win Sound / Celebration Effect — звук и эффект победы.",
                "Auto Next Level Delay — сам перейти на следующий уровень через N секунд (0 — ждать кнопку)."
            },
            WorksWith = "CoinCounter, LevelTimer, SceneLoader (кнопки), TriggerEvent2D (флажок финиша → Win)."
        },

        // ============================== Меню KT ==============================
        new KTScriptCard
        {
            Type = "KTAnimatorWizard", Title = "Мастер аниматора", Group = "Меню KT", Variant = "А и Б", IsScript = false,
            Summary = "Из ваших клипов или кадров сам собирает Animator со всеми переходами для игрока или врага: " +
                      "стоит, бежит, прыжок, падение, удар, урон, смерть, направления.",
            PutOn = "Меню KT → Мастер аниматора.",
            Needs = "Персонаж на сцене и хотя бы анимация «Стоит».",
            WorksWith = "PlayerAnimator2D, PlayerAttack2D, EnemyAnimator2D, Damageable2D."
        },
        new KTScriptCard
        {
            Type = "KTHeightLevels", Title = "Уровни высоты", Group = "Меню KT", Variant = "Б", IsScript = false,
            Summary = "Холмы с лестницами: сам настраивает проект, строит стены для игрока внизу и наверху и зоны лестниц.",
            PutOn = "Меню KT → Уровни высоты (холмы и лестницы).",
            Needs = "Холм на трёх Tilemap: верх, обрыв, лестницы.",
            WorksWith = "ElevationTrigger2D."
        },
        new KTScriptCard
        {
            Type = "KTWinScreenBuilder", Title = "Создать экран победы", Group = "Меню KT", Variant = "А и Б", IsScript = false,
            Summary = "Одной кнопкой собирает на Canvas простой экран победы из стандартных элементов Unity (Panel, тексты, кнопки) " +
                      "с уже подключёнными кнопками «Заново» и «Дальше». Оформление — на ваш вкус.",
            PutOn = "Меню KT → Создать экран победы.",
            Needs = "Ничего: Canvas и EventSystem создадутся сами, если их нет.",
            WorksWith = "WinScreen2D, SceneLoader."
        },
        new KTScriptCard
        {
            Type = "KTSceneChecker", Title = "Проверка сцены", Group = "Меню KT", Variant = "А и Б", IsScript = false,
            Summary = "Ищет типичные ошибки в сцене и показывает, где они.",
            PutOn = "Меню KT → Проверить сцену.",
            Needs = "Ничего."
        },

        // ============================== Как это связано ==============================
        new KTScriptCard
        {
            Type = "Events", Title = "События (On … )", Group = "Как это связано", Variant = "А и Б", IsScript = false,
            Summary = "Поля вида On Player Enter, On Interact, On All Coins Collected — это события. Через них скрипты «нажимают кнопки» друг у друга " +
                      "без кода: «когда игрок зашёл в триггер — открой дверь».",
            PutOn = "В событии нажмите +, перетащите в пустое поле объект (например, дверь) и выберите справа скрипт и действие (Door2D → Open).",
            Needs = "Объект, у которого есть нужный скрипт.",
            WorksWith = "TriggerEvent2D, Interactable2D, CoinCounter, Door2D, PlayerHealth2D, Damageable2D."
        },
        new KTScriptCard
        {
            Type = "Interfaces", Title = "Служебные файлы", Group = "Как это связано", Variant = "А и Б", IsScript = false,
            Summary = "KTUtils и KTBehaviour в папке 0_Core — общие помощники для всех скриптов. Ни на что не вешаются, но должны лежать в проекте.",
            PutOn = "Никуда.",
            Needs = "Ничего."
        }
    };

    private static Dictionary<string, KTScriptCard> byType;

    public static KTScriptCard Find(string typeName)
    {
        if (byType == null)
        {
            byType = new Dictionary<string, KTScriptCard>();
            foreach (KTScriptCard card in Cards) byType[card.Type] = card;
        }
        return byType.TryGetValue(typeName, out KTScriptCard found) ? found : null;
    }

    // ============================== Отрисовка ==============================

    private static GUIStyle headingStyle;
    private static GUIStyle textStyle;

    private static void EnsureStyles()
    {
        if (headingStyle != null) return;
        headingStyle = new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true };
        textStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false };
    }

    /// <summary>Подробности карточки: куда вешать, что нужно, поля, с чем работает.</summary>
    public static void DrawDetails(KTScriptCard card)
    {
        EnsureStyles();
        Section(card.IsScript ? "Куда вешать" : "Где найти", card.PutOn);
        Section("Что нужно", card.Needs);

        if (card.Fields != null && card.Fields.Length > 0)
        {
            EditorGUILayout.LabelField("Главные поля", headingStyle);
            foreach (string field in card.Fields)
            {
                EditorGUILayout.LabelField("• " + field, textStyle);
            }
            EditorGUILayout.Space(2);
        }

        Section("Работает вместе с", card.WorksWith);
    }

    private static void Section(string heading, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        EditorGUILayout.LabelField(heading, headingStyle);
        EditorGUILayout.LabelField(text, textStyle);
        EditorGUILayout.Space(2);
    }
}
