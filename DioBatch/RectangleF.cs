using Microsoft.Xna.Framework;
using System;

namespace DioUI.RectangleFNS;


public enum Anchor {
    Top,
    Right,
    Bottom,
    Left,
    Center,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
public struct RectangleF : IEquatable<RectangleF> {
    private float _x;
    private float _y;
    private float _width;
    private float _height;

    public float X {
        readonly get { return _x; }
        set { _x = value; }
    }

    public float Y {
        readonly get { return _y; }
        set { _y = value; }
    }

    public float Width {
        readonly get { return _width; }
        set { _width = value; }
    }

    public float Height {
        readonly get { return _height; }
        set { _height = value; }
    }

    public readonly float Left => _x;

    public readonly float Right => _x + _width;

    public readonly float Top => _y;

    public readonly float Bottom => _y + _height;

    public readonly float CenterX => _x + _width / 2;
    public readonly float CenterY => _y + _height / 2;
    public Vector2 Position {
        readonly get { return new Vector2(_x, _y); }
        set {
            _x = value.X;
            _y = value.Y;
        }
    }
    public readonly Vector2 TopLeft => new(_x, _y);
    public readonly Vector2 TopRight => new(_x + _width, _y);
    public readonly Vector2 BottomLeft => new(_x, _y + _height);
    public readonly Vector2 BottomRight => new(_x + _width, _y + _height);
    public readonly Vector2 MidLeft => new(_x, _y + _height / 2);
    public readonly Vector2 MidRight => new(_x + _width, _y + _height / 2);
    public readonly Vector2 MidTop => new(_x + _width / 2, _y);
    public readonly Vector2 MidBottom => new(_x + _width / 2, _y + _height);
    public readonly Vector2 TL => new(_x, _y);
    public readonly Vector2 TR => new(_x + _width, _y);
    public readonly Vector2 BL => new(_x, _y + _height);
    public readonly Vector2 BR => new(_x + _width, _y + _height);
    public readonly Vector2[] Corners => [TopLeft, TopRight, BottomRight, BottomLeft];

    public Vector2 Size {
        readonly get { return new Vector2(_width, _height); }
        set {
            _width = value.X;
            _height = value.Y;
        }
    }

    public readonly Vector2 Center => new(_x + _width / 2, _y + _height / 2);

    public static RectangleF Empty => new(0, 0, 0, 0);

    public RectangleF(float x, float y, float width, float height) {
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }
    public RectangleF(double x, double y, double width, double height) {
        _x = (float)x;
        _y = (float)y;
        _width = (float)width;
        _height = (float)height;
    }

    public RectangleF(Vector2 position, Vector2 size) {
        _x = position.X;
        _y = position.Y;
        _width = size.X;
        _height = size.Y;
    }

    public RectangleF(Point location, Point size) {
        _x = location.X;
        _y = location.Y;
        _width = size.X;
        _height = size.Y;
    }

    public RectangleF(Rectangle rectangle) {
        _x = rectangle.X;
        _y = rectangle.Y;
        _width = rectangle.Width;
        _height = rectangle.Height;
    }

    public readonly Rectangle ToRectangle() {
        return new Rectangle(
            (int)Math.Round(_x),
            (int)Math.Round(_y),
            (int)Math.Round(_width),
            (int)Math.Round(_height)
        );
    }
    public readonly bool Contains(Point point) {
        return _x <= point.X && point.X < _x + _width && _y <= point.Y && point.Y < _y + _height;
    }

    public readonly bool Contains(Vector2 point) {
        return _x <= point.X && point.X < _x + _width && _y <= point.Y && point.Y < _y + _height;
    }
    public readonly bool Contains(float X, float Y) {
        return _x <= X && X < _x + _width && _y <= Y && Y < _y + _height;
    }

    public readonly bool Contains(RectangleF value) {
        return _x <= value._x && value._x + value._width <= _x + _width &&
                _y <= value._y && value._y + value._height <= _y + _height;
    }
    public readonly bool Contains(Rectangle value) {
        return _x <= value.X && value.X + value.Width <= _x + _width &&
                _y <= value.Y && value.Y + value.Height <= _y + _height;
    }

    public readonly bool Intersects(RectangleF value) {
        return value._x < _x + _width && _x < value._x + value._width &&
                value._y < _y + _height && _y < value._y + value._height;
    }
    public readonly bool Intersects(Rectangle value) {
        return value.X < _x + _width && _x < value.X + value.Width &&
                value.Y < _y + _height && _y < value.Y + value.Height;
    }

    public readonly bool Intersects(RectangleF value, out RectangleF? result) {
        if (value._x < _x + _width && _x < value._x + value._width &&
            value._y < _y + _height && _y < value._y + value._height) {
            float resultX = Math.Max(_x, value._x);
            float resultY = Math.Max(_y, value._y);
            result = new RectangleF(
                resultX,
                resultY,
                Math.Min(_x + _width, value._x + value._width) - resultX,
                Math.Min(_y + _height, value._y + value._height) - resultY
            );
            return true;
        }

        result = null;
        return false;
    }
    public readonly RectangleF? GetIntersection(RectangleF value) {
        if (value._x < _x + _width && _x < value._x + value._width &&
            value._y < _y + _height && _y < value._y + value._height) {
            float resultX = Math.Max(_x, value._x);
            float resultY = Math.Max(_y, value._y);
            return new RectangleF(
                resultX,
                resultY,
                Math.Min(_x + _width, value._x + value._width) - resultX,
                Math.Min(_y + _height, value._y + value._height) - resultY
            );
        }

        return null;
    }
    public readonly Vector2 GetAnchor(Anchor anchor) {
        return anchor switch {
            Anchor.TopLeft => TopLeft,
            Anchor.TopRight => TopRight,
            Anchor.BottomLeft => BottomLeft,
            Anchor.BottomRight => BottomRight,
            Anchor.Top => new Vector2(X + Width / 2f, Y),
            Anchor.Bottom => new Vector2(X + Width / 2f, Y + Height),
            Anchor.Left => new Vector2(X, Y + Height / 2f),
            Anchor.Right => new Vector2(X + Width, Y + Height / 2f),
            Anchor.Center => new Vector2(X + Width / 2f, Y + Height / 2f),
            _ => TopLeft
        };
    }
    public static RectangleF Union(RectangleF value1, RectangleF value2) {
        float x = Math.Min(value1._x, value2._x);
        float y = Math.Min(value1._y, value2._y);
        return new RectangleF(
            x, y,
            Math.Max(value1._x + value1._width, value2._x + value2._width) - x,
            Math.Max(value1._y + value1._height, value2._y + value2._height) - y
        );
    }
    public static RectangleF FromRectangle(Rectangle rectangle) {
        return new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }
    public static Rectangle ToRectangle(RectangleF rectangle) {
        return new Rectangle((int)rectangle.X, (int)rectangle.Y, (int)rectangle.Width, (int)rectangle.Height);
    }
    public static Rectangle? ToRectangle(RectangleF? rectangle) {
        if (rectangle.HasValue)
            return new Rectangle((int)rectangle.Value.X, (int)rectangle.Value.Y, (int)rectangle.Value.Width, (int)rectangle.Value.Height);
        else return null;
    }

    public void Inflate(float horizontalAmount, float verticalAmount) {
        _x -= horizontalAmount;
        _y -= verticalAmount;
        _width += horizontalAmount * 2;
        _height += verticalAmount * 2;
    }
    public void Inflate(float amount) {
        _x -= amount;
        _y -= amount;
        _width += amount * 2;
        _height += amount * 2;
    }
    public void Offset(Point amount) {
        _x += amount.X;
        _y += amount.Y;
    }

    public void Offset(Vector2 amount) {
        _x += amount.X;
        _y += amount.Y;
    }
    public void Offset(float xAmount, float yAmount) {
        _x += xAmount;
        _y += yAmount;
    }
    public readonly RectangleF GetOffset(Point amount) {
        RectangleF OffSet = this;
        OffSet._x += amount.X;
        OffSet._y += amount.Y;
        return OffSet;
    }
    public readonly RectangleF GetOffset(Vector2 amount) {
        RectangleF OffSet = this;
        OffSet._x += amount.X;
        OffSet._y += amount.Y;
        return OffSet;
    }
    public readonly RectangleF GetOffset(float xAmount, float yAmount) {
        RectangleF OffSet = this;
        OffSet._x += xAmount;
        OffSet._y += yAmount;
        return OffSet;
    }
    public readonly RectangleF GetInflated(float horizontalAmount, float verticalAmount) {
        return new RectangleF(_x - horizontalAmount, _y - verticalAmount, _width + horizontalAmount * 2, _height + verticalAmount * 2);
    }
    public readonly RectangleF GetInflated(float amount) {
        return new RectangleF(_x - amount, _y - amount, _width + amount * 2, _height + amount * 2);
    }
    public readonly RectangleF GetInflated(Vector2 amount) {
        return new RectangleF(_x - amount.X, _y - amount.Y, _width + amount.X * 2, _height + amount.Y * 2);
    }
    public readonly RectangleF GetExpanded(float horizontalAmount, float verticalAmount) {
        return new RectangleF(_x, _y, _width + horizontalAmount, _height + verticalAmount);
    }
    public readonly RectangleF GetExpanded(float amount) {
        return new RectangleF(_x, _y, _width + amount, _height + amount);
    }
    public readonly RectangleF GetExpanded(Vector2 amount) {
        return new RectangleF(_x, _y, _width + amount.X, _height + amount.Y);
    }
    public void dScale(Vector2 dScale) {
        if (dScale == Vector2.Zero) return;
        Vector2 center = Center;
        float newWidth = Width * (1 + dScale.X);
        float newHeight = Height * (1 + dScale.Y);

        _x = center.X - newWidth / 2.0f;
        _y = center.Y - newHeight / 2.0f;
        (_width, _height) = (newWidth, newHeight);
    }
    public void Scale(Vector2 scale) {
        if (scale == Vector2.One) return;
        Vector2 center = Center;
        float newWidth = Width * scale.X;
        float newHeight = Height * scale.Y;

        _x = center.X - newWidth / 2.0f;
        _y = center.Y - newHeight / 2.0f;
        (_width, _height) = (newWidth, newHeight);
    }
    public void Scale(float scale) {
        if (scale == 1) return;
        Vector2 center = Center;
        float newWidth = Width * scale;
        float newHeight = Height * scale;

        _x = center.X - newWidth / 2.0f;
        _y = center.Y - newHeight / 2.0f;
        (_width, _height) = (newWidth, newHeight);
    }
    public void FitAndCenter(float aspectRatio) {
        float boundsAspect = Width / Height;
        float newWidth, newHeight;

        if (boundsAspect > aspectRatio) {
            newHeight = Height;
            newWidth = newHeight * aspectRatio;
        }
        else {
            newWidth = Width;
            newHeight = newWidth / aspectRatio;
        }

        X += (Width - newWidth) / 2f;
        Y += (Height - newHeight) / 2f;
        Width = newWidth;
        Height = newHeight;
    }
    public void FitAndAlign(float aspectRatio, Anchor alignment = Anchor.Center) {
        float boundsAspect = Width / Height;
        float newWidth, newHeight;

        if (boundsAspect > aspectRatio) {
            newHeight = Height;
            newWidth = newHeight * aspectRatio;
        }
        else {
            newWidth = Width;
            newHeight = newWidth / aspectRatio;
        }

        X += alignment switch {
            Anchor.Left or Anchor.TopLeft or Anchor.BottomLeft => 0f,
            Anchor.Right or Anchor.TopRight or Anchor.BottomRight => Width - newWidth,
            _ => (Width - newWidth) / 2f
        };

        Y += alignment switch {
            Anchor.Top or Anchor.TopLeft or Anchor.TopRight => 0f,
            Anchor.Bottom or Anchor.BottomLeft or Anchor.BottomRight => Height - newHeight,
            _ => (Height - newHeight) / 2f
        };

        Width = newWidth;
        Height = newHeight;
    }
    public readonly RectangleF GetScaled(Vector2 scale) {
        if (scale == Vector2.One) return this;
        RectangleF Scaled = this;
        Vector2 center = Center;
        float newWidth = Width * scale.X;
        float newHeight = Height * scale.Y;

        Scaled._x = center.X - newWidth / 2.0f;
        Scaled._y = center.Y - newHeight / 2.0f;
        (Scaled._width, Scaled._height) = (newWidth, newHeight);
        return Scaled;
    }
    public readonly RectangleF GetScaled(float scale) {
        if (scale == 1) return this;
        RectangleF Scaled = this;
        Vector2 center = Center;
        float newWidth = Width * scale;
        float newHeight = Height * scale;

        Scaled._x = center.X - newWidth / 2.0f;
        Scaled._y = center.Y - newHeight / 2.0f;
        (Scaled._width, Scaled._height) = (newWidth, newHeight);
        return Scaled;
    }
    public readonly RectangleF GetFitAndCentered(float aspectRatio) {
        return FitAndCenter(this, aspectRatio);
    }
    public static RectangleF FitAndCenter(RectangleF bounds, float aspectRatio) {
        float boundsAspect = bounds.Width / bounds.Height;
        float width, height;
        if (boundsAspect > aspectRatio) {
            height = bounds.Height;
            width = height * aspectRatio;
        }
        else {
            width = bounds.Width;
            height = width / aspectRatio;
        }
        float x = bounds.X + (bounds.Width - width) / 2f;
        float y = bounds.Y + (bounds.Height - height) / 2f;
        return new RectangleF(x, y, width, height);
    }
    public static RectangleF Offset(RectangleF rectangle, Point amount) {
        return new(rectangle._x + amount.X, rectangle._y + amount.Y, rectangle._width, rectangle._height);
    }
    public static RectangleF Offset(RectangleF rectangle, Vector2 amount) {
        return new(rectangle._x + amount.X, rectangle._y + amount.Y, rectangle._width, rectangle._height);
    }
    public static RectangleF Offset(RectangleF rectangle, float xAmount, float yAmount) {
        return new(rectangle._x + xAmount, rectangle._y + yAmount, rectangle._width, rectangle._height);
    }
    public static RectangleF Inflate(RectangleF rectangle, float horizontalAmount, float verticalAmount) {
        return new(rectangle._x - horizontalAmount, rectangle._y - verticalAmount, rectangle._width + horizontalAmount + horizontalAmount, rectangle._height + verticalAmount + verticalAmount);
    }
    public static RectangleF dScale(RectangleF rectangle, Vector2 dScale) {
        Vector2 center = rectangle.Center;
        float newWidth = rectangle.Width * (1 + dScale.X);
        float newHeight = rectangle.Height * (1 + dScale.Y);
        float newX = center.X - newWidth / 2.0f;
        float newY = center.Y - newHeight / 2.0f;
        return new RectangleF(newX, newY, newWidth, newHeight);
    }
    public static RectangleF Scale(RectangleF rectangle, Vector2 scale) {
        Vector2 center = rectangle.Center;
        float newWidth = rectangle.Width * scale.X;
        float newHeight = rectangle.Height * scale.Y;
        float newX = center.X - newWidth / 2.0f;
        float newY = center.Y - newHeight / 2.0f;
        return new RectangleF(newX, newY, newWidth, newHeight);
    }
    public static RectangleF Scale(RectangleF rectangle, float scale) {
        Vector2 center = rectangle.Center;
        float newWidth = rectangle.Width * scale;
        float newHeight = rectangle.Height * scale;
        float newX = center.X - newWidth / 2.0f;
        float newY = center.Y - newHeight / 2.0f;
        return new RectangleF(newX, newY, newWidth, newHeight);
    }
    public static RectangleF Lerp(RectangleF A, RectangleF B, float t) {
        float newX = A.X + (B.X - A.X) * t;
        float newY = A.Y + (B.Y - A.Y) * t;
        float newWidth = A.Width + (B.Width - A.Width) * t;
        float newHeight = A.Height + (B.Height - A.Height) * t;
        return new RectangleF(newX, newY, newWidth, newHeight);
    }
    public static bool CloseEnough(RectangleF a, RectangleF b, float tolerance = 2f) {
        return Math.Abs(a.X - b.X) <= tolerance &&
               Math.Abs(a.Y - b.Y) <= tolerance &&
               Math.Abs(a.Width - b.Width) <= tolerance &&
               Math.Abs(a.Height - b.Height) <= tolerance;
    }

    public readonly bool Equals(RectangleF other) {
        return _x == other._x && _y == other._y && _width == other._width && _height == other._height;
    }

    public readonly override bool Equals(object? obj) {
        return obj is RectangleF rect && Equals(rect);
    }

    public readonly override int GetHashCode() {
        return _x.GetHashCode() ^ _y.GetHashCode() ^ _width.GetHashCode() ^ _height.GetHashCode();
    }

    public readonly override string ToString() {
        return $"{{X:{_x} Y:{_y} W:{_width} H:{_height}}}";
    }

    public static bool operator ==(RectangleF a, RectangleF b) {
        return a.Equals(b);
    }

    public static bool operator !=(RectangleF a, RectangleF b) {
        return !a.Equals(b);
    }
}
