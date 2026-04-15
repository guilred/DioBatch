using Microsoft.Xna.Framework;
public struct PaintStyle {
    public enum PaintType : byte { Solid, Linear, Radial }
    public PaintType Type;
    public Color ColorA;
    public Color ColorB;
    public Vector4 Points; // xy = Start, zw = End
    public bool IsLocal;
    public static PaintStyle Solid(Color color) => new() { Type = PaintType.Solid, ColorA = color };

    public static PaintStyle Linear(Vector2 start, Vector2 end, Color startColor, Color endColor, bool isLocal = false) => new() {
        Type = PaintType.Linear,
        ColorA = startColor,
        ColorB = endColor,
        Points = new Vector4(start.X, start.Y, end.X, end.Y),
        IsLocal = isLocal
    };

    public static PaintStyle Radial(Vector2 center, Vector2 edge, Color centerColor, Color edgeColor, bool isLocal = false) => new() {
        Type = PaintType.Radial,
        ColorA = centerColor,
        ColorB = edgeColor,
        Points = new Vector4(center.X, center.Y, edge.X, edge.Y),
        IsLocal = isLocal
    };
}