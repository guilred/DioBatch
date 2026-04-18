using DioUI.RectangleFNS;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DioUI;

public class DioBatch {
    private const int _maxVertices = 8192;
    private const int _maxIndices = _maxVertices * 3;

    private readonly GraphicsDevice _device;

    private readonly Effect _effect;
    private readonly EffectPass _pass;
    private readonly EffectParameter _projectionParam;
    private BlendState _currentBlendState = BlendState.AlphaBlend;
    private SamplerState _currentSamplerState = SamplerState.LinearClamp;
    private readonly SamplerState[] _prevSamplerStatesBuffer = new SamplerState[8];

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

    public void Begin(Matrix? view = null, Matrix? projection = null, BlendState? blendState = null, SamplerState? samplerState = null) {
        if (_begun) throw new InvalidOperationException("DioBatch is already begun.");

        projection = (view ?? Matrix.Identity) * (projection ?? Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, 0f, 1f));

        _projectionParam.SetValue(projection.Value);
        _vertexCount = 0;
        _indexCount = 0;
        _currentBlendState = blendState ?? BlendState.AlphaBlend;
        _currentSamplerState = samplerState ?? SamplerState.LinearClamp;
        _begun = true;
    }

    public void End(bool maintainClipRects = false) {
        if (!_begun) throw new InvalidOperationException("DioBatch has not been begun.");

        flush();
        _begun = false;
        if (!maintainClipRects) {
            _clipStack.Clear();
            _currentClip = new ClipState { Rect = Vector4.Zero, Params = Vector2.Zero };
        }
    }

    private void flush() {
        if (_vertexCount == 0) return;

        _vertexBuffer.SetData(_vertices, 0, _vertexCount, SetDataOptions.Discard);
        _indexBuffer.SetData(_indices, 0, _indexCount, SetDataOptions.Discard);

        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        (var previousBlendState, _device.BlendState) = (_device.BlendState, _currentBlendState);
        (var previousRasterizerState, _device.RasterizerState) = (_device.RasterizerState, RasterizerState.CullNone);

        _pass.Apply();
        for (int i = 0; i < _textureCount; i++) {
            _device.Textures[i] = _textures[i];
            _prevSamplerStatesBuffer[i] = _device.SamplerStates[i];
            _device.SamplerStates[i] = _currentSamplerState;
        }


        _device.DrawIndexedPrimitives(
            primitiveType: PrimitiveType.TriangleList,
            baseVertex: 0,
            startIndex: 0,
            primitiveCount: _indexCount / 3
        );

        for (int i = 0; i < _textureCount; i++) {
            _textures[i] = null;
            _device.SamplerStates[i] = _prevSamplerStatesBuffer[i];
        }
        _device.BlendState = previousBlendState;
        _device.RasterizerState = previousRasterizerState;

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
    public void DrawLine(Vector2 start, Vector2 end, PaintStyle paint, PaintStyle borderPaint, float thickness, float borderThickness, int capSegments = 8) {
        if (thickness <= 0) return;

        Vector2 dir = end - start;
        float length = dir.Length();
        float angle = MathF.Atan2(dir.Y, dir.X);

        Vector2 size = new(length + thickness, thickness);

        Vector2 origin = new(thickness * 0.5f, thickness * 0.5f);
        Vector2 position = start - origin;

        paint = transformPaint(paint, origin, Vector2.Zero, 0);
        borderPaint = transformPaint(borderPaint, origin, Vector2.Zero, 0);

        DrawRectangle(position, size, paint, borderPaint, borderThickness, rounding: thickness * 0.5f, angle, origin, capSegments);
    }

    public void DrawLine(Vector2 start, Vector2 end, Color color, Color borderColor, float thickness, float borderThickness, int capSegments = 8)
        => DrawLine(start, end, PaintStyle.Solid(color), PaintStyle.Solid(borderColor), thickness, borderThickness, capSegments);

    public void FillLine(Vector2 start, Vector2 end, PaintStyle paint, float thickness, int capSegments = 8)
        => DrawLine(start, end, paint, default, thickness, 0f, capSegments);

    public void FillLine(Vector2 start, Vector2 end, Color color, float thickness, int capSegments = 8)
        => FillLine(start, end, PaintStyle.Solid(color), thickness, capSegments);

    public void BorderLine(Vector2 start, Vector2 end, PaintStyle borderPaint, float thickness, float borderThickness, int capSegments = 8)
        => DrawLine(start, end, default, borderPaint, thickness, borderThickness, capSegments);

    public void BorderLine(Vector2 start, Vector2 end, Color borderColor, float thickness, float borderThickness, int capSegments = 8)
        => BorderLine(start, end, PaintStyle.Solid(borderColor), thickness, borderThickness, capSegments);

    public void DrawArc(Vector2 center, PaintStyle fillPaint, PaintStyle borderPaint, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 32) {
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

    public void DrawArc(Vector2 center, Color fillColor, Color borderColor, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 32)
    => DrawArc(center, PaintStyle.Solid(fillColor), PaintStyle.Solid(borderColor), innerRadius, outerRadius, startAngle, endAngle, borderThickness, segments);

    public void FillArc(Vector2 center, PaintStyle fillPaint, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments = 32)
        => DrawArc(center, fillPaint, default, innerRadius, outerRadius, startAngle, endAngle, 0f, segments);

    public void FillArc(Vector2 center, Color fillColor, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments = 32)
        => FillArc(center, PaintStyle.Solid(fillColor), innerRadius, outerRadius, startAngle, endAngle, segments);

    public void BorderArc(Vector2 center, PaintStyle borderPaint, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 32)
        => DrawArc(center, default, borderPaint, innerRadius, outerRadius, startAngle, endAngle, borderThickness, segments);

    public void BorderArc(Vector2 center, Color borderColor, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 32)
        => BorderArc(center, PaintStyle.Solid(borderColor), innerRadius, outerRadius, startAngle, endAngle, borderThickness, segments);

    public void DrawCircle(Vector2 center, PaintStyle fillPaint, PaintStyle borderPaint, float radius, float borderThickness, int segments = 32) {
        float innerRadius = Math.Max(0, radius - borderThickness);

        if (innerRadius > 0) {
            fillPaint = transformPaint(fillPaint, center, Vector2.Zero, 0f);
            addRingSegment(center, 0, innerRadius, 0, MathHelper.TwoPi, fillPaint, segments);
        }

        if (borderThickness > 0 && borderPaint.ColorA.A > 0) {
            borderPaint = transformPaint(borderPaint, center, Vector2.Zero, 0f);
            addRingSegment(center, innerRadius, radius, 0, MathHelper.TwoPi, borderPaint, segments);
        }
    }

    public void DrawCircle(Vector2 center, Color fillColor, Color borderColor, float radius, float borderThickness, int segments = 32)
        => DrawCircle(center, PaintStyle.Solid(fillColor), PaintStyle.Solid(borderColor), radius, borderThickness, segments);

    public void FillCircle(Vector2 center, PaintStyle fillPaint, float radius, int segments = 32)
        => DrawCircle(center, fillPaint, default, radius, 0f, segments);

    public void FillCircle(Vector2 center, Color fillColor, float radius, int segments = 32)
        => FillCircle(center, PaintStyle.Solid(fillColor), radius, segments);

    public void BorderCircle(Vector2 center, PaintStyle borderPaint, float radius, float borderThickness, int segments = 32)
        => DrawCircle(center, default, borderPaint, radius, borderThickness, segments);

    public void BorderCircle(Vector2 center, Color borderColor, float radius, float borderThickness, int segments = 32)
        => BorderCircle(center, PaintStyle.Solid(borderColor), radius, borderThickness, segments);

    public void DrawRectangle(Vector2 position, Vector2 size, PaintStyle fillPaint, PaintStyle borderPaint, float borderThickness, float rounding, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8) {
        if (size.X <= 0 || size.Y <= 0) return;


        fillPaint = transformPaint(fillPaint, position + origin, -origin, rotation);
        borderPaint = transformPaint(borderPaint, position + origin, -origin, rotation);

        float minHalf = Math.Min(size.X, size.Y) * 0.5f;
        rounding = Math.Clamp(rounding, 0, minHalf);
        borderThickness = Math.Clamp(borderThickness, 0, minHalf);

        bool hasBorder = borderThickness > 0 && borderPaint.ColorA.A > 0;
        bool hasFill = fillPaint.ColorA.A > 0 && borderThickness < minHalf;

        if (!hasBorder && !hasFill) return;

        cornerSegments = rounding > 0 ? Math.Max(1, cornerSegments) : 1;
        int perimeterVerts = (cornerSegments + 1) * 4;

        float outR = rounding;
        float inR = Math.Max(0, rounding - borderThickness);

        Span<Vector2> outCenters = [
            position + new Vector2(size.X - outR, size.Y - outR),
            position + new Vector2(outR, size.Y - outR),
            position + new Vector2(outR, outR),
            position + new Vector2(size.X - outR, outR),
        ];
        Span<Vector2> inCenters = [Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero];

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

    public void DrawRectangle(Vector2 position, Vector2 size, Color fillColor, Color borderColor, float borderThickness, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
    => DrawRectangle(position, size, PaintStyle.Solid(fillColor), PaintStyle.Solid(borderColor), borderThickness, rounding, rotation, origin, cornerSegments);

    public void FillRectangle(Vector2 position, Vector2 size, PaintStyle fillPaint, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => DrawRectangle(position, size, fillPaint, default, 0, rounding, rotation, origin, cornerSegments);

    public void FillRectangle(Vector2 position, Vector2 size, Color fillColor, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => FillRectangle(position, size, PaintStyle.Solid(fillColor), rounding, rotation, origin, cornerSegments);

    public void BorderRectangle(Vector2 position, Vector2 size, PaintStyle borderPaint, float borderThickness, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => DrawRectangle(position, size, default, borderPaint, borderThickness, rounding, rotation, origin, cornerSegments);

    public void BorderRectangle(Vector2 position, Vector2 size, Color borderColor, float borderThickness, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 8)
        => BorderRectangle(position, size, PaintStyle.Solid(borderColor), borderThickness, rounding, rotation, origin, cornerSegments);

    private static PaintStyle transformPaint(PaintStyle paint, Vector2 center, Vector2 offset, float rotation) {
        if (!paint.IsLocal || paint.Type == PaintStyle.PaintType.Solid)
            return paint;

        Vector2 p1 = new Vector2(paint.Start.X, paint.Start.Y) + offset;
        Vector2 p2 = new Vector2(paint.End.X, paint.End.Y) + offset;

        if (rotation != 0f) {
            var (sin, cos) = MathF.SinCos(rotation);

            Vector2 r1 = new(p1.X * cos - p1.Y * sin, p1.X * sin + p1.Y * cos);
            Vector2 r2 = new(p2.X * cos - p2.Y * sin, p2.X * sin + p2.Y * cos);

            p1 = r1;
            p2 = r2;
        }

        p1 += center;
        p2 += center;

        paint.Start = new Vector2(p1.X, p1.Y);
        paint.End = new Vector2(p2.X, p2.Y);
        return paint;
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Vector2? size = null, Rectangle? sourceRect = null, Color? tint = null, float rotation = 0f, Vector2 origin = default, SpriteEffects effects = SpriteEffects.None, float rounding = 0f, int cornerSegments = 8) {
        Vector2 actualSize = size ?? new Vector2(texture.Width, texture.Height);
        if (actualSize.X <= 0 || actualSize.Y <= 0) return;

        Color actualTint = tint ?? Color.White;
        if (actualTint.A == 0) return;

        int texIndex = getTextureIndex(texture);

        float minHalf = Math.Min(actualSize.X, actualSize.Y) * 0.5f;
        rounding = Math.Clamp(rounding, 0, minHalf);
        cornerSegments = rounding > 0 ? Math.Max(1, cornerSegments) : 1;

        int perimeterVerts = (cornerSegments + 1) * 4;
        ensureCapacity(perimeterVerts + 1, perimeterVerts * 3);

        float outR = rounding;
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

        bool flipH = (effects & SpriteEffects.FlipHorizontally) != 0;
        bool flipV = (effects & SpriteEffects.FlipVertically) != 0;

        Vector2 GetUV(Vector2 p) {
            float tx = (p.X - position.X) / actualSize.X;
            float ty = (p.Y - position.Y) / actualSize.Y;

            if (flipH) tx = 1f - tx;
            if (flipV) ty = 1f - ty;

            return new Vector2(
                MathHelper.Lerp(uvMin.X, uvMax.X, tx),
                MathHelper.Lerp(uvMin.Y, uvMax.Y, ty)
            );
        }

        int startIdx = _vertexCount;
        Vector2 centerPos = position + actualSize * 0.5f;

        _vertices[_vertexCount++] = new PrimitiveVertex(
            new Vector3(Transform(centerPos), 0f),
            new Vector4(GetUV(centerPos), texIndex, 0),
            actualTint, 3f, _currentClip.Rect, _currentClip.Params);

        int vertCounter = 0;
        for (int c = 0; c < 4; c++) {
            for (int i = 0; i <= cornerSegments; i++) {
                float angle = startAngles[c] + i * step;
                (float sin, float cos) = MathF.SinCos(angle);
                Vector2 pos = outCenters[c] + new Vector2(cos, sin) * outR;

                _vertices[_vertexCount++] = new PrimitiveVertex(
                    new Vector3(Transform(pos), 0f),
                    new Vector4(GetUV(pos), texIndex, 0),
                    actualTint, 3f, _currentClip.Rect, _currentClip.Params);

                _indices[_indexCount++] = (short)startIdx;
                _indices[_indexCount++] = (short)(startIdx + vertCounter + 1);
                _indices[_indexCount++] = (short)(startIdx + (vertCounter + 1) % perimeterVerts + 1);
                vertCounter++;
            }
        }
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Color color, float rounding = 0f, int cornerSegments = 8) {
        DrawTexture(texture, position, null, null, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Rectangle destinationRectangle, Color color, float rounding = 0f, int cornerSegments = 8) {
        DrawTexture(texture, new Vector2(destinationRectangle.X, destinationRectangle.Y), new Vector2(destinationRectangle.Width, destinationRectangle.Height), null, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rounding = 0f, int cornerSegments = 8) {
        Vector2 size = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        DrawTexture(texture, position, size, sourceRectangle, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rounding = 0f, int cornerSegments = 8) {
        DrawTexture(texture, new Vector2(destinationRectangle.X, destinationRectangle.Y), new Vector2(destinationRectangle.Width, destinationRectangle.Height), sourceRectangle, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float rounding = 0f, int cornerSegments = 8) {
        Vector2 srcSize = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        DrawTexture(texture, position, srcSize * scale, sourceRectangle, color, rotation, origin * scale, effects, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float rounding = 0f, int cornerSegments = 8) {
        Vector2 srcSize = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        DrawTexture(texture, position, srcSize * scale, sourceRectangle, color, rotation, origin * scale, effects, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float rounding = 0f, int cornerSegments = 8) {
        Vector2 srcSize = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        Vector2 destSize = new(destinationRectangle.Width, destinationRectangle.Height);
        Vector2 scale = new(destSize.X / srcSize.X, destSize.Y / srcSize.Y);

        DrawTexture(texture, new Vector2(destinationRectangle.X, destinationRectangle.Y), destSize, sourceRectangle, color, rotation, origin * scale, effects, rounding, cornerSegments);
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PrimitiveVertex : IVertexType {
        public Vector3 Position;
        public Vector4 ClipRect;
        public Vector2 ClipParams;
        public Vector4 ColorA;
        public Vector4 ColorB;
        public Vector4 GradientData;
        public Vector3 PaintParams;

        public static readonly VertexDeclaration VertexDeclaration = new(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(28, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(36, VertexElementFormat.Vector4, VertexElementUsage.Color, 0),
            new VertexElement(52, VertexElementFormat.Vector4, VertexElementUsage.Color, 1),
            new VertexElement(68, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(84, VertexElementFormat.Vector3, VertexElementUsage.TextureCoordinate, 3)
        );

        public PrimitiveVertex(Vector3 pos, PaintStyle paint, Vector4 clipRect, Vector2 clipParams) {
            Position = pos;
            ClipRect = clipRect;
            ClipParams = clipParams;
            ColorA = paint.ColorA.ToVector4();
            ColorB = paint.ColorB.ToVector4();
            GradientData = new(paint.Start.X, paint.Start.Y, paint.End.X, paint.End.Y);
            float safePower = Math.Clamp(paint.EasingPower, 0f, 99.9f);
            float packedData = ((float)paint.Type * 1000f) + ((float)paint.Easing * 100f) + safePower;
            PaintParams = new Vector3(paint.OffsetsA, paint.OffsetsB, packedData);
        }

        public PrimitiveVertex(Vector3 pos, Vector4 gradientData, Color tint, float paintType, Vector4 clipRect, Vector2 clipParams) {
            Position = pos;
            ClipRect = clipRect;
            ClipParams = clipParams;
            ColorA = tint.ToVector4();
            ColorB = Vector4.Zero;
            GradientData = gradientData;
            PaintParams = new Vector3(0f, 0f, paintType * 1000f);
        }

        readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}