using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Botón de pausa y menú de pausa del nivel: continuar, reintentar, menú principal y volúmenes.
/// Al pausar se detiene el tiempo (timeScale = 0) y los efectos de sonido (la música sigue).
/// También pausa al minimizar la app y con el botón "atrás" de Android / Escape.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField] private float _fadeDuration = 0.4f;

    private VisualElement _overlay;
    private VisualElement _fade;
    private Button _btnPause;
    private Button _btnResume;
    private Button _btnRetry;
    private Button _btnMenu;
    private Slider _sliderMusic;
    private Slider _sliderSfx;
    private Toggle _toggleVibration;

    private GameHudController _hud;
    private QuadcopterCrash _crash;
    private bool _isLoading;

    public bool IsPaused { get; private set; }

    private void OnEnable()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        _overlay = root.Q<VisualElement>("pause-overlay");
        _fade = root.Q<VisualElement>("fade");
        _btnPause = root.Q<Button>("btn-pause");
        _btnResume = root.Q<Button>("btn-resume");
        _btnRetry = root.Q<Button>("btn-pause-retry");
        _btnMenu = root.Q<Button>("btn-pause-menu");
        _sliderMusic = root.Q<Slider>("pause-slider-music");
        _sliderSfx = root.Q<Slider>("pause-slider-sfx");
        _toggleVibration = root.Q<Toggle>("pause-toggle-vibration");

        if (_btnPause != null) _btnPause.clicked += Pause;
        if (_btnResume != null) _btnResume.clicked += Resume;
        if (_btnRetry != null) _btnRetry.clicked += OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked += OnMenuClicked;

        _sliderMusic?.RegisterValueChangedCallback(OnMusicChanged);
        _sliderSfx?.RegisterValueChangedCallback(OnSfxChanged);
        _toggleVibration?.RegisterValueChangedCallback(OnVibrationChanged);

        _hud = GetComponent<GameHudController>();
        _crash = FindAnyObjectByType<QuadcopterCrash>();
        if (_crash != null) _crash.Crashed += OnCrashed;
    }

    private void OnDisable()
    {
        if (_btnPause != null) _btnPause.clicked -= Pause;
        if (_btnResume != null) _btnResume.clicked -= Resume;
        if (_btnRetry != null) _btnRetry.clicked -= OnRetryClicked;
        if (_btnMenu != null) _btnMenu.clicked -= OnMenuClicked;

        _sliderMusic?.UnregisterValueChangedCallback(OnMusicChanged);
        _sliderSfx?.UnregisterValueChangedCallback(OnSfxChanged);
        _toggleVibration?.UnregisterValueChangedCallback(OnVibrationChanged);

        if (_crash != null) _crash.Crashed -= OnCrashed;

        // Nunca dejar el juego congelado al salir de la escena
        if (IsPaused) SetPausedState(false);
    }

    private void Update()
    {
        // Botón "atrás" de Android (llega como Escape) o Escape en teclado
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    private void OnApplicationPause(bool paused)
    {
        // Al minimizar la app en el móvil, dejar el juego en pausa
        if (paused) Pause();
    }

    public void Pause()
    {
        if (IsPaused || _isLoading) return;
        if (_crash != null && _crash.HasCrashed) return;

        _hud?.ReleaseSticks();
        LoadSettings();
        SetPausedState(true);

        _overlay.style.display = DisplayStyle.Flex;
        // Las transiciones USS usan tiempo real, así que funcionan con timeScale = 0
        _overlay.schedule.Execute(() =>
        {
            _overlay.AddToClassList("overlay--visible");
            _btnResume?.Focus();
        });
    }

    public void Resume()
    {
        if (!IsPaused || _isLoading) return;

        GameSettings.Save();
        SetPausedState(false);
        _overlay.RemoveFromClassList("overlay--visible");
        _overlay.schedule.Execute(() => _overlay.style.display = DisplayStyle.None).StartingIn(200);
    }

    private void SetPausedState(bool paused)
    {
        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused; // la música y la UI ignoran la pausa (ver AudioManager)
    }

    private void OnCrashed()
    {
        // La pantalla de derrota sustituye al botón de pausa
        if (_btnPause != null) _btnPause.style.display = DisplayStyle.None;
    }

    #region Opciones

    private void LoadSettings()
    {
        _sliderMusic?.SetValueWithoutNotify(GameSettings.MusicVolume);
        _sliderSfx?.SetValueWithoutNotify(GameSettings.SfxVolume);
        _toggleVibration?.SetValueWithoutNotify(GameSettings.Vibration);
    }

    private void OnMusicChanged(ChangeEvent<float> evt)
    {
        GameSettings.MusicVolume = evt.newValue;
        AudioManager.Instance?.ApplyVolumes();
    }

    private void OnSfxChanged(ChangeEvent<float> evt)
    {
        GameSettings.SfxVolume = evt.newValue;
        AudioManager.Instance?.ApplyVolumes();
    }

    private void OnVibrationChanged(ChangeEvent<bool> evt) => GameSettings.Vibration = evt.newValue;

    #endregion

    #region Cambio de escena

    private void OnRetryClicked() => LoadScene(SceneManager.GetActiveScene().name);

    private void OnMenuClicked() => LoadScene(_menuSceneName);

    private void LoadScene(string sceneName)
    {
        if (_isLoading) return;
        GameSettings.Save();
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        _isLoading = true;
        _btnResume?.SetEnabled(false);
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

        // Restaurar tiempo y audio antes de activar la nueva escena
        SetPausedState(false);
        load.allowSceneActivation = true;
    }

    #endregion
}
