// DioBatch.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using DioUI.RectangleFNS;

namespace DioBatch;

public class DioBatch {
    private const int _maxVertices = 8192;
    private const int _maxIndices = _maxVertices * 3;

    private readonly GraphicsDevice _device;

    private readonly Effect _effect;
    private readonly EffectPass _pass;
    private readonly EffectParameter _projectionParam;

    private readonly Texture2D?[] _textures = new Texture2D[8];
    private int _textureCount = 0;

    private readonly DynamicVertexBuffer _vertexBuffer;
    private readonly DynamicIndexBuffer _indexBuffer;

    private readonly PrimitiveVertex[] _vertices;
    private readonly short[] _indices;
    private int _vertexCount;
    private int _indexCount;

    private bool _begun;
    public DioBatch(GraphicsDevice device, ContentManager content) {
        _device = device;

        _effect = content.Load<Effect>("diobatch-effect");
        _pass = _effect.Techniques[0].Passes[0];
        _projectionParam = _effect.Parameters["Projection"];

        _vertexBuffer = new DynamicVertexBuffer(device, PrimitiveVertex.VertexDeclaration, _maxVertices, BufferUsage.WriteOnly);
        _indexBuffer = new DynamicIndexBuffer(device, IndexElementSize.SixteenBits, _maxIndices, BufferUsage.WriteOnly);

        _vertices = new PrimitiveVertex[_maxVertices];
        _indices = new short[_maxIndices];
    }

    public void Begin(Matrix? view = null, Matrix? projection = null) {
        if (_begun) throw new InvalidOperationException("DioBatch is already begun.");

        projection = (view ?? Matrix.Identity) * (projection ?? Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, 0f, 1f));

        _projectionParam.SetValue(projection.Value);
        _vertexCount = 0;
        _indexCount = 0;
        _begun = true;
    }

    public void End() {
        if (!_begun) throw new InvalidOperationException("DioBatch has not been begun.");

        flush();
        _begun = false;
    }

    private void flush() {
        if (_vertexCount == 0) return;

        _vertexBuffer.SetData(_vertices, 0, _vertexCount, SetDataOptions.Discard);
        _indexBuffer.SetData(_indices, 0, _indexCount, SetDataOptions.Discard);

        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;
        _device.BlendState = BlendState.AlphaBlend;

        _pass.Apply();
        for (int i = 0; i < _textureCount; i++) {
            _device.Textures[i] = _textures[i];
            _device.SamplerStates[i] = SamplerState.PointClamp; //will add var later im asleeeeeeeeep
        }


        _device.DrawIndexedPrimitives(
            primitiveType: PrimitiveType.TriangleList,
            baseVertex: 0,
            startIndex: 0,
            primitiveCount: _indexCount / 3
        );

        for (int i = 0; i < _textureCount; i++) {
            _textures[i] = null;
        }

        _vertexCount = 0;
        _indexCount = 0;
        _textureCount = 0;
    }

    private void ensureCapacity(int verticesToAdd, int indicesToAdd) {
        if (_vertexCount + verticesToAdd > _maxVertices ||
            _indexCount + indicesToAdd > _maxIndices) {
            flush();
        }
    }

    private struct ClipState {
        public Vector4 Rect;
        public Vector2 Params;
    }

    private readonly Stack<ClipState> _clipStack = new();
    private ClipState _currentClip = new() { Rect = Vector4.Zero, Params = Vector2.Zero };

    public void PushClip(RectangleF clipRect, float rounding = 0f, float rotation = 0f, bool intersect = true) {
        Vector4 newRect = new(clipRect.Position.X, clipRect.Position.Y, clipRect.Width, clipRect.Height);

        if (intersect && _clipStack.Count > 0 && _currentClip.Rect.Z > 0) {
            float x1 = Math.Max(_currentClip.Rect.X, newRect.X);
            float y1 = Math.Max(_currentClip.Rect.Y, newRect.Y);
            float x2 = Math.Min(_currentClip.Rect.X + _currentClip.Rect.Z, newRect.X + newRect.Z);
            float y2 = Math.Min(_currentClip.Rect.Y + _currentClip.Rect.W, newRect.Y + newRect.W);

            if (x2 >= x1 && y2 >= y1) {
                newRect = new Vector4(x1, y1, x2 - x1, y2 - y1);
            }
            else {
                newRect = Vector4.Zero;
            }
        }

        _currentClip = new ClipState { Rect = newRect, Params = new Vector2(rounding, rotation) };
        _clipStack.Push(_currentClip);
    }

    public void PopClip() {
        if (_clipStack.Count > 0) {
            _clipStack.Pop();
        }
        _currentClip = _clipStack.Count > 0 ? _clipStack.Peek() : new ClipState { Rect = Vector4.Zero, Params = Vector2.Zero };
    }
    private int getTextureIndex(Texture2D texture) {
        for (int i = 0; i < _textureCount; i++) {
            if (_textures[i] == texture) return i;
        }

        if (_textureCount >= 8) {
            flush();
        }

        _textures[_textureCount] = texture;
        return _textureCount++;
    }
    private void addRingSegment(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, PaintStyle paint, int segments) {
        if (segments < 1 || paint.ColorA.A == 0 || outerRadius <= 0) return;

        if (innerRadius <= 0.001f) {
            ensureCapacity(segments + 2, segments * 3);
            int startIdx = _vertexCount;

            _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(center, 0), paint, _currentClip.Rect, _currentClip.Params);

            for (int i = 0; i <= segments; i++) {
                float angle = MathHelper.Lerp(startAngle, endAngle, (float)i / segments);
                (float sin, float cos) = MathF.SinCos(angle);
                Vector2 pos = center + new Vector2(cos, sin) * outerRadius;

                _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(pos, 0), paint, _currentClip.Rect, _currentClip.Params);

                if (i > 0) {
                    _indices[_indexCount++] = (short)startIdx;
                    _indices[_indexCount++] = (short)(startIdx + i);
                    _indices[_indexCount++] = (short)(startIdx + i + 1);
                }
            }
        }
        else {
            ensureCapacity((segments + 1) * 2, segments * 6);
            int startIdx = _vertexCount;

            for (int i = 0; i <= segments; i++) {
                float angle = MathHelper.Lerp(startAngle, endAngle, (float)i / segments);
                (float sin, float cos) = MathF.SinCos(angle);
                Vector2 dir = new(cos, sin);

                _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(center + dir * innerRadius, 0), paint, _currentClip.Rect, _currentClip.Params);
                _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(center + dir * outerRadius, 0), paint, _currentClip.Rect, _currentClip.Params);

                if (i > 0) {
                    int v0 = startIdx + (i - 1) * 2;
                    int v1 = v0 + 1;
                    int v2 = startIdx + i * 2;
                    int v3 = v2 + 1;

                    _indices[_indexCount++] = (short)v0;
                    _indices[_indexCount++] = (short)v1;
                    _indices[_indexCount++] = (short)v2;

                    _indices[_indexCount++] = (short)v1;
                    _indices[_indexCount++] = (short)v3;
                    _indices[_indexCount++] = (short)v2;
                }
            }
        }
    }
    public void DrawLine(Vector2 start, Vector2 end, float thickness, PaintStyle paint, int capSegments = 8) {
        if (thickness <= 0 || paint.ColorA.A == 0) return;

        Vector2 dir = end - start;
        float length = dir.Length();

        float angle = MathF.Atan2(dir.Y, dir.X);

        paint = transformPaint(paint, start, Vector2.Zero, angle);

        float halfThick = thickness * 0.5f;

        if (length < 0.0001f) {
            if (capSegments > 0) {
                addRingSegment(start, 0, halfThick, 0, MathHelper.TwoPi, paint, capSegments * 2);
            }
            return;
        }

        dir /= length;
        Vector2 normal = new(-dir.Y, dir.X);
        Vector2 offset = normal * halfThick;

        ensureCapacity(4, 6);
        int startIdx = _vertexCount;

        _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(start + offset, 0), paint, _currentClip.Rect, _currentClip.Params);
        _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(start - offset, 0), paint, _currentClip.Rect, _currentClip.Params);
        _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(end + offset, 0), paint, _currentClip.Rect, _currentClip.Params);
        _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(end - offset, 0), paint, _currentClip.Rect, _currentClip.Params);

        _indices[_indexCount++] = (short)startIdx;
        _indices[_indexCount++] = (short)(startIdx + 1);
        _indices[_indexCount++] = (short)(startIdx + 2);

        _indices[_indexCount++] = (short)(startIdx + 1);
        _indices[_indexCount++] = (short)(startIdx + 3);
        _indices[_indexCount++] = (short)(startIdx + 2);

        if (capSegments > 0) {
            addRingSegment(start, 0, halfThick, angle + MathHelper.PiOver2, angle + MathHelper.Pi * 1.5f, paint, capSegments);
            addRingSegment(end, 0, halfThick, angle - MathHelper.PiOver2, angle + MathHelper.PiOver2, paint, capSegments);
        }
    }

    public void DrawLine(Vector2 start, Vector2 end, float thickness, Color color, int capSegments = 8)
        => DrawLine(start, end, thickness, PaintStyle.Solid(color), capSegments);

    public void DrawArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, PaintStyle fillPaint, float borderThickness, PaintStyle borderPaint, int segments = 32) {
        if (outerRadius < innerRadius) {
            (innerRadius, outerRadius) = (outerRadius, innerRadius);
        }

        float thickness = outerRadius - innerRadius;
        if (thickness <= 0 || segments < 3) return;

        float midRadius = (innerRadius + outerRadius) * 0.5f;
        float halfThick = thickness * 0.5f;

        float borderThick = Math.Min(borderThickness, halfThick);
        float fillHalfThick = Math.Max(0, halfThick - borderThick);

        bool hasBorder = borderThick > 0 && borderPaint.ColorA.A > 0;
        bool hasFill = fillHalfThick > 0 && fillPaint.ColorA.A > 0;

        if (!hasBorder && !hasFill) return;

        fillPaint = transformPaint(fillPaint, center, Vector2.Zero, 0f);
        borderPaint = transformPaint(borderPaint, center, Vector2.Zero, 0f);

        Vector2 startCenter = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * midRadius;
        Vector2 endCenter = center + new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle)) * midRadius;

        int capSegments = Math.Max(3, segments / 4);

        if (hasBorder) {
            addRingSegment(center, midRadius + fillHalfThick, midRadius + halfThick, startAngle, endAngle, borderPaint, segments);
            addRingSegment(center, midRadius - halfThick, midRadius - fillHalfThick, startAngle, endAngle, borderPaint, segments);
            addRingSegment(startCenter, fillHalfThick, halfThick, startAngle + MathHelper.Pi, startAngle + MathHelper.TwoPi, borderPaint, capSegments);
            addRingSegment(endCenter, fillHalfThick, halfThick, endAngle, endAngle + MathHelper.Pi, borderPaint, capSegments);
        }

        if (hasFill) {
            addRingSegment(center, midRadius - fillHalfThick, midRadius + fillHalfThick, startAngle, endAngle, fillPaint, segments);
            addRingSegment(startCenter, 0, fillHalfThick, startAngle + MathHelper.Pi, startAngle + MathHelper.TwoPi, fillPaint, capSegments);
            addRingSegment(endCenter, 0, fillHalfThick, endAngle, endAngle + MathHelper.Pi, fillPaint, capSegments);
        }
    }

    public void DrawArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, Color fillColor, float borderThickness, Color borderColor, int segments = 32)
        => DrawArc(center, innerRadius, outerRadius, startAngle, endAngle, PaintStyle.Solid(fillColor), borderThickness, PaintStyle.Solid(borderColor), segments);

    public void FillArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, PaintStyle fillPaint, int segments = 32)
        => DrawArc(center, innerRadius, outerRadius, startAngle, endAngle, fillPaint, 0f, default, segments);

    public void FillArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, Color fillColor, int segments = 32)
        => FillArc(center, innerRadius, outerRadius, startAngle, endAngle, PaintStyle.Solid(fillColor), segments);

    public void BorderArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, PaintStyle borderPaint, int segments = 32)
        => DrawArc(center, innerRadius, outerRadius, startAngle, endAngle, default, borderThickness, borderPaint, segments);

    public void BorderArc(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, Color borderColor, int segments = 32)
        => BorderArc(center, innerRadius, outerRadius, startAngle, endAngle, borderThickness, PaintStyle.Solid(borderColor), segments);

    public void DrawCircle(Vector2 center, float radius, PaintStyle fillPaint, float borderThickness, PaintStyle borderPaint, int segments = 32) {
        float innerRadius = Math.Max(0, radius - borderThickness);

        if (innerRadius > 0 && fillPaint.ColorA.A > 0) {
            addRingSegment(center, 0, innerRadius, 0, MathHelper.TwoPi, fillPaint, segments);
        }

        if (borderThickness > 0 && borderPaint.ColorA.A > 0) {
            addRingSegment(center, innerRadius, radius, 0, MathHelper.TwoPi, borderPaint, segments);
        }
    }

    public void DrawCircle(Vector2 center, float radius, Color fillColor, float borderThickness, Color borderColor, int segments = 32)
        => DrawCircle(center, radius, PaintStyle.Solid(fillColor), borderThickness, PaintStyle.Solid(borderColor), segments);

    public void FillCircle(Vector2 center, float radius, PaintStyle fillPaint, int segments = 32)
        => DrawCircle(center, radius, fillPaint, 0f, default, segments);

    public void FillCircle(Vector2 center, float radius, Color fillColor, int segments = 32)
        => FillCircle(center, radius, PaintStyle.Solid(fillColor), segments);

    public void BorderCircle(Vector2 center, float radius, float borderThickness, PaintStyle borderPaint, int segments = 32)
        => DrawCircle(center, radius, default, borderThickness, borderPaint, segments);

    public void BorderCircle(Vector2 center, float radius, float borderThickness, Color borderColor, int segments = 32)
        => BorderCircle(center, radius, borderThickness, PaintStyle.Solid(borderColor), segments);


    // ==================== RECTANGLE ====================

    public void DrawRectangle(Vector2 position, Vector2 size, float radius, PaintStyle fillPaint, float borderThickness, PaintStyle borderPaint, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8) {
        if (size.X <= 0 || size.Y <= 0) return;

        fillPaint = transformPaint(fillPaint, position + size / 2, -size / 2, rotation);
        borderPaint = transformPaint(borderPaint, position + size / 2, -size / 2, rotation);

        float minHalf = Math.Min(size.X, size.Y) * 0.5f;
        radius = Math.Clamp(radius, 0, minHalf);
        borderThickness = Math.Clamp(borderThickness, 0, minHalf);

        bool hasBorder = borderThickness > 0 && borderPaint.ColorA.A > 0;
        bool hasFill = fillPaint.ColorA.A > 0 && borderThickness < minHalf;

        if (!hasBorder && !hasFill) return;

        cornerSegments = radius > 0 ? Math.Max(1, cornerSegments) : 1;
        int perimeterVerts = (cornerSegments + 1) * 4;

        float outR = radius;
        float inR = Math.Max(0, radius - borderThickness);

        Span<Vector2> outCenters = [
            position + new Vector2(size.X - outR, size.Y - outR),
            position + new Vector2(outR, size.Y - outR),
            position + new Vector2(outR, outR),
            position + new Vector2(size.X - outR, outR),
        ];
        Span<Vector2> inCenters = stackalloc Vector2[4];

        if (hasBorder) {
            Vector2 inPos = position + new Vector2(borderThickness, borderThickness);
            Vector2 inSize = size - new Vector2(borderThickness * 2, borderThickness * 2);
            inCenters[0] = inPos + new Vector2(inSize.X - inR, inSize.Y - inR);
            inCenters[1] = inPos + new Vector2(inR, inSize.Y - inR);
            inCenters[2] = inPos + new Vector2(inR, inR);
            inCenters[3] = inPos + new Vector2(inSize.X - inR, inR);
        }

        Span<float> startAngles = [0, MathHelper.PiOver2, MathHelper.Pi, MathHelper.Pi * 1.5f];
        float step = MathHelper.PiOver2 / cornerSegments;

        float rotSin = 0, rotCos = 1;
        bool hasRotation = rotation != 0f;
        if (hasRotation) {
            rotSin = MathF.Sin(rotation);
            rotCos = MathF.Cos(rotation);
        }

        Vector2 Transform(Vector2 p) {
            if (!hasRotation) return p;
            float rx = p.X - (position.X + origin.X);
            float ry = p.Y - (position.Y + origin.Y);
            return new Vector2(
                position.X + origin.X + rx * rotCos - ry * rotSin,
                position.Y + origin.Y + rx * rotSin + ry * rotCos
            );
        }

        if (hasFill) {
            ensureCapacity(perimeterVerts + 1, perimeterVerts * 3);
            int startIdx = _vertexCount;
            _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(Transform(position + size * 0.5f), 0), fillPaint, _currentClip.Rect, _currentClip.Params);

            Span<Vector2> fillCenters = hasBorder ? inCenters : outCenters;
            float fillR = hasBorder ? inR : outR;

            int vertCounter = 0;
            for (int c = 0; c < 4; c++) {
                for (int i = 0; i <= cornerSegments; i++) {
                    float angle = startAngles[c] + i * step;
                    (float sin, float cos) = MathF.SinCos(angle);
                    Vector2 pos = fillCenters[c] + new Vector2(cos, sin) * fillR;

                    _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(Transform(pos), 0), fillPaint, _currentClip.Rect, _currentClip.Params);

                    _indices[_indexCount++] = (short)startIdx;
                    _indices[_indexCount++] = (short)(startIdx + vertCounter + 1);
                    _indices[_indexCount++] = (short)(startIdx + (vertCounter + 1) % perimeterVerts + 1);
                    vertCounter++;
                }
            }
        }

        if (hasBorder) {
            ensureCapacity(perimeterVerts * 2, perimeterVerts * 6);
            int startIdx = _vertexCount;
            int vertCounter = 0;

            for (int c = 0; c < 4; c++) {
                for (int i = 0; i <= cornerSegments; i++) {
                    float angle = startAngles[c] + i * step;
                    (float sin, float cos) = MathF.SinCos(angle);
                    Vector2 dir = new(cos, sin);

                    Vector2 inPos = inCenters[c] + dir * inR;
                    Vector2 outPos = outCenters[c] + dir * outR;

                    _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(Transform(inPos), 0), borderPaint, _currentClip.Rect, _currentClip.Params);
                    _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(Transform(outPos), 0), borderPaint, _currentClip.Rect, _currentClip.Params);

                    int nextI = (vertCounter + 1) % perimeterVerts;
                    int v0 = startIdx + vertCounter * 2;
                    int v1 = v0 + 1;
                    int v2 = startIdx + nextI * 2;
                    int v3 = v2 + 1;

                    _indices[_indexCount++] = (short)v0;
                    _indices[_indexCount++] = (short)v1;
                    _indices[_indexCount++] = (short)v2;

                    _indices[_indexCount++] = (short)v1;
                    _indices[_indexCount++] = (short)v3;
                    _indices[_indexCount++] = (short)v2;

                    vertCounter++;
                }
            }
        }
    }

    public void DrawRectangle(Vector2 position, Vector2 size, float radius, Color fillColor, float borderThickness, Color borderColor, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => DrawRectangle(position, size, radius, PaintStyle.Solid(fillColor), borderThickness, PaintStyle.Solid(borderColor), rotation, origin, cornerSegments);

    public void FillRectangle(Vector2 position, Vector2 size, float radius, PaintStyle fillPaint, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => DrawRectangle(position, size, radius, fillPaint, 0f, default, rotation, origin, cornerSegments);

    public void FillRectangle(Vector2 position, Vector2 size, float radius, Color fillColor, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => FillRectangle(position, size, radius, PaintStyle.Solid(fillColor), rotation, origin, cornerSegments);

    public void BorderRectangle(Vector2 position, Vector2 size, float radius, float borderThickness, PaintStyle borderPaint, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => DrawRectangle(position, size, radius, default, borderThickness, borderPaint, rotation, origin, cornerSegments);

    public void BorderRectangle(Vector2 position, Vector2 size, float radius, float borderThickness, Color borderColor, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => BorderRectangle(position, size, radius, borderThickness, PaintStyle.Solid(borderColor), rotation, origin, cornerSegments);

    private static PaintStyle transformPaint(PaintStyle paint, Vector2 center, Vector2 offset, float rotation) {
        if (!paint.IsLocal || paint.Type == PaintStyle.PaintType.Solid)
            return paint;

        Vector2 p1 = new Vector2(paint.Points.X, paint.Points.Y) + offset;
        Vector2 p2 = new Vector2(paint.Points.Z, paint.Points.W) + offset;

        if (rotation != 0f) {
            var (sin, cos) = MathF.SinCos(rotation);

            Vector2 r1 = new(p1.X * cos - p1.Y * sin, p1.X * sin + p1.Y * cos);
            Vector2 r2 = new(p2.X * cos - p2.Y * sin, p2.X * sin + p2.Y * cos);

            p1 = r1;
            p2 = r2;
        }

        p1 += center;
        p2 += center;

        paint.Points = new Vector4(p1.X, p1.Y, p2.X, p2.Y);
        return paint;
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Vector2? size = null, Rectangle? sourceRect = null, Color? tint = null, float rotation = 0f, Vector2 origin = default, float radius = 0f, int cornerSegments = 8) {
        Vector2 actualSize = size ?? new Vector2(texture.Width, texture.Height);
        if (actualSize.X <= 0 || actualSize.Y <= 0) return;

        Color actualTint = tint ?? Color.White;
        if (actualTint.A == 0) return;

        int texIndex = getTextureIndex(texture);

        float minHalf = Math.Min(actualSize.X, actualSize.Y) * 0.5f;
        radius = Math.Clamp(radius, 0, minHalf);
        cornerSegments = radius > 0 ? Math.Max(1, cornerSegments) : 1;

        int perimeterVerts = (cornerSegments + 1) * 4;
        ensureCapacity(perimeterVerts + 1, perimeterVerts * 3);

        float outR = radius;
        Span<Vector2> outCenters = [
            position + new Vector2(actualSize.X - outR, actualSize.Y - outR),
            position + new Vector2(outR, actualSize.Y - outR),
            position + new Vector2(outR, outR),
            position + new Vector2(actualSize.X - outR, outR),
        ];
        Span<float> startAngles = [0, MathHelper.PiOver2, MathHelper.Pi, MathHelper.Pi * 1.5f];
        float step = MathHelper.PiOver2 / cornerSegments;

        float rotSin = 0, rotCos = 1;
        bool hasRotation = rotation != 0f;
        if (hasRotation) {
            rotSin = MathF.Sin(rotation);
            rotCos = MathF.Cos(rotation);
        }

        Vector2 Transform(Vector2 p) {
            if (!hasRotation) return p;
            float rx = p.X - (position.X + origin.X);
            float ry = p.Y - (position.Y + origin.Y);
            return new Vector2(
                position.X + origin.X + rx * rotCos - ry * rotSin,
                position.Y + origin.Y + rx * rotSin + ry * rotCos
            );
        }

        RectangleF src = sourceRect.HasValue ?
            new RectangleF(sourceRect.Value.X, sourceRect.Value.Y, sourceRect.Value.Width, sourceRect.Value.Height) :
            new RectangleF(0, 0, texture.Width, texture.Height);

        Vector2 uvMin = new(src.X / texture.Width, src.Y / texture.Height);
        Vector2 uvMax = new((src.X + src.Width) / texture.Width, (src.Y + src.Height) / texture.Height);

        Vector2 GetUV(Vector2 p) {
            float tx = (p.X - position.X) / actualSize.X;
            float ty = (p.Y - position.Y) / actualSize.Y;
            return new Vector2(
                MathHelper.Lerp(uvMin.X, uvMax.X, tx),
                MathHelper.Lerp(uvMin.Y, uvMax.Y, ty)
            );
        }

        int startIdx = _vertexCount;

        Vector2 centerPos = position + actualSize * 0.5f;
        _vertices[_vertexCount++] = new PrimitiveVertex(
            new Vector3(Transform(centerPos), 0),
            new Vector4(GetUV(centerPos), texIndex, 0),
            actualTint, 3f, _currentClip.Rect, _currentClip.Params);

        int vertCounter = 0;
        for (int c = 0; c < 4; c++) {
            for (int i = 0; i <= cornerSegments; i++) {
                float angle = startAngles[c] + i * step;
                (float sin, float cos) = MathF.SinCos(angle);
                Vector2 pos = outCenters[c] + new Vector2(cos, sin) * outR;

                _vertices[_vertexCount++] = new PrimitiveVertex(
                    new Vector3(Transform(pos), 0),
                    new Vector4(GetUV(pos), texIndex, 0),
                    actualTint, 3f, _currentClip.Rect, _currentClip.Params);

                _indices[_indexCount++] = (short)startIdx;
                _indices[_indexCount++] = (short)(startIdx + vertCounter + 1);
                _indices[_indexCount++] = (short)(startIdx + (vertCounter + 1) % perimeterVerts + 1);
                vertCounter++;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrimitiveVertex : IVertexType {
        public Vector3 Position;
        public Vector4 ClipRect;
        public Vector2 ClipParams;
        public Vector4 ColorA;
        public Vector4 ColorB;
        public Vector4 GradientData;
        public float PaintType;

        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
            new VertexElement(52, VertexElementFormat.Vector4, VertexElementUsage.Color, 1),
            new VertexElement(68, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(84, VertexElementFormat.Single, VertexElementUsage.TextureCoordinate, 3)
        );

        public PrimitiveVertex(Vector3 pos, PaintStyle paint, Vector4 clipRect, Vector2 clipParams) {
            Position = pos;
            ClipRect = clipRect;
            ClipParams = clipParams;
            ColorA = paint.ColorA.ToVector4();
            ColorB = paint.ColorB.ToVector4();
            GradientData = paint.Points;
            PaintType = (float)paint.Type;
        }

        public PrimitiveVertex(Vector3 pos, Vector4 gradientData, Color tint, float paintType, Vector4 clipRect, Vector2 clipParams) {
            Position = pos;
            ClipRect = clipRect;
            ClipParams = clipParams;
            ColorA = tint.ToVector4();
            ColorB = Vector4.Zero;
            GradientData = gradientData;
            PaintType = paintType;
        }

        readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}

