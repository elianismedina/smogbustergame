using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Icono del estado del aire en la barra de smog (GDD 9.3 y 10.3): la forma cambia con el nivel,
/// así el estado no depende solo del color (WCAG 1.4.1).
/// - Limpio: pulmones.
/// - Contaminado: nube con mascarilla.
/// - Alerta: triángulo con exclamación.
/// Se dibuja con vectores (Painter2D), sin texturas.
/// </summary>
public class SmogStateIcon : VisualElement
{
    public enum State
    {
        Clean,
        Polluted,
        Alert,
    }

    // Las coordenadas de dibujo van en una rejilla de 44 x 44
    private const float Grid = 44f;

    private static readonly Color LungColor = new Color(0.47f, 0.90f, 0.67f);
    private static readonly Color CloudColor = new Color(0.82f, 0.80f, 0.72f);
    private static readonly Color MaskColor = new Color(0.97f, 0.97f, 0.95f);
    private static readonly Color AlertColor = new Color(0.94f, 0.42f, 0.33f);
    private static readonly Color InkColor = new Color(0.08f, 0.13f, 0.17f);

    private State _state = State.Clean;

    public SmogStateIcon()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    public State Current
    {
        get => _state;
        set
        {
            if (value == _state) return;
            _state = value;
            MarkDirtyRepaint();
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect rect = contentRect;
        float size = Mathf.Min(rect.width, rect.height);
        if (size <= 0f) return;

        float scale = size / Grid;
        Vector2 origin = rect.center - new Vector2(size, size) * 0.5f;
        Vector2 P(float x, float y) => origin + new Vector2(x, y) * scale;

        Painter2D painter = context.painter2D;
        painter.lineJoin = LineJoin.Round;
        painter.lineCap = LineCap.Round;

        switch (_state)
        {
            case State.Clean:
                DrawLungs(painter, P, scale);
                break;
            case State.Polluted:
                DrawMaskedCloud(painter, P, scale);
                break;
            default:
                DrawAlert(painter, P, scale);
                break;
        }
    }

    private delegate Vector2 Point(float x, float y);

    private static void DrawLungs(Painter2D p, Point P, float scale)
    {
        p.fillColor = LungColor;
        p.BeginPath();
        // Lóbulo izquierdo
        p.MoveTo(P(19f, 15f));
        p.BezierCurveTo(P(9f, 13f), P(3f, 28f), P(5f, 38f));
        p.BezierCurveTo(P(9f, 41f), P(15f, 39f), P(18f, 36f));
        p.BezierCurveTo(P(20f, 30f), P(20f, 22f), P(19f, 15f));
        p.ClosePath();
        // Lóbulo derecho
        p.MoveTo(P(25f, 15f));
        p.BezierCurveTo(P(35f, 13f), P(41f, 28f), P(39f, 38f));
        p.BezierCurveTo(P(35f, 41f), P(29f, 39f), P(26f, 36f));
        p.BezierCurveTo(P(24f, 30f), P(24f, 22f), P(25f, 15f));
        p.ClosePath();
        p.Fill();

        // Tráquea y bronquios
        p.strokeColor = LungColor;
        p.lineWidth = 3.5f * scale;
        p.BeginPath();
        p.MoveTo(P(22f, 4f));
        p.LineTo(P(22f, 18f));
        p.LineTo(P(17f, 24f));
        p.MoveTo(P(22f, 18f));
        p.LineTo(P(27f, 24f));
        p.Stroke();
    }

    private static void DrawMaskedCloud(Painter2D p, Point P, float scale)
    {
        // Nube: tres bultos y una base redondeada
        p.fillColor = CloudColor;
        p.BeginPath();
        p.Arc(P(14f, 22f), 8f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();
        p.BeginPath();
        p.Arc(P(24f, 16f), 10f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();
        p.BeginPath();
        p.Arc(P(32f, 23f), 7.5f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();
        p.BeginPath();
        p.MoveTo(P(8f, 24f));
        p.LineTo(P(38f, 24f));
        p.LineTo(P(38f, 30f));
        p.BezierCurveTo(P(38f, 34f), P(36f, 36f), P(32f, 36f));
        p.LineTo(P(12f, 36f));
        p.BezierCurveTo(P(8f, 36f), P(6f, 34f), P(6f, 30f));
        p.ClosePath();
        p.Fill();

        // Mascarilla con sus gomas
        p.fillColor = MaskColor;
        p.BeginPath();
        p.MoveTo(P(14f, 24f));
        p.LineTo(P(30f, 24f));
        p.LineTo(P(29f, 33f));
        p.BezierCurveTo(P(25f, 36f), P(19f, 36f), P(15f, 33f));
        p.ClosePath();
        p.Fill();

        p.strokeColor = InkColor;
        p.lineWidth = 1.6f * scale;
        p.BeginPath();
        p.MoveTo(P(14f, 25f));
        p.LineTo(P(6f, 21f));
        p.MoveTo(P(30f, 25f));
        p.LineTo(P(38f, 21f));
        p.MoveTo(P(17f, 28f));
        p.LineTo(P(27f, 28f));
        p.MoveTo(P(17f, 31f));
        p.LineTo(P(27f, 31f));
        p.Stroke();
    }

    private static void DrawAlert(Painter2D p, Point P, float scale)
    {
        p.fillColor = AlertColor;
        p.BeginPath();
        p.MoveTo(P(22f, 4f));
        p.LineTo(P(41f, 38f));
        p.LineTo(P(3f, 38f));
        p.ClosePath();
        p.Fill();

        p.strokeColor = AlertColor;
        p.lineWidth = 4f * scale;
        p.BeginPath();
        p.MoveTo(P(22f, 4f));
        p.LineTo(P(41f, 38f));
        p.LineTo(P(3f, 38f));
        p.ClosePath();
        p.Stroke();

        p.strokeColor = InkColor;
        p.lineWidth = 4f * scale;
        p.BeginPath();
        p.MoveTo(P(22f, 15f));
        p.LineTo(P(22f, 27f));
        p.Stroke();

        p.fillColor = InkColor;
        p.BeginPath();
        p.Arc(P(22f, 33f), 2.4f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();
    }
}
