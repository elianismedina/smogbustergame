using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenuController : MonoBehaviour
{
    [Header("Configuración de Escena")]
    [SerializeField] private string _nextSceneName = "Level01";
    [SerializeField] private float _fadeDuration = 0.4f;

    [Header("Sonido")]
    [Tooltip("Se reproduce al pulsar cualquier botón del menú (grupo UI del mixer).")]
    [SerializeField] private AudioClip _selectSound;

    private VisualElement _root;
    private VisualElement _safeArea;
    private VisualElement _menu;
    private VisualElement _optionsOverlay;
    private VisualElement _levelsOverlay;
    private VisualElement _creditsOverlay;
    private VisualElement _fade;

    private Button _btnPlay;
    private Button _btnOptions;
    private Button _btnExit;
    private Button _btnOptionsBack;
    private Button _btnDifficulty;
    private Button _btnLevel1;
    private Button _btnLevelsBack;
    private Button _btnCredits;
    private Button _btnCreditsBack;
    private Label _level1Best;

    private Slider _sliderMusic;
    private Slider _sliderSfx;
    private Toggle _toggleVibration;
    private Toggle _toggleFixedJoystick;

    private MainMenuBackground _background;
    private Rect _lastSafeArea;
    private bool _isLoading;

    private void OnEnable()
    {
        // 1. Obtener la raíz del UXML
        UIDocument uiDocument = GetComponent<UIDocument>();
        _root = uiDocument.rootVisualElement;
        if (_root == null)
        {
            Debug.LogError("El UIDocument no tiene un Source Asset (UXML) asignado.", this);
            return;
        }

        // 2. Buscar elementos en la interfaz
        _safeArea = _root.Q<VisualElement>("safe-area");
        _menu = _root.Q<VisualElement>("menu");
        _optionsOverlay = _root.Q<VisualElement>("options-overlay");
        _levelsOverlay = _root.Q<VisualElement>("levels-overlay");
        _creditsOverlay = _root.Q<VisualElement>("credits-overlay");
        _fade = _root.Q<VisualElement>("fade");

        _btnPlay = _root.Q<Button>("btn-play");
        _btnOptions = _root.Q<Button>("btn-options");
        _btnExit = _root.Q<Button>("btn-exit");
        _btnOptionsBack = _root.Q<Button>("btn-options-back");
        _btnDifficulty = _root.Q<Button>("btn-difficulty");
        _btnLevel1 = _root.Q<Button>("btn-level-1");
        _btnLevelsBack = _root.Q<Button>("btn-levels-back");
        _btnCredits = _root.Q<Button>("btn-credits");
        _btnCreditsBack = _root.Q<Button>("btn-credits-back");
        _level1Best = _root.Q<Label>("level-1-best");
        // Los niveles 2 y 3 aún no existen
        _root.Q<Button>("btn-level-2")?.SetEnabled(false);
        _root.Q<Button>("btn-level-3")?.SetEnabled(false);

        _sliderMusic = _root.Q<Slider>("slider-music");
        _sliderSfx = _root.Q<Slider>("slider-sfx");
        _toggleVibration = _root.Q<Toggle>("toggle-vibration");
        _toggleFixedJoystick = _root.Q<Toggle>("toggle-fixed-joystick");

        // 3. Registrar listeners con comprobación previa
        RegisterButton(_btnPlay, OnPlayClicked, "btn-play");
        RegisterButton(_btnOptions, OnOptionsClicked, "btn-options");
        RegisterButton(_btnExit, OnExitClicked, "btn-exit");
        RegisterButton(_btnOptionsBack, CloseOptions, "btn-options-back");
        RegisterButton(_btnDifficulty, OnDifficultyClicked, "btn-difficulty");
        RegisterButton(_btnLevel1, OnLevel1Clicked, "btn-level-1");
        RegisterButton(_btnLevelsBack, CloseLevels, "btn-levels-back");
        RegisterButton(_btnCredits, OpenCredits, "btn-credits");
        RegisterButton(_btnCreditsBack, CloseCredits, "btn-credits-back");

        LoadOptions();
        _sliderMusic?.RegisterValueChangedCallback(OnMusicChanged);
        _sliderSfx?.RegisterValueChangedCallback(OnSfxChanged);
        _toggleVibration?.RegisterValueChangedCallback(OnVibrationChanged);
        _toggleFixedJoystick?.RegisterValueChangedCallback(OnFixedJoystickChanged);

        // 4. Ajustes por plataforma: iOS y WebGL no permiten cerrar la app
#if UNITY_IOS || UNITY_WEBGL
        if (_btnExit != null) _btnExit.style.display = DisplayStyle.None;
#endif

        Label versionLabel = _root.Q<Label>("version-label");
        if (versionLabel != null) versionLabel.text = $"v{Application.version}";

        // 5. Área segura (notch / barras del sistema)
        _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);

        // 6. Animación de entrada (la transición USS se dispara al añadir la clase)
        _menu?.schedule.Execute(() =>
        {
            _menu.AddToClassList("menu--visible");
            _btnPlay?.Focus();
        }).StartingIn(50);

        // 7. Fondo animado (humo, rayo, dron y logo)
        _background = new MainMenuBackground(_root);
    }

    private void OnDisable()
    {
        // Cancelar suscripción a eventos
        UnregisterButton(_btnPlay, OnPlayClicked);
        UnregisterButton(_btnOptions, OnOptionsClicked);
        UnregisterButton(_btnExit, OnExitClicked);
        UnregisterButton(_btnOptionsBack, CloseOptions);
        UnregisterButton(_btnDifficulty, OnDifficultyClicked);
        UnregisterButton(_btnLevel1, OnLevel1Clicked);
        UnregisterButton(_btnLevelsBack, CloseLevels);
        UnregisterButton(_btnCredits, OpenCredits);
        UnregisterButton(_btnCreditsBack, CloseCredits);

        _sliderMusic?.UnregisterValueChangedCallback(OnMusicChanged);
        _sliderSfx?.UnregisterValueChangedCallback(OnSfxChanged);
        _toggleVibration?.UnregisterValueChangedCallback(OnVibrationChanged);
        _toggleFixedJoystick?.UnregisterValueChangedCallback(OnFixedJoystickChanged);

        _root?.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        _background?.Dispose();
        _background = null;
    }

    private void Update()
    {
        _background?.Tick(Time.unscaledTime);

        // El área segura puede cambiar al rotar el dispositivo
        if (Screen.safeArea != _lastSafeArea)
        {
            ApplySafeArea();
        }

        // Botón "atrás" de Android (llega como Escape) o Escape en teclado
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnBackPressed();
        }
    }

    private void RegisterButton(Button button, System.Action callback, string buttonName)
    {
        if (button != null)
        {
            button.clicked += PlaySelectSound;
            button.clicked += callback;
        }
        else
        {
            Debug.LogWarning($"No se encontró un Button con el nombre '{buttonName}' en el UXML.");
        }
    }

    private void UnregisterButton(Button button, System.Action callback)
    {
        if (button != null)
        {
            button.clicked -= PlaySelectSound;
            button.clicked -= callback;
        }
    }

    private void PlaySelectSound() => AudioManager.Instance?.PlayUi(_selectSound);

    #region Área segura

    private void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        _lastSafeArea = Screen.safeArea;
        if (_safeArea == null || _root == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        // Convertir píxeles de pantalla a unidades del panel (PanelSettings escala la UI)
        float scaleX = _root.layout.width / Screen.width;
        float scaleY = _root.layout.height / Screen.height;
        if (float.IsNaN(scaleX) || float.IsNaN(scaleY))
        {
            return;
        }

        Rect area = Screen.safeArea;
        _safeArea.style.left = area.xMin * scaleX;
        _safeArea.style.right = (Screen.width - area.xMax) * scaleX;
        _safeArea.style.top = (Screen.height - area.yMax) * scaleY;
        _safeArea.style.bottom = area.yMin * scaleY;
    }

    #endregion

    #region Opciones

    private void LoadOptions()
    {
        _sliderMusic?.SetValueWithoutNotify(GameSettings.MusicVolume);
        _sliderSfx?.SetValueWithoutNotify(GameSettings.SfxVolume);
        _toggleVibration?.SetValueWithoutNotify(GameSettings.Vibration);
        _toggleFixedJoystick?.SetValueWithoutNotify(GameSettings.FixedJoystick);
        ShowDifficulty();
    }

    // Fácil -> Normal -> Difícil -> Fácil
    private void OnDifficultyClicked()
    {
        GameSettings.Difficulty = (GameSettings.DifficultyLevel)(((int)GameSettings.Difficulty + 1) % 3);
        ShowDifficulty();
    }

    private void ShowDifficulty()
    {
        if (_btnDifficulty != null) _btnDifficulty.text = "DIFICULTAD: " + GameSettings.DifficultyName(GameSettings.Difficulty);
    }

    // El volumen se aplica al mixer en vivo, mientras se mueve el slider
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

    private void OnFixedJoystickChanged(ChangeEvent<bool> evt) => GameSettings.FixedJoystick = evt.newValue;

    private bool IsOptionsOpen => _optionsOverlay != null && _optionsOverlay.resolvedStyle.display == DisplayStyle.Flex;

    private void OpenOptions()
    {
        if (_optionsOverlay == null) return;

        _optionsOverlay.style.display = DisplayStyle.Flex;
        // Esperar un frame para que la transición de opacidad se aplique
        _optionsOverlay.schedule.Execute(() =>
        {
            _optionsOverlay.AddToClassList("overlay--visible");
            _btnOptionsBack?.Focus();
        });
    }

    private void CloseOptions()
    {
        if (_optionsOverlay == null) return;

        GameSettings.Save();
        _optionsOverlay.RemoveFromClassList("overlay--visible");
        _optionsOverlay.schedule.Execute(() => _optionsOverlay.style.display = DisplayStyle.None).StartingIn(200);
        _btnOptions?.Focus();
    }

    private bool IsCreditsOpen => _creditsOverlay != null && _creditsOverlay.resolvedStyle.display == DisplayStyle.Flex;

    private void OpenCredits()
    {
        if (_creditsOverlay == null || _isLoading) return;
        _creditsOverlay.style.display = DisplayStyle.Flex;
        _creditsOverlay.schedule.Execute(() =>
        {
            _creditsOverlay.AddToClassList("overlay--visible");
            _btnCreditsBack?.Focus();
        });
    }

    private void CloseCredits()
    {
        if (_creditsOverlay == null) return;
        _creditsOverlay.RemoveFromClassList("overlay--visible");
        _creditsOverlay.schedule.Execute(() => _creditsOverlay.style.display = DisplayStyle.None).StartingIn(200);
        _btnCredits?.Focus();
    }

    private bool IsLevelsOpen => _levelsOverlay != null && _levelsOverlay.resolvedStyle.display == DisplayStyle.Flex;

    private void OpenLevels()
    {
        if (_levelsOverlay == null) return;

        int best = GameSession.BestScore(_nextSceneName);
        if (_level1Best != null) _level1Best.text = best > 0 ? $"Mejor puntaje: {best:N0}" : "";

        _levelsOverlay.style.display = DisplayStyle.Flex;
        _levelsOverlay.schedule.Execute(() =>
        {
            _levelsOverlay.AddToClassList("overlay--visible");
            _btnLevel1?.Focus();
        });
    }

    private void CloseLevels()
    {
        if (_levelsOverlay == null) return;

        _levelsOverlay.RemoveFromClassList("overlay--visible");
        _levelsOverlay.schedule.Execute(() => _levelsOverlay.style.display = DisplayStyle.None).StartingIn(200);
        _btnPlay?.Focus();
    }

    #endregion

    #region Eventos de Botones

    // JUGAR abre la selección de nivel
    private void OnPlayClicked()
    {
        if (_isLoading) return;
        OpenLevels();
    }

    private void OnLevel1Clicked()
    {
        if (_isLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(_nextSceneName))
        {
            Debug.LogError($"La escena '{_nextSceneName}' no está en Build Profiles / Scene List.", this);
            return;
        }

        StartCoroutine(LoadNextScene());
    }

    private void OnOptionsClicked()
    {
        if (_isLoading) return;
        OpenOptions();
    }

    private void OnExitClicked()
    {
        if (_isLoading) return;

        Debug.Log("Cerrando el juego...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnBackPressed()
    {
        if (_isLoading) return;

        if (IsOptionsOpen)
        {
            PlaySelectSound();
            CloseOptions();
        }
        else if (IsLevelsOpen)
        {
            PlaySelectSound();
            CloseLevels();
        }
        else if (IsCreditsOpen)
        {
            PlaySelectSound();
            CloseCredits();
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        else
        {
            // En Android, "atrás" en el menú principal cierra la app
            Application.Quit();
        }
#endif
    }

    private IEnumerator LoadNextScene()
    {
        _isLoading = true;
        _btnPlay?.SetEnabled(false);
        _btnLevel1?.SetEnabled(false);
        _btnOptions?.SetEnabled(false);
        _btnExit?.SetEnabled(false);

        Debug.Log($"Cargando escena: {_nextSceneName}...");
        AsyncOperation load = SceneManager.LoadSceneAsync(_nextSceneName);
        load.allowSceneActivation = false;

        _fade?.AddToClassList("fade--visible");
        yield return new WaitForSecondsRealtime(_fadeDuration);

        // allowSceneActivation=false deja el progreso en 0.9 hasta activar
        while (load.progress < 0.9f)
        {
            yield return null;
        }

        load.allowSceneActivation = true;
    }

    #endregion
}
