using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Tutorial corto de la primera partida (GDD 10.5): unas tarjetas, una por idea, con el juego en pausa.
/// Cada tarjeta señala con un anillo que late el control del HUD del que habla (joystick, botones,
/// barra de smog). Se avanza con un toque, clic, Espacio o A del mando, y se puede saltar.
/// Solo sale una vez (PlayerPrefs); <see cref="ResetSeen"/> lo vuelve a activar.
/// Construye su interfaz por código dentro del UIDocument del HUD; los estilos están en GameHUD.uss.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TutorialController : MonoBehaviour
{
    private const string PrefSeen = "tutorial.seen";

    [Tooltip("Mostrarlo aunque ya se haya visto (para probarlo en el Editor).")]
    [SerializeField] private bool _alwaysShow;
    [Tooltip("Segundos antes de la primera tarjeta, para que se vea la ciudad.")]
    [SerializeField] private float _startDelay = 0.6f;

    private struct Step
    {
        public string Title;
        public string Touch;
        public string Keys;
        public string Target; // nombre del elemento a señalar (puede estar en otro UIDocument del mismo panel)
    }

    private static readonly Step[] Steps =
    {
        new Step
        {
            Title = "¡Hola, piloto!",
            Touch = "La ciudad está llena de smog. Tu dron Buster-1 puede limpiarla.",
            Keys = "La ciudad está llena de smog. Tu dron Buster-1 puede limpiarla.",
        },
        new Step
        {
            Title = "Vuela",
            Touch = "Joystick arriba para avanzar y a los lados para girar. Usa ▲ y ▼ para subir y bajar.",
            Keys = "W y S avanzan y retroceden, A y D giran. Sube con Espacio y baja con Shift.",
            Target = "touch-zone-left",
        },
        new Step
        {
            Title = "Rayo Purificador",
            Touch = "Apunta a una nube de smog y mantén pulsado RAYO para deshacerla.",
            Keys = "Apunta a una nube de smog y mantén el clic izquierdo para deshacerla.",
            Target = "btn-beam",
        },
        new Step
        {
            Title = "Semillas",
            Touch = "Busca los puntos verdes y pulsa SEMILLA. Los árboles limpian el aire solos.",
            Keys = "Busca los puntos verdes y haz clic derecho. Los árboles limpian el aire solos.",
            Target = "btn-seed",
        },
        new Step
        {
            Title = "Tu misión",
            Touch = "Baja el smog a 0% antes de que acabe el tiempo. ¡Necesitas el rayo y las semillas!",
            Keys = "Baja el smog a 0% antes de que acabe el tiempo. ¡Necesitas el rayo y las semillas!",
            Target = "smog-meter",
        },
    };

    private VisualElement _root;
    private VisualElement _overlay;
    private VisualElement _ring;
    private Label _title;
    private Label _body;
    private Label _counter;
    private Button _next;
    private Button _skip;
    private IVisualElementScheduledItem _pulse;
    private int _step = -1;
    private float _openAt = -1f;

    public bool IsOpen => _step >= 0;

    /// <summary>Hace que el tutorial vuelva a salir en la próxima partida.</summary>
    public static void ResetSeen() => PlayerPrefs.DeleteKey(PrefSeen);

    private void OnEnable()
    {
        _root = GetComponent<UIDocument>().rootVisualElement;
        if (!_alwaysShow && PlayerPrefs.GetInt(PrefSeen, 0) == 1) return;
        Build();
        _openAt = Time.realtimeSinceStartup + _startDelay;
    }

    private void OnDisable()
    {
        _pulse?.Pause();
        if (IsOpen) Time.timeScale = 1f;
    }

    private void Update()
    {
        if (_openAt > 0f && Time.realtimeSinceStartup >= _openAt)
        {
            _openAt = -1f;
            Show(0);
        }
        if (!IsOpen) return;

        // El juego espera mientras se lee (la pausa también pone timeScale; aquí se mantiene a 0)
        Time.timeScale = 0f;
        PlaceRing();

        bool keyNext = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                       || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        if (keyNext) Next();
    }

    private void Build()
    {
        _overlay = new VisualElement { name = "tutorial-overlay" };
        _overlay.AddToClassList("tutorial-overlay");
        _overlay.style.display = DisplayStyle.None;
        // Un toque en cualquier parte avanza
        _overlay.RegisterCallback<PointerUpEvent>(_ => Next());

        _ring = new VisualElement { pickingMode = PickingMode.Ignore };
        _ring.AddToClassList("tutorial-ring");
        _overlay.Add(_ring);

        var card = new VisualElement();
        card.AddToClassList("tutorial-card");
        _counter = new Label();
        _counter.AddToClassList("tutorial-card__counter");
        _title = new Label();
        _title.AddToClassList("tutorial-card__title");
        _body = new Label();
        _body.AddToClassList("tutorial-card__body");

        var row = new VisualElement();
        row.AddToClassList("tutorial-card__row");
        _skip = new Button(Finish) { text = "SALTAR" };
        _skip.AddToClassList("menu-button");
        _skip.AddToClassList("menu-button--ghost");
        _skip.AddToClassList("tutorial-card__button");
        _next = new Button(Next) { text = "SIGUIENTE" };
        _next.AddToClassList("menu-button");
        _next.AddToClassList("menu-button--primary");
        _next.AddToClassList("tutorial-card__button");
        // Que el toque en los botones no cuente también como "toque en cualquier parte"
        _skip.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
        _next.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
        row.Add(_skip);
        row.Add(_next);

        card.Add(_counter);
        card.Add(_title);
        card.Add(_body);
        card.Add(row);
        _overlay.Add(card);

        // Encima de todo el HUD salvo el fundido
        VisualElement hudRoot = _root.Q<VisualElement>("game-hud-root") ?? _root;
        VisualElement fade = hudRoot.Q<VisualElement>("fade");
        if (fade != null) hudRoot.Insert(hudRoot.IndexOf(fade), _overlay);
        else hudRoot.Add(_overlay);

        _pulse = _ring.schedule.Execute(() => _ring.ToggleInClassList("tutorial-ring--pulse")).Every(450);
    }

    private void Show(int index)
    {
        _step = index;
        Step step = Steps[index];
        bool touch = Application.isMobilePlatform || Touchscreen.current != null;
        _counter.text = $"{index + 1} / {Steps.Length}";
        _title.text = step.Title;
        _body.text = touch ? step.Touch : step.Keys;
        _next.text = index == Steps.Length - 1 ? "¡A JUGAR!" : "SIGUIENTE";
        _overlay.style.display = DisplayStyle.Flex;
        PlaceRing();
    }

    private void Next()
    {
        if (!IsOpen) return;
        if (_step + 1 < Steps.Length) Show(_step + 1);
        else Finish();
    }

    private void Finish()
    {
        _step = -1;
        _overlay.style.display = DisplayStyle.None;
        Time.timeScale = 1f;
        PlayerPrefs.SetInt(PrefSeen, 1);
        PlayerPrefs.Save();
    }

    // Anillo alrededor del control del que habla la tarjeta (oculto si no está visible, p. ej. sin pantalla táctil)
    private void PlaceRing()
    {
        VisualElement target = FindTarget(Steps[_step].Target);
        bool visible = target != null && target.resolvedStyle.display != DisplayStyle.None
                       && target.worldBound.width > 1f && IsShown(target);
        _ring.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible) return;

        Rect bounds = _overlay.WorldToLocal(target.worldBound);
        const float pad = 18f;
        _ring.style.left = bounds.xMin - pad;
        _ring.style.top = bounds.yMin - pad;
        _ring.style.width = bounds.width + pad * 2f;
        _ring.style.height = bounds.height + pad * 2f;
    }

    private VisualElement FindTarget(string elementName)
    {
        if (string.IsNullOrEmpty(elementName) || _root.panel == null) return null;
        return _root.panel.visualTree.Q<VisualElement>(elementName);
    }

    private static bool IsShown(VisualElement element)
    {
        for (VisualElement e = element; e != null; e = e.parent)
        {
            if (e.resolvedStyle.display == DisplayStyle.None) return false;
        }
        return true;
    }
}
