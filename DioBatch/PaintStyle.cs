using Microsoft.Xna.Framework;

namespace DioUI;

public struct PaintStyle {
    public enum PaintType : byte { Solid, Linear, Radial, Texture }
    public enum EasingType : byte { Linear, EaseIn, EaseOut, EaseInOut }
    public PaintType Type;
    public EasingType Easing;
    public float EasingPower;
    public Color ColorA;
    public Color ColorB;
    public Vector2 Start;
    public Vector2 End;
    public bool IsLocal;
    public float OffsetsA;
    public float OffsetsB;
    public static PaintStyle Solid(Color color) => new() { Type = PaintType.Solid, ColorA = color, ColorB = Color.White };
    public static PaintStyle Linear(Vector2 start, Vector2 end, Color startColor, Color endColor, bool isLocal = true) {
        return new PaintStyle() {
            Type = PaintType.Linear,
            ColorA = startColor,
            ColorB = endColor,
            Start = start,
            End = end,
            IsLocal = isLocal,
            OffsetsB = 1f
        };
    }

    public static PaintStyle Radial(Vector2 center, Vector2 edge, Color centerColor, Color edgeColor, bool isLocal = true) {
        return new PaintStyle() {
            Type = PaintType.Radial,
            ColorA = centerColor,
            ColorB = edgeColor,
            Start = center,
            End = edge,
            IsLocal = isLocal,
            OffsetsB = 1f
        };
    }
    public PaintStyle SetOffsets(float offsetA = 0f, float offsetB = 1f, bool usePixelOffsets = false) {
        if (!usePixelOffsets) {
            (OffsetsA, OffsetsB) = (offsetA, offsetB);
            return this;
        }
        float distance = Vector2.Distance(Start, End);
        if (distance > 0.0001f) {
            (OffsetsA, OffsetsB) = (offsetA / distance, (distance - offsetB) / distance);
            return this;
        }
        (OffsetsA, OffsetsB) = (0f, 1f);
        return this;
    }
    public PaintStyle SetEasing(EasingType easing = EasingType.Linear, float power = 2f) {
        Easing = easing;
        EasingPower = power;
        return this;
    }
    public PaintStyle Reversed() {
        return this with { Start = End, End = Start };
    }
    public bool IsOpaque() => ColorA.A == 255 && ColorB.A == 255;
    public bool IsTrspt() => ColorA.A == 0 && ColorB.A == 0;
    public static PaintStyle operator *(PaintStyle l, float r) {
        return l with { ColorA = l.ColorA * r, ColorB = l.ColorB * r };
    }
    public static implicit operator PaintStyle(Color color) => PaintStyle.Solid(color);
}