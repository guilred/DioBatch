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
    // debugging
    private double time;
    private bool blink => double.Sin(time * 5) > 0;
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
        time += 1 / 60f;
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
                float angle = float.Lerp(startAngle, endAngle, (float)i / segments);
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
                float angle = float.Lerp(startAngle, endAngle, (float)i / segments);
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

    private void addRectFringe(Span<Vector2> centers, float radius, int cornerSegments, PaintStyle paint, bool outer, bool hasRotation, float rotSin, float rotCos, Vector2 pivot) {
        int perimeterVerts = (cornerSegments + 1) * 4;
        ensureCapacity(perimeterVerts * 2, perimeterVerts * 6);

        float step = MathHelper.PiOver2 / cornerSegments;
        int fringeStart = _vertexCount;

        for (int c = 0; c < 4; c++) {
            float startAngle = c * MathHelper.PiOver2;
            for (int i = 0; i <= cornerSegments; i++) {
                float angle = startAngle + i * step;
                (float sin, float cos) = MathF.SinCos(angle);
                Vector2 dir = new(cos, sin);
                Vector2 basePos = centers[c] + dir * radius;

                Vector2 worldBase, worldFringe;
                if (hasRotation) {
                    float rx = basePos.X - pivot.X, ry = basePos.Y - pivot.Y;
                    worldBase = new Vector2(pivot.X + rx * rotCos - ry * rotSin, pivot.Y + rx * rotSin + ry * rotCos);

                    float wdx = dir.X * rotCos - dir.Y * rotSin;
                    float wdy = dir.X * rotSin + dir.Y * rotCos;
                    worldFringe = outer
                        ? new Vector2(worldBase.X + wdx, worldBase.Y + wdy)
                        : new Vector2(worldBase.X - wdx, worldBase.Y - wdy);
                }
                else {
                    worldBase = basePos;
                    worldFringe = outer ? basePos + dir : basePos - dir;
                }

                _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(worldBase, 0), paint, _currentClip.Rect, _currentClip.Params);

                _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(worldFringe, 0), paint, _currentClip.Rect, _currentClip.Params) {
                    ColorA = Vector4.Zero,
                    ColorB = Vector4.Zero
                };
            }
        }

        for (int k = 0; k < perimeterVerts; k++) {
            int next = (k + 1) % perimeterVerts;
            int v0 = fringeStart + k * 2;
            int v1 = fringeStart + k * 2 + 1;
            int v2 = fringeStart + next * 2;
            int v3 = fringeStart + next * 2 + 1;

            _indices[_indexCount++] = (short)v0;
            _indices[_indexCount++] = (short)v1;
            _indices[_indexCount++] = (short)v2;

            _indices[_indexCount++] = (short)v1;
            _indices[_indexCount++] = (short)v3;
            _indices[_indexCount++] = (short)v2;
        }
    }

    private void addCircleFringe(Vector2 center, float radius, float startAngle, float endAngle,
        PaintStyle paint, int segments, bool outer) {
        if (segments < 1 || radius <= 0f) return;

        float fringeRadius = outer ? radius + 1f : Math.Max(0f, radius - 1f);
        if (!outer && fringeRadius >= radius) return;

        ensureCapacity((segments + 1) * 2, segments * 6);
        int fringeStart = _vertexCount;

        for (int i = 0; i <= segments; i++) {
            float angle = float.Lerp(startAngle, endAngle, (float)i / segments);
            (float sin, float cos) = MathF.SinCos(angle);
            Vector2 dir = new(cos, sin);

            _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(center + dir * radius, 0), paint, _currentClip.Rect, _currentClip.Params);

            _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(center + dir * fringeRadius, 0), paint, _currentClip.Rect, _currentClip.Params) {
                ColorA = Vector4.Zero,
                ColorB = Vector4.Zero
            };
        }

        for (int i = 0; i < segments; i++) {
            int v0 = fringeStart + i * 2;
            int v1 = fringeStart + i * 2 + 1;
            int v2 = fringeStart + (i + 1) * 2;
            int v3 = fringeStart + (i + 1) * 2 + 1;

            _indices[_indexCount++] = (short)v0;
            _indices[_indexCount++] = (short)v1;
            _indices[_indexCount++] = (short)v2;

            _indices[_indexCount++] = (short)v1;
            _indices[_indexCount++] = (short)v3;
            _indices[_indexCount++] = (short)v2;
        }
    }

    public void DrawRectangle(Vector2 position, Vector2 size, PaintStyle fillPaint, PaintStyle borderPaint, float borderThickness, float rounding, float rotation = 0f, Vector2 origin = default, int cornerSegments = 12, bool enableAA = true) {
        if (size.X <= 0 || size.Y <= 0) return;


        fillPaint = transformPaint(fillPaint, position + origin, -origin, rotation);
        borderPaint = transformPaint(borderPaint, position + origin, -origin, rotation);

        float minHalf = Math.Min(size.X, size.Y) * 0.5f;
        rounding = Math.Clamp(rounding, 0, minHalf);
        borderThickness = Math.Clamp(borderThickness, 0, minHalf);

        bool bpTr = borderPaint.IsTrspt();
        bool hasBorder = borderThickness > 0 && !bpTr;
        bool hasFill = borderThickness < minHalf && !fillPaint.IsTrspt();

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

        if (!bpTr) {
            Vector2 inPos = position + new Vector2(borderThickness, borderThickness);
            Vector2 inSize = size - new Vector2(borderThickness * 2, borderThickness * 2);
            inCenters[0] = inPos + new Vector2(inSize.X - inR, inSize.Y - inR);
            inCenters[1] = inPos + new Vector2(inR, inSize.Y - inR);
            inCenters[2] = inPos + new Vector2(inR, inR);
            inCenters[3] = inPos + new Vector2(inSize.X - inR, inR);
        }

        Span<float> startAngles = [0, MathHelper.PiOver2, float.Pi, float.Pi * 1.5f];
        float step = MathHelper.PiOver2 / cornerSegments;

        float rotSin = 0, rotCos = 1;
        bool hasRotation = rotation != 0f;
        if (hasRotation) {
            rotSin = MathF.Sin(rotation);
            rotCos = MathF.Cos(rotation);
        }

        Vector2 transform(Vector2 p) {
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
            _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(transform(position + size * 0.5f), 0), fillPaint, _currentClip.Rect, _currentClip.Params);

            Span<Vector2> fillCenters = !bpTr ? inCenters : outCenters;
            float fillR = bpTr ? inR : outR;

            int vertCounter = 0;
            for (int c = 0; c < 4; c++) {
                for (int i = 0; i <= cornerSegments; i++) {
                    float angle = startAngles[c] + i * step;
                    (float sin, float cos) = MathF.SinCos(angle);
                    Vector2 pos = fillCenters[c] + new Vector2(cos, sin) * fillR;

                    _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(transform(pos), 0), fillPaint, _currentClip.Rect, _currentClip.Params);

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

                    _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(transform(inPos), 0), borderPaint, _currentClip.Rect, _currentClip.Params);
                    _vertices[_vertexCount++] = new PrimitiveVertex(new Vector3(transform(outPos), 0), borderPaint, _currentClip.Rect, _currentClip.Params);

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

        if (!enableAA) return;

        Vector2 aaPivot = position + origin;

        if (hasBorder) {
            addRectFringe(outCenters, outR, cornerSegments, borderPaint, true, hasRotation, rotSin, rotCos, aaPivot);
            bool bpOp = borderPaint.IsOpaque();
            if (bpOp || !hasFill) {
                addRectFringe(inCenters, inR, cornerSegments, borderPaint, false, hasRotation, rotSin, rotCos, aaPivot);
            }
            if (!bpOp && fillPaint.IsOpaque()) {
                addRectFringe(inCenters, inR, cornerSegments, fillPaint, true, hasRotation, rotSin, rotCos, aaPivot);
            }
        }
        else if (hasFill) {
            addRectFringe(outCenters, inR, cornerSegments, fillPaint, true, hasRotation, rotSin, rotCos, aaPivot);
        }
    }

    public void DrawRectangle(Vector2 position, Vector2 size, Color fillColor, Color borderColor, float borderThickness, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 12, bool enableAA = true)
        => DrawRectangle(position, size, PaintStyle.Solid(fillColor), PaintStyle.Solid(borderColor), borderThickness, rounding, rotation, origin, cornerSegments, enableAA);

    public void FillRectangle(Vector2 position, Vector2 size, PaintStyle fillPaint, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 12, bool enableAA = true)
        => DrawRectangle(position, size, fillPaint, default, 0, rounding, rotation, origin, cornerSegments, enableAA);

    public void FillRectangle(Vector2 position, Vector2 size, Color fillColor, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 12, bool enableAA = true)
        => FillRectangle(position, size, PaintStyle.Solid(fillColor), rounding, rotation, origin, cornerSegments, enableAA);

    public void BorderRectangle(Vector2 position, Vector2 size, PaintStyle borderPaint, float borderThickness, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 12, bool enableAA = true)
        => DrawRectangle(position, size, default, borderPaint, borderThickness, rounding, rotation, origin, cornerSegments, enableAA);

    public void BorderRectangle(Vector2 position, Vector2 size, Color borderColor, float borderThickness, float rounding = 0f, float rotation = 0f, Vector2 origin = default, int cornerSegments = 12, bool enableAA = true)
        => BorderRectangle(position, size, PaintStyle.Solid(borderColor), borderThickness, rounding, rotation, origin, cornerSegments, enableAA);

    public void DrawLine(Vector2 start, Vector2 end, PaintStyle paint, PaintStyle borderPaint, float thickness, float borderThickness, int capSegments = 8, bool enableAA = true) {
        if (thickness <= 0) return;

        Vector2 dir = end - start;
        float length = dir.Length();
        float angle = MathF.Atan2(dir.Y, dir.X);

        Vector2 size = new(length + thickness, thickness);

        Vector2 origin = new(thickness * 0.5f, thickness * 0.5f);
        Vector2 position = start - origin;

        paint = transformPaint(paint, origin, Vector2.Zero, 0);
        borderPaint = transformPaint(borderPaint, origin, Vector2.Zero, 0);

        DrawRectangle(position, size, paint, borderPaint, borderThickness, rounding: thickness * 0.5f, angle, origin, capSegments, enableAA);
    }

    public void DrawLine(Vector2 start, Vector2 end, Color color, Color borderColor, float thickness, float borderThickness, int capSegments = 8, bool enableAA = true)
        => DrawLine(start, end, PaintStyle.Solid(color), PaintStyle.Solid(borderColor), thickness, borderThickness, capSegments, enableAA);

    public void FillLine(Vector2 start, Vector2 end, PaintStyle paint, float thickness, int capSegments = 8, bool enableAA = true)
        => DrawLine(start, end, paint, default, thickness, 0f, capSegments, enableAA);

    public void FillLine(Vector2 start, Vector2 end, Color color, float thickness, int capSegments = 8, bool enableAA = true)
        => FillLine(start, end, PaintStyle.Solid(color), thickness, capSegments, enableAA);

    public void BorderLine(Vector2 start, Vector2 end, PaintStyle borderPaint, float thickness, float borderThickness, int capSegments = 8, bool enableAA = true)
        => DrawLine(start, end, default, borderPaint, thickness, borderThickness, capSegments, enableAA);

    public void BorderLine(Vector2 start, Vector2 end, Color borderColor, float thickness, float borderThickness, int capSegments = 8, bool enableAA = true)
        => BorderLine(start, end, PaintStyle.Solid(borderColor), thickness, borderThickness, capSegments, enableAA);

    public void DrawArc(Vector2 center, PaintStyle fillPaint, PaintStyle borderPaint, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 48, bool enableAA = true) {
        if (outerRadius < innerRadius) {
            (innerRadius, outerRadius) = (outerRadius, innerRadius);
        }

        float thickness = outerRadius - innerRadius;
        if (thickness <= 0 || segments < 3) return;

        float midRadius = (innerRadius + outerRadius) * 0.5f;
        float halfThick = thickness * 0.5f;

        float borderThick = Math.Min(borderThickness, halfThick);
        float fillHalfThick = Math.Max(0, halfThick - borderThick);

        bool hasBorder = borderThick > 0 && !borderPaint.IsTrspt();
        bool hasFill = fillHalfThick > 0 && !fillPaint.IsTrspt();

        if (!(borderThick > 0) && !hasFill) return;

        float totalRadius = innerRadius + outerRadius;
        fillPaint = transformPaint(fillPaint, center, -Vector2.One * totalRadius, 0f);
        borderPaint = transformPaint(borderPaint, center, -Vector2.One * totalRadius, 0f);

        Vector2 startCenter = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * midRadius;
        Vector2 endCenter = center + new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle)) * midRadius;

        int capSegments = Math.Max(3, segments / 4);

        bool fpOp = fillPaint.IsOpaque();
        if (hasBorder ) {
            if (enableAA) {
                addCircleFringe(center, midRadius + halfThick, startAngle, endAngle, borderPaint, segments, true);
                addCircleFringe(center, midRadius - halfThick, startAngle, endAngle, borderPaint, segments, false);
                addCircleFringe(startCenter, halfThick, startAngle + float.Pi, startAngle + float.Tau, borderPaint, capSegments, true);
                addCircleFringe(endCenter, halfThick, endAngle, endAngle + float.Pi, borderPaint, capSegments, true);
                if (!fpOp && borderPaint.IsOpaque() || !hasFill) {
                    addCircleFringe(center, midRadius - fillHalfThick, startAngle, endAngle, borderPaint, segments, true);
                    addCircleFringe(center, midRadius + fillHalfThick, startAngle, endAngle, borderPaint, segments, false);
                    addCircleFringe(startCenter, fillHalfThick, startAngle + float.Pi, startAngle + float.Tau, borderPaint, capSegments, false);
                    addCircleFringe(endCenter, fillHalfThick, endAngle, endAngle + float.Pi, borderPaint, capSegments, false);
                }
            }
            addRingSegment(center, midRadius + fillHalfThick, midRadius + halfThick, startAngle, endAngle, borderPaint, segments);
            addRingSegment(center, midRadius - halfThick, midRadius - fillHalfThick, startAngle, endAngle, borderPaint, segments);
            addRingSegment(startCenter, fillHalfThick, halfThick, startAngle + float.Pi, startAngle + float.Tau, borderPaint, capSegments);
            addRingSegment(endCenter, fillHalfThick, halfThick, endAngle, endAngle + float.Pi, borderPaint, capSegments);
        }

        if (hasFill) {
            if (!hasBorder || fpOp) {
                addCircleFringe(center, midRadius + fillHalfThick, startAngle, endAngle, fillPaint, segments, true);
                addCircleFringe(center, midRadius - fillHalfThick, startAngle, endAngle, fillPaint, segments, false);
                addCircleFringe(startCenter, fillHalfThick, startAngle + float.Pi, startAngle + float.Tau, fillPaint, capSegments, true);
                addCircleFringe(endCenter, fillHalfThick, endAngle, endAngle + float.Pi, fillPaint, capSegments, true);
            }
            addRingSegment(center, midRadius - fillHalfThick, midRadius + fillHalfThick, startAngle, endAngle, fillPaint, segments);
            addRingSegment(startCenter, 0, fillHalfThick, startAngle + float.Pi, startAngle + float.Tau, fillPaint, capSegments);
            addRingSegment(endCenter, 0, fillHalfThick, endAngle, endAngle + float.Pi, fillPaint, capSegments);
        }

        
    }

    public void DrawArc(Vector2 center, Color fillColor, Color borderColor, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 48, bool enableAA = true)
        => DrawArc(center, PaintStyle.Solid(fillColor), PaintStyle.Solid(borderColor), innerRadius, outerRadius, startAngle, endAngle, borderThickness, segments, enableAA);

    public void FillArc(Vector2 center, PaintStyle fillPaint, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments = 48, bool enableAA = true)
        => DrawArc(center, fillPaint, default, innerRadius, outerRadius, startAngle, endAngle, 0f, segments, enableAA);

    public void FillArc(Vector2 center, Color fillColor, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments = 48, bool enableAA = true)
        => FillArc(center, PaintStyle.Solid(fillColor), innerRadius, outerRadius, startAngle, endAngle, segments, enableAA);

    public void BorderArc(Vector2 center, PaintStyle borderPaint, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 48, bool enableAA = true)
        => DrawArc(center, default, borderPaint, innerRadius, outerRadius, startAngle, endAngle, borderThickness, segments, enableAA);

    public void BorderArc(Vector2 center, Color borderColor, float innerRadius, float outerRadius, float startAngle, float endAngle, float borderThickness, int segments = 48, bool enableAA = true)
        => BorderArc(center, PaintStyle.Solid(borderColor), innerRadius, outerRadius, startAngle, endAngle, borderThickness, segments, enableAA);

    public void DrawCircle(Vector2 center, PaintStyle fillPaint, PaintStyle borderPaint, float radius, float borderThickness, int segments = 48, bool enableAA = true) {
        float innerRadius = Math.Max(0, radius - borderThickness);

        bool hasBorder = borderThickness > 0 && !borderPaint.IsTrspt();
        bool hasFill = innerRadius > 0 && !fillPaint.IsTrspt();

        if (innerRadius > 0) {
            fillPaint = transformPaint(fillPaint, center, -Vector2.One * radius, 0f);
            addRingSegment(center, 0, innerRadius, 0, float.Tau, fillPaint, segments);
        }

        if (hasBorder) {
            borderPaint = transformPaint(borderPaint, center, -Vector2.One * radius, 0f);
            addRingSegment(center, innerRadius, radius, 0, float.Tau, borderPaint, segments);
        }

        if (!enableAA) return;

        if (hasBorder) {
            addCircleFringe(center, radius, 0, float.Tau, borderPaint, segments, true);
            bool bpOp = borderPaint.IsOpaque();
            if (bpOp || !hasFill) {
                addCircleFringe(center, innerRadius, 0, float.Tau, borderPaint, segments, false);
            }
            if (!bpOp && fillPaint.IsOpaque()) {
                addCircleFringe(center, innerRadius, 0, float.Tau, fillPaint, segments, true);
            }
        }
        else if (hasFill) {
            addCircleFringe(center, innerRadius, 0, float.Tau, fillPaint, segments, true);
        }
    }

    public void DrawCircle(Vector2 center, Color fillColor, Color borderColor, float radius, float borderThickness, int segments = 48, bool enableAA = true)
        => DrawCircle(center, PaintStyle.Solid(fillColor), PaintStyle.Solid(borderColor), radius, borderThickness, segments, enableAA);

    public void FillCircle(Vector2 center, PaintStyle fillPaint, float radius, int segments = 48, bool enableAA = true)
        => DrawCircle(center, fillPaint, default, radius, 0f, segments, enableAA);

    public void FillCircle(Vector2 center, Color fillColor, float radius, int segments = 48, bool enableAA = true)
        => FillCircle(center, PaintStyle.Solid(fillColor), radius, segments, enableAA);

    public void BorderCircle(Vector2 center, PaintStyle borderPaint, float radius, float borderThickness, int segments = 48, bool enableAA = true)
        => DrawCircle(center, default, borderPaint, radius, borderThickness, segments, enableAA);

    public void BorderCircle(Vector2 center, Color borderColor, float radius, float borderThickness, int segments = 48, bool enableAA = true)
        => BorderCircle(center, PaintStyle.Solid(borderColor), radius, borderThickness, segments, enableAA);

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

    public void DrawTexture(Texture2D texture, Vector2 position, Vector2? size = null, Rectangle? sourceRect = null, Color? tint = null, float rotation = 0f, Vector2 origin = default, SpriteEffects effects = SpriteEffects.None, float rounding = 0f, int cornerSegments = 12) {
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
        Span<float> startAngles = [0, MathHelper.PiOver2, float.Pi, float.Pi * 1.5f];
        float step = MathHelper.PiOver2 / cornerSegments;

        float rotSin = 0, rotCos = 1;
        bool hasRotation = rotation != 0f;
        if (hasRotation) {
            rotSin = MathF.Sin(rotation);
            rotCos = MathF.Cos(rotation);
        }

        Vector2 transform(Vector2 p) {
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

        Vector2 getUV(Vector2 p) {
            float tx = (p.X - position.X) / actualSize.X;
            float ty = (p.Y - position.Y) / actualSize.Y;

            if (flipH) tx = 1f - tx;
            if (flipV) ty = 1f - ty;

            return new Vector2(
                float.Lerp(uvMin.X, uvMax.X, tx),
                float.Lerp(uvMin.Y, uvMax.Y, ty)
            );
        }

        int startIdx = _vertexCount;
        Vector2 centerPos = position + actualSize * 0.5f;

        _vertices[_vertexCount++] = new PrimitiveVertex(
            new Vector3(transform(centerPos), 0f),
            new Vector4(getUV(centerPos), texIndex, 0),
            actualTint, 3f, _currentClip.Rect, _currentClip.Params);

        int vertCounter = 0;
        for (int c = 0; c < 4; c++) {
            for (int i = 0; i <= cornerSegments; i++) {
                float angle = startAngles[c] + i * step;
                (float sin, float cos) = MathF.SinCos(angle);
                Vector2 pos = outCenters[c] + new Vector2(cos, sin) * outR;

                _vertices[_vertexCount++] = new PrimitiveVertex(
                    new Vector3(transform(pos), 0f),
                    new Vector4(getUV(pos), texIndex, 0),
                    actualTint, 3f, _currentClip.Rect, _currentClip.Params);

                _indices[_indexCount++] = (short)startIdx;
                _indices[_indexCount++] = (short)(startIdx + vertCounter + 1);
                _indices[_indexCount++] = (short)(startIdx + (vertCounter + 1) % perimeterVerts + 1);
                vertCounter++;
            }
        }
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Color color, float rounding = 0f, int cornerSegments = 12) {
        DrawTexture(texture, position, null, null, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Rectangle destinationRectangle, Color color, float rounding = 0f, int cornerSegments = 12) {
        DrawTexture(texture, new Vector2(destinationRectangle.X, destinationRectangle.Y), new Vector2(destinationRectangle.Width, destinationRectangle.Height), null, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rounding = 0f, int cornerSegments = 12) {
        Vector2 size = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        DrawTexture(texture, position, size, sourceRectangle, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rounding = 0f, int cornerSegments = 12) {
        DrawTexture(texture, new Vector2(destinationRectangle.X, destinationRectangle.Y), new Vector2(destinationRectangle.Width, destinationRectangle.Height), sourceRectangle, color, 0f, default, SpriteEffects.None, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float scale, SpriteEffects effects, float rounding = 0f, int cornerSegments = 12) {
        Vector2 srcSize = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        DrawTexture(texture, position, srcSize * scale, sourceRectangle, color, rotation, origin * scale, effects, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, SpriteEffects effects, float rounding = 0f, int cornerSegments = 12) {
        Vector2 srcSize = sourceRectangle.HasValue ? new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height) : new Vector2(texture.Width, texture.Height);
        DrawTexture(texture, position, srcSize * scale, sourceRectangle, color, rotation, origin * scale, effects, rounding, cornerSegments);
    }

    public void DrawTexture(Texture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, SpriteEffects effects, float rounding = 0f, int cornerSegments = 12) {
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
            float safePower = Math.Clamp(paint.EasingPower, 0f, 99.9F);
            float packedData = ((float)paint.Type * 1000f) + ((float)paint.Easing * 100F) + safePower;
            PaintParams = new Vector3(paint.OffsetsA, paint.OffsetsB, packedData);
        }

        public PrimitiveVertex(Vector3 pos, Vector4 gradientData, Color tint, float paintType, Vector4 clipRect, Vector2 clipParams) {
            Position = pos;
            ClipRect = clipRect;
            ClipParams = clipParams;
            ColorA = tint.ToVector4();
            ColorB = Vector4.Zero;
            GradientData = gradientData;
            PaintParams = new Vector3(0F, 0F, paintType * 1000F);
        }

        readonly VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }
}