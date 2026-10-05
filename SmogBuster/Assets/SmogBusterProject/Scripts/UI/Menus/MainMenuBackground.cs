using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Anima el fondo del menú principal hecho con el key art en capas (MainMenu.uxml).
/// La imagen base queda fija y encima se mueven el humo, el rayo, el dron y el logo.
/// Todo son VisualElements con transformaciones, así que es barato en WebGL y Android.
/// </summary>
public class MainMenuBackground
{
    // Tamaño del key art en píxeles: las coordenadas de abajo están en este espacio
    private const float ArtWidth = 1376f;
    private const float ArtHeight = 768f;

    // Parte superior de las chimeneas que echan humo
    private static readonly Vector2[] Chimneys =
    {
        new Vector2(418, 200), new Vector2(432, 193),
        new Vector2(662, 212), new Vector2(684, 203),
        new Vector2(801, 212), new Vector2(815, 205),
        new Vector2(885, 208), new Vector2(906, 202),
    };

    private const int PuffsPerChimney = 3;
    private const float PuffSize = 54f;
    private const float PuffLife = 6.5f;

    private struct Puff
    {
        public VisualElement Element;
        public Vector2 Origin;
        public float Offset;
        public float Drift;
    }

    private readonly VisualElement _background;
    private readonly VisualElement _stage;
    private readonly VisualElement _drone;
    private readonly VisualElement _beam;
    private readonly VisualElement _impact;
    private readonly VisualElement _logo;
    private readonly VisualElement _logoGlow;
    private readonly List<VisualElement> _rotors;
    private readonly List<Puff> _puffs = new List<Puff>();

    private float _scale = 1f;
    private float _startTime = -1f;

    public MainMenuBackground(VisualElement root)
    {
        _background = root.Q<VisualElement>("background");
        _stage = root.Q<VisualElement>("bg-stage");
        _drone = root.Q<VisualElement>("bg-drone");
        _beam = root.Q<VisualElement>("bg-beam");
        _impact = root.Q<VisualElement>("bg-impact");
        _logo = root.Q<VisualElement>("bg-logo");
        _logoGlow = root.Q<VisualElement>("bg-logo-glow");
        _rotors = root.Query<VisualElement>(className: "bg-rotor").ToList();

        if (_background == null || _stage == null)
        {
            Debug.LogWarning("MainMenuBackground: no se encontró 'background' o 'bg-stage' en el UXML.");
            return;
        }

        _background.RegisterCallback<GeometryChangedEvent>(OnBackgroundGeometryChanged);
        CreatePuffs(root.Q<VisualElement>("bg-smoke"));

        // El logo empieza oculto y entra con Tick()
        if (_logo != null) _logo.style.opacity = 0f;
    }

    public void Dispose()
    {
        _background?.UnregisterCallback<GeometryChangedEvent>(OnBackgroundGeometryChanged);
    }

    private void CreatePuffs(VisualElement container)
    {
        if (container == null) return;

        // Desfases fijos para que el resultado sea igual en cada arranque
        var random = new System.Random(11);
        foreach (Vector2 chimney in Chimneys)
        {
            for (int i = 0; i < PuffsPerChimney; i++)
            {
                var element = new VisualElement { pickingMode = PickingMode.Ignore };
                element.AddToClassList("bg-smoke-puff");
                element.style.left = Length.Percent((chimney.x - PuffSize * 0.5f) / ArtWidth * 100f);
                element.style.top = Length.Percent((chimney.y - PuffSize * 0.5f) / ArtHeight * 100f);
                element.style.width = Length.Percent(PuffSize / ArtWidth * 100f);
                element.style.height = Length.Percent(PuffSize / ArtHeight * 100f);
                container.Add(element);

                _puffs.Add(new Puff
                {
                    Element = element,
                    Origin = chimney,
                    Offset = (i + (float)random.NextDouble() * 0.6f) / PuffsPerChimney * PuffLife,
                    Drift = 18f + (float)random.NextDouble() * 22f,
                });
            }
        }
    }

    /// <summary>Encaja el escenario como "cover": llena la pantalla y recorta lo que sobre.</summary>
    private void OnBackgroundGeometryChanged(GeometryChangedEvent evt)
    {
        float width = _background.layout.width;
        float height = _background.layout.height;
        if (width <= 0f || height <= 0f || float.IsNaN(width) || float.IsNaN(height)) return;

        _scale = Mathf.Max(width / ArtWidth, height / ArtHeight);
        float stageWidth = ArtWidth * _scale;
        float stageHeight = ArtHeight * _scale;
        _stage.style.width = stageWidth;
        _stage.style.height = stageHeight;
        // Lo que sobra se recorta sobre todo por la izquierda y por abajo,
        // para no cortar el logo (arriba a la derecha) en pantallas muy anchas o 4:3
        _stage.style.left = (width - stageWidth) * 0.8f;
        _stage.style.top = 0f;
    }

    /// <summary>Llamar cada frame con un tiempo que no dependa de Time.timeScale.</summary>
    public void Tick(float time)
    {
        if (_stage == null) return;
        if (_startTime < 0f) _startTime = time;
        float t = time - _startTime;

        // 1. Zoom muy lento de toda la escena
        float zoom = 1.015f + 0.02f * Mathf.Sin(t * 2f * Mathf.PI / 26f);
        _stage.style.scale = new Scale(new Vector3(zoom, zoom, 1f));

        // 2. Humo: cada bocanada sube, crece, se desplaza con el viento y se desvanece
        foreach (Puff puff in _puffs)
        {
            float life = Mathf.Repeat(t + puff.Offset, PuffLife) / PuffLife;
            float rise = Mathf.Lerp(0f, 95f, Mathf.Sqrt(life));
            float drift = puff.Drift * life * life;
            float size = Mathf.Lerp(0.35f, 1.5f, life);
            float alpha = Mathf.Sin(life * Mathf.PI) * 0.55f;
            puff.Element.style.translate = new Translate(drift * _scale, -rise * _scale);
            puff.Element.style.scale = new Scale(new Vector3(size, size, 1f));
            puff.Element.style.opacity = alpha;
        }

        // 3. Rayo: pulso suave con un parpadeo rápido encima
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * 0.9f);
        float flicker = 0.5f + 0.5f * Mathf.Sin(t * 37f) * Mathf.Sin(t * 23f);
        if (_beam != null) _beam.style.opacity = 0.22f + 0.38f * pulse + 0.08f * flicker;
        if (_impact != null)
        {
            float impact = 0.85f + 0.3f * pulse + 0.1f * flicker;
            _impact.style.scale = new Scale(new Vector3(impact, impact, 1f));
            _impact.style.opacity = 0.55f + 0.35f * pulse;
        }

        // 4. Dron: flota arriba y abajo con un leve balanceo
        if (_drone != null)
        {
            float bob = Mathf.Sin(t * 2f * Mathf.PI / 2.8f) * 4f;
            float sway = Mathf.Sin(t * 2f * Mathf.PI / 4.1f) * 0.6f;
            _drone.style.translate = new Translate(0f, bob * _scale);
            _drone.style.rotate = new Rotate(sway);
        }

        for (int i = 0; i < _rotors.Count; i++)
        {
            _rotors[i].style.opacity = 0.25f + 0.25f * Mathf.Abs(Mathf.Sin(t * 11f + i * 1.7f));
        }

        // 5. Logo: entra con un rebote y luego brilla cada pocos segundos
        if (_logo != null)
        {
            float intro = Mathf.Clamp01((t - 0.3f) / 0.9f);
            float logoScale = Mathf.LerpUnclamped(1.25f, 1f, EaseOutBack(intro));
            _logo.style.opacity = Mathf.Clamp01(intro * 2f);
            _logo.style.scale = new Scale(new Vector3(logoScale, logoScale, 1f));
        }

        if (_logoGlow != null)
        {
            float cycle = Mathf.Repeat(t - 1.2f, 5f);
            float swell = t < 1.2f
                ? Mathf.Sin(Mathf.Clamp01((t - 0.3f) / 0.9f) * Mathf.PI)
                : (cycle < 1.4f ? Mathf.Sin(cycle / 1.4f * Mathf.PI) : 0f);
            _logoGlow.style.opacity = 0.12f + 0.6f * swell;
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
