using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Ajustes de la build WebGL al arrancar: desactiva el menú contextual del navegador, porque el clic
/// derecho es el botón de Semilla en PC (ver Plugins/WebGL/BrowserInput.jslib). En otras plataformas no hace nada.
/// </summary>
public static class WebGLBrowserSetup
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SmogBuster_DisableContextMenu();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Setup() => SmogBuster_DisableContextMenu();
#endif
}
