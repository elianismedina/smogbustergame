using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Icono del botón de cámara del HUD: una cámara de fotos rodeada por dos flechas circulares
/// (cambiar de vista). En primera persona se pinta en el verde de acento para marcar la vista activa.
/// Se dibuja con vectores (Painter2D), sin texturas, así se ve nítido a cualquier resolución.
/// </summary>
public class CameraSwitchIcon : VisualElement
{
    // Las coordenadas de dibujo van en una rejilla de 44 x 44
    private const float Grid = 44f;

    private static readonly Color IconColor = new Color(0.88f, 0.92f, 0.90f);
    private static readonly Color AccentColor = new Color(0.47f, 0.90f, 0.67f);
    private static readonly Color InkColor = new Color(0.06f, 0.11f, 0.15f);

    private bool _highlighted;

    public CameraSwitchIcon()
    {
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    /// <summary>True para pintarlo en el color de acento (vista en primera persona).</summary>
    public bool Highlighted
    {
        get => _highlighted;
        set
        {
            if (value == _highlighted) return;
            _highlighted = value;
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

        Painter2D p = context.painter2D;
        p.lineJoin = LineJoin.Round;
        p.lineCap = LineCap.Round;
        Color color = _highlighted ? AccentColor : IconColor;

        // Cuerpo de la cámara: rectángulo redondeado con el visor encima
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(P(17f, 15f));
        p.LineTo(P(19f, 12f));
        p.LineTo(P(25f, 12f));
        p.LineTo(P(27f, 15f));
        p.ArcTo(P(32f, 15f), P(32f, 18f), 3f * scale);
        p.ArcTo(P(32f, 30f), P(29f, 30f), 3f * scale);
        p.ArcTo(P(12f, 30f), P(12f, 27f), 3f * scale);
        p.ArcTo(P(12f, 15f), P(15f, 15f), 3f * scale);
        p.ClosePath();
        p.Fill();

        // Objetivo: hueco oscuro con un anillo dentro
        p.fillColor = InkColor;
        p.BeginPath();
        p.Arc(P(22f, 22.5f), 5f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();
        p.fillColor = color;
        p.BeginPath();
        p.Arc(P(22f, 22.5f), 2.6f * scale, Angle.Degrees(0f), Angle.Degrees(360f));
        p.Fill();

        // Dos flechas circulares alrededor, en el sentido de las agujas del reloj (arriba y abajo)
        DrawArrowArc(p, P(22f, 22f), 18f, 200f, 320f, scale, color);
        DrawArrowArc(p, P(22f, 22f), 18f, 20f, 140f, scale, color);
    }

    // Arco con punta de flecha al final. Ángulos en grados con y hacia abajo (0 = derecha, 90 = abajo).
    private static void DrawArrowArc(Painter2D p, Vector2 center, float radius, float from, float to, float scale, Color color)
    {
        float r = radius * scale;
        p.strokeColor = color;
        p.lineWidth = 3f * scale;
        p.BeginPath();
        p.Arc(center, r, Angle.Degrees(from), Angle.Degrees(to - 6f));
        p.Stroke();

        float a = to * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        Vector2 tangent = new Vector2(-radial.y, radial.x);
        Vector2 end = center + radial * r;
        float head = 4.5f * scale;

        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(end + tangent * head);
        p.LineTo(end - tangent * head * 0.6f + radial * head);
        p.LineTo(end - tangent * head * 0.6f - radial * head);
        p.ClosePath();
        p.Fill();
    }
}
