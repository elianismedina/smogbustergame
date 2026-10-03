using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Pantalla de resultados al terminar la partida (<see cref="GameSession.Ended"/>):
/// victoria o derrota por saturación, por tiempo o por choque, con su causa y un consejo (GDD 4.4 y 10.5).
/// Permite reintentar el nivel o volver al menú principal.
/// Sin GameSession en la escena, solo reacciona al choque del dron.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameOverController : MonoBehaviour
{
    [SerializeField] private GameSession _session;
    [SerializeField] private QuadcopterCrash _player;
    [Tooltip("Opcional: la cámara deja de girar con el dron mientras cae.")]
    [SerializeField] private FollowCamera _followCamera;
    [Tooltip("Segundos entre el choque y la pantalla (para ver caer el dron).")]
    [SerializeField] private float _crashDelay = 1.2f;
    [Tooltip("Segundos entre el final por smog o tiempo y la pantalla.")]
    [SerializeField] private float _showDelay = 0.6f;
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField] private float _fadeDuration = 0.4f;
    [Tooltip("Efecto de sonido al perder (smog al 100%, tiempo agotado o choque).")]
    [SerializeField] private AudioClip _defeatSound;

    private VisualElement _overlay;
    private Label _title;
    private Label _subtitle;
    private Label _hint;
    private VisualElement _fade;
    private Button _btnRetry;
    private Button _btnMenu;
    private bool _isLoading;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _overlay = root.Q<VisualElement>("gameover-overlay");
        _fade = root.Q<VisualElement>("fade");
        _title = root.Q<Label>("gameover-title");
        _subtitle = root.Q<Label>("gameover-subtitle");
        _hint = root.Q<Label>("gameover-hint");
        _btnRetry = root.Q<Button>("btn-retry");
        _btnMenu = root.Q<Button>("btn-menu");

        if (_btnRetry != null) _btnRetry.clicked += OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked += OnMenuClicked;

        if (_session == null) _session = FindAnyObjectByType<GameSession>();
        if (_player == null) _player = FindAnyObjectByType<QuadcopterCrash>();
        if (_followCamera == null) _followCamera = FindAnyObjectByType<FollowCamera>();

        if (_session != null) _session.Ended += OnSessionEnded;
        else if (_player != null) _player.Crashed += OnPlayerCrashed;
        else Debug.LogWarning("GameOverController: no hay GameSession ni QuadcopterCrash en la escena.", this);
    }

    private void OnDisable()
    {
        if (_btnRetry != null) _btnRetry.clicked -= OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked -= OnMenuClicked;
        if (_session != null) _session.Ended -= OnSessionEnded;
        if (_player != null) _player.Crashed -= OnPlayerCrashed;
    }

    private void OnPlayerCrashed() => OnSessionEnded(GameSession.Result.Crash);

    private void OnSessionEnded(GameSession.Result result)
    {
        bool victory = result == GameSession.Result.Victory;
        if (!victory && _defeatSound != null) AudioManager.Instance?.PlayUi(_defeatSound);
        _overlay?.EnableInClassList("gameover-overlay--victory", victory);
        if (_btnRetry != null) _btnRetry.text = victory ? "JUGAR DE NUEVO" : "REINTENTAR";

        switch (result)
        {
            case GameSession.Result.Victory:
                int seconds = _session != null ? Mathf.CeilToInt(_session.TimeRemaining) : 0;
                SetTexts("¡AIRE PURO!",
                    $"Limpiaste el aire con {seconds / 60:00}:{seconds % 60:00} de sobra",
                    "Los árboles y jardines siguen limpiando el aire cada día.");
                break;
            case GameSession.Result.Saturation:
                SetTexts("¡ALERTA AMBIENTAL!",
                    "¡La contaminación llegó al 100%!",
                    "Consejo: el smog no para de subir. Actúa rápido en las nubes más densas.");
                break;
            case GameSession.Result.TimeUp:
                SetTexts("¡SE ACABÓ EL TIEMPO!",
                    "¡El tiempo se agotó!",
                    "Consejo: empieza por las nubes más grandes para bajar el smog antes.");
                break;
            default:
                if (_followCamera != null) _followCamera.FollowYaw = false;
                SetTexts("¡CHOCASTE!",
                    _player != null && _player.LastCrashReason == QuadcopterCrash.CrashReason.HardLanding
                        ? "Aterrizaje demasiado brusco"
                        : "¡El dron chocó contra un edificio!",
                    "Consejo: sube por encima de los edificios para cruzar la ciudad.");
                break;
        }

        StartCoroutine(ShowAfterDelay(result == GameSession.Result.Crash ? _crashDelay : _showDelay));
    }

    private void SetTexts(string title, string subtitle, string hint)
    {
        if (_title != null) _title.text = title;
        if (_subtitle != null) _subtitle.text = subtitle;
        if (_hint != null) _hint.text = hint;
    }

    private IEnumerator ShowAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        _overlay.style.display = DisplayStyle.Flex;
        // Esperar un frame para que la transición USS se aplique
        _overlay.schedule.Execute(() =>
        {
            _overlay.AddToClassList("gameover-overlay--visible");
            _btnRetry?.Focus();
        });
    }

    private void OnRetryClicked() => LoadScene(SceneManager.GetActiveScene().name);

    private void OnMenuClicked() => LoadScene(_menuSceneName);

    private void LoadScene(string sceneName)
    {
        if (_isLoading) return;
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        _isLoading = true;
        _btnRetry?.SetEnabled(false);
        _btnMenu?.SetEnabled(false);

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneName);
        load.allowSceneActivation = false;

        _fade?.AddToClassList("fade--visible");
        yield return new WaitForSecondsRealtime(_fadeDuration);

        while (load.progress < 0.9f)
        {
            yield return null;
        }

        load.allowSceneActivation = true;
    }
}
