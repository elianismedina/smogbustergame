using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    [Header("Configuración de Escena")]
    [SerializeField] private string _nextSceneName = "Level01";

    private Button _btnPlay;
    private Button _btnOptions;
    private Button _btnExit;

    private void OnEnable()
    {
        // 1. Validar el componente UIDocument
        UIDocument uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null)
        {
            Debug.LogError("No se encontró el componente UIDocument en este GameObject.", this);
            return;
        }

        // 2. Obtener la raíz del UXML
        VisualElement root = uiDocument.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("El UIDocument no tiene un Source Asset (UXML) asignado.", this);
            return;
        }

        // 3. Buscar botones en la interfaz
        _btnPlay = root.Q<Button>("btn-play");
        _btnOptions = root.Q<Button>("btn-options");
        _btnExit = root.Q<Button>("btn-exit");

        // 4. Registrar listeners con comprobación previa
        RegisterButton(_btnPlay, OnPlayClicked, "btn-play");
        RegisterButton(_btnOptions, OnOptionsClicked, "btn-options");
        RegisterButton(_btnExit, OnExitClicked, "btn-exit");
    }

    private void OnDisable()
    {
        // Cancelar suscripción a eventos
        UnregisterButton(_btnPlay, OnPlayClicked);
        UnregisterButton(_btnOptions, OnOptionsClicked);
        UnregisterButton(_btnExit, OnExitClicked);
    }

    private void RegisterButton(Button button, System.Action callback, string buttonName)
    {
        if (button != null)
        {
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
            button.clicked -= callback;
        }
    }

    #region Eventos de Botones

    private void OnPlayClicked()
    {
        Debug.Log($"Cargando escena: {_nextSceneName}...");
        SceneManager.LoadScene(_nextSceneName);
    }

    private void OnOptionsClicked()
    {
        Debug.Log("Abriendo menú de opciones...");
    }

    private void OnExitClicked()
    {
        Debug.Log("Cerrando el juego...");
        Application.Quit();
    }

    #endregion
}
