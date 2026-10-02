using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Muestra la pantalla de derrota cuando el quadcopter se estrella
/// y permite reintentar el nivel o volver al menú principal.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameOverController : MonoBehaviour
{
    [SerializeField] private QuadcopterCrash _player;
    [Tooltip("Opcional: la cámara deja de girar con el dron mientras cae.")]
    [SerializeField] private FollowCamera _followCamera;
    [Tooltip("Segundos entre el choque y la pantalla de derrota (para ver caer el dron).")]
    [SerializeField] private float _showDelay = 1.2f;
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField] private float _fadeDuration = 0.4f;

    private VisualElement _overlay;
    private VisualElement _fade;
    private Button _btnRetry;
    private Button _btnMenu;
    private bool _isLoading;

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _overlay = root.Q<VisualElement>("gameover-overlay");
        _fade = root.Q<VisualElement>("fade");
        _btnRetry = root.Q<Button>("btn-retry");
        _btnMenu = root.Q<Button>("btn-menu");

        if (_btnRetry != null) _btnRetry.clicked += OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked += OnMenuClicked;

        if (_player == null) _player = FindAnyObjectByType<QuadcopterCrash>();
        if (_followCamera == null) _followCamera = FindAnyObjectByType<FollowCamera>();

        if (_player != null) _player.Crashed += OnPlayerCrashed;
        else Debug.LogWarning("GameOverController: no hay QuadcopterCrash en la escena.", this);
    }

    private void OnDisable()
    {
        if (_btnRetry != null) _btnRetry.clicked -= OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked -= OnMenuClicked;
        if (_player != null) _player.Crashed -= OnPlayerCrashed;
    }

    private void OnPlayerCrashed()
    {
        if (_followCamera != null) _followCamera.FollowYaw = false;
        StartCoroutine(ShowAfterDelay());
    }

    private IEnumerator ShowAfterDelay()
    {
        yield return new WaitForSeconds(_showDelay);

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
