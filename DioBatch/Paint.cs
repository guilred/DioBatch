using Microsoft.Xna.Framework;

namespace DioUI;

public struct Paint {
    public enum PaintType : byte { Solid, Linear, Radial, Texture }
    public enum EasingType : byte { Linear, EaseIn, EaseOut, EaseInOut }
    public PaintType Type;
    public EasingType Easing;
    public float EasingPower;
    public Color ColorA;
    public Color ColorB;
    public Vector2 Start;
    public Vector2 End;
    public float OffsetA;
    public float OffsetB;
    public bool IsLocal;
    public bool isNormalized;
    public bool isPixelOffsets;
    public static Paint Solid(Color color) => new() { Type = PaintType.Solid, ColorA = color , ColorB = color };
    public static Paint Linear(Vector2 start, Vector2 end, Color startColor, Color endColor, bool isLocal = true) {
        return new Paint() {
            Type = PaintType.Linear,
            ColorA = startColor,
            ColorB = endColor,
            Start = start,
            End = end,
            IsLocal = isLocal,
            OffsetB = 1f
        };
    }
    public static Paint LinearNorm(Vector2 start, Vector2 end, Color startColor, Color endColor) {
        return new Paint() {
            Type = PaintType.Linear,
            ColorA = startColor,
            ColorB = endColor,
            Start = start,
            End = end,
            isNormalized = true,
            IsLocal = true,
            OffsetB = 1f
        };
    }

    public static Paint Radial(Vector2 center, Vector2 edge, Color centerColor, Color edgeColor, bool isLocal = true) {
        return new Paint() {
            Type = PaintType.Radial,
            ColorA = centerColor,
            ColorB = edgeColor,
            Start = center,
            End = edge,
            IsLocal = isLocal,
            OffsetB = 1f
        };
    }
    public static Paint RadialNorm(Vector2 center, Vector2 edge, Color centerColor, Color edgeColor) {
        return new Paint() {
            Type = PaintType.Radial,
            ColorA = centerColor,
            ColorB = edgeColor,
            Start = center,
            End = edge,
            isNormalized = true,
            IsLocal = true,
            OffsetB = 1f
        };
    }
    public Paint SetOffsets(float offsetA = 0f, float offsetB = 1f, bool usePixelOffsets = false) {
        if (!usePixelOffsets) {
            (OffsetA, OffsetB) = (offsetA, offsetB);
            return this;
        }
        if (isNormalized) {
            (OffsetA, OffsetB) = (offsetA, offsetB);
            isPixelOffsets = true;
            return this;
        }

        float distance = Vector2.Distance(Start, End);
        if (distance > 0.0001f) {
            (OffsetA, OffsetB) = (offsetA / distance, (distance - offsetB) / distance);
            return this;
        }
        (OffsetA, OffsetB) = (0f, 1f);
        return this;
    }
    public Paint SetEasing(EasingType easing = EasingType.Linear, float power = 2f) {
        Easing = easing;
        EasingPower = power;
        return this;
    }
    public static Paint Lerp(Paint a, Paint b, float amount) {
        return new Paint {
            Type = a.Type,
            Easing = a.Easing,
            IsLocal = a.IsLocal,
            EasingPower = MathHelper.Lerp(a.EasingPower, b.EasingPower, amount),
            ColorA = Color.Lerp(a.ColorA, b.ColorA, amount),
            ColorB = Color.Lerp(a.ColorB, b.ColorB, amount),
            Start = Vector2.Lerp(a.Start, b.Start, amount),
            End = Vector2.Lerp(a.End, b.End, amount),
            OffsetA = MathHelper.Lerp(a.OffsetA, b.OffsetA, amount),
            OffsetB = MathHelper.Lerp(a.OffsetB, b.OffsetB, amount),
        };
    }
    public Paint Reversed() {
        return this with { Start = End, End = Start };
    }
    public bool IsOpaque() => ColorA.A == 255 && ColorB.A == 255;
    public bool IsTrspt() => ColorA.A == 0 && ColorB.A == 0;
    public static Paint operator *(Paint l, float r) {
        return l with { ColorA = l.ColorA * r, ColorB = l.ColorB * r };
    }
    public static implicit operator Paint(Color color) => Solid(color);
}