#nullable disable
using DioUI.RectangleFNS;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Runtime.CompilerServices;

namespace DioUI;

public class Test : Game {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private DioBatch _dioBatch;
    private Texture2D _mg;
    private Texture2D _gr;
    private RenderTarget2D _mid;
    public Test() {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize() {
        (_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight) = (1600, 900);
        _graphics.SynchronizeWithVerticalRetrace = false;
        IsFixedTimeStep = false;
        _graphics.ApplyChanges();

        base.Initialize();
    }

    protected override void LoadContent() {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _dioBatch = new DioBatch(GraphicsDevice);
        _mg = Content.Load<Texture2D>("mg");
        _gr = Content.Load<Texture2D>("gr");
        _mid = new RenderTarget2D(GraphicsDevice, 1600, 900);
    }
    private bool _zoomed;
    protected override void Update(GameTime gameTime) {
        if (Keyboard.GetState().IsKeyDown(Keys.F1))
            Exit();
        if (!_zoomed && Keyboard.GetState().IsKeyDown(Keys.Q)) {
            _zoomed = true;
        }
        else if (_zoomed && Keyboard.GetState().IsKeyDown(Keys.A)) {
            _zoomed = false;
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        if (!IsActive) return;

        int SCENE = 0;
        float time = (float)gameTime.TotalGameTime.TotalSeconds;
        var wave = float.Pow(float.Sin(time * 0.25f * float.Pi), 2);
        var mpos = Mouse.GetState().Position.ToVector2();

        GraphicsDevice.SetRenderTarget(_mid);
        if (SCENE == 0) {
            GraphicsDevice.Clear(new Color(0, 191, 255));

            _dioBatch.Begin();
            var bgps = PaintStyle.Linear(Vector2.Zero, Vector2.UnitY * 900, new Color(0, 191, 255), Color.Blue).SetEasing(PaintStyle.EasingType.EaseIn, 1.5f);
            _dioBatch.FillRectangle(Vector2.Zero, new(1600, 900), bgps);

            var clipRect = new RectangleF(mpos.X - 200, mpos.Y - 200 - 100 * wave, 400, 400 + 200 * wave);
            var clipRot = time * 0.5f;

            var rng = new RngStruct(420);
            var nextF = rng.NextFloat;
            float depth = 0.2f;
            for (int i = 0; i < 100; i++) {
                var dir = float.Pi * 0.1f * nextF();
                var csDir = Vector2.Rotate(Vector2.UnitX, dir);
                var pos = new Vector2(1600 * nextF(), 900 * nextF()) + csDir * time * 500 * depth;
                if (pos.X > 2000) pos.X = pos.X % 2000 - 300;
                if (pos.Y > 1200) pos.Y = pos.Y % 1200 - 300;
                var size = new Vector2(200 + 50 * nextF(), 50 + 50 * nextF()) * depth;
                var ps = PaintStyle.Linear(Vector2.Zero, Vector2.UnitY * size.Y, Color.White, Color.LightBlue).SetEasing(PaintStyle.EasingType.EaseIn, 1.5f);
                _dioBatch.FillRectangle(pos, size, ps, 10, dir);
                depth += 0.8f / 100;
                if (i == 49) {
                    _dioBatch.PushClip(clipRect, 50, clipRot);
                }
            }
            depth = 0.2f;
            for (int i = 0; i < 20; i++) {
                float r = i % 2 == 0 ? 0 : 50 * nextF();
                var pos = new Vector2(1600 * nextF(), 900 * nextF()) - Vector2.UnitY * time * 500 * depth;
                if (pos.Y < 1200) pos.Y = 1200 + pos.Y % 1200 - 300;
                var size = new Vector2(200, 200) * depth;
                _dioBatch.DrawTexture(i % 2 == 0 ? _mg : _gr, pos, size, rounding: r);
                var Midbottom = pos + new Vector2(size.X / 2, size.Y);
                _dioBatch.FillLine(Midbottom, Midbottom + Vector2.UnitY * 200 * depth, Color.Black, 5 * depth);
                depth += 0.8f / 20;
            }

            _dioBatch.PopClip();
            var sunCenter = new Vector2(800, 450);
            var sunR = 300 - 30 * wave;
            var sunG = PaintStyle.Radial(Vector2.One * sunR, Vector2.UnitX * sunR, Color.Yellow, Color.LightYellow).SetEasing(PaintStyle.EasingType.EaseIn, 4);
            _dioBatch.FillCircle(sunCenter, sunG, sunR, 64);
            for (int i = 0; i < 20; i++) {
                if (i % 2 == 0) {
                    _dioBatch.PushClip(clipRect, 50, clipRot);
                }
                else {
                    _dioBatch.PopClip();
                }
                float t = i / 20f;
                float angle = float.Tau * t + time * 0.5f;
                var start = sunCenter + Vector2.Rotate(Vector2.UnitX * (360 - 30 * wave), angle);
                var bladeLenght = 200 - 30 * float.Sin(float.Tau * t + time * 2);
                var end = sunCenter + Vector2.Rotate(Vector2.UnitX * (320 + bladeLenght), angle);
                var ps = PaintStyle.Linear(Vector2.Zero, Vector2.UnitX * bladeLenght, Color.Yellow, Color.LightYellow).SetEasing(PaintStyle.EasingType.EaseIn, 1.5f);
                _dioBatch.FillLine(start, end, ps, 50);
            }

            for (int j = 0; j < 6; j++) {
                if (j % 2 == 0) {
                    _dioBatch.PushClip(clipRect, 50, clipRot);
                }
                else {
                    _dioBatch.PopClip();
                }
                for (int i = 0; i < 10; i++) {
                    var iwave = float.Pow(float.Sin(time * 0.5f * float.Pi + i * 0.2f + (j * 0.2f - 0.3f)), 2);
                    var pos = new Vector2(160 * i + 80, 780 + (j * 35) - (80 - j * 10) * iwave);
                    var ps = PaintStyle.Linear(Vector2.Zero, Vector2.UnitX * (85 + (j * 10)) * 2, bc(Color.Green, 0.5f - j * 0.1f), bc(Color.Green, 1 - j * 0.1f));
                    _dioBatch.FillCircle(pos, ps, 85 + (j * 10));
                }
            }

            if (wave > 0.5f)
                _dioBatch.BorderRectangle(clipRect.Position, clipRect.Size, Color.Black, 2, 50, clipRot, clipRect.Size / 2);

            _dioBatch.End(); // ONE SINGLE DRAW CALL
        }
        else if (SCENE == 1) {
            performTests((int)((time * 0.25f) / 0.25f) % (5 + 1));
        }

        GraphicsDevice.SetRenderTarget(null);

        var zoomPos = mpos; // Vector2.One * 80 + Vector2.UnitX * 1200;
        var zoomMat = _zoomed ? Matrix.CreateTranslation(-zoomPos.X, -zoomPos.Y, 0) * Matrix.CreateScale(4) * Matrix.CreateTranslation(zoomPos.X, zoomPos.Y, 0): Matrix.Identity;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: zoomMat);
        _spriteBatch.Draw(_mid, _mid.Bounds, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
    private static Color bc(Color color, float amount) => new(color.R * amount / 255, color.G * amount / 255, color.B * amount / 255, color.A / 255);
    private void performTests(int state) {
        GraphicsDevice.Clear(Color.White);

        float cx = 100, cy = 100, padding = 100;
        (float m1, float m2) = state switch { 0 => (0.5f, 0.5f), 1 => (0, 0.5f), 2 => (0.5f, 0), 3 => (1, 1), 4 => (1, 0.5f), 5 => (0.5f, 1), _ => (0, 0) };
        var pos = new Vector2(cx, cy);
        var size = new Vector2(200, 150);
        void step() {
            cx += 200 + padding;
            if (cx + 200 + padding > 1600) {
                cx = padding;
                cy += 150 + 100;
            }
            (pos, size) = (new(cx, cy), new(200, 150));
        }

        _dioBatch.Begin();

        var ps = PaintStyle.Linear(Vector2.Zero, size, Color.Blue, Color.Magenta) * m1;
        var ps2 = PaintStyle.Linear(Vector2.Zero, size, Color.Magenta, Color.Blue) * m2;
        _dioBatch.DrawRectangle(pos, size, ps, ps2, 20, 40, 0.05f, size / 2);
        step();

        ps = PaintStyle.Linear(Vector2.Zero, Vector2.One * size.X, Color.Blue, Color.Magenta) * m1;
        ps2 = PaintStyle.Linear(Vector2.Zero, Vector2.One * size.X, Color.Magenta, Color.Blue) * m2;
        _dioBatch.DrawCircle(pos + size / 2, ps, ps2, size.X / 2, 20);
        step();

        ps = PaintStyle.Linear(Vector2.Zero, Vector2.One * size.X, Color.Blue, Color.Magenta) * m1;
        ps2 = PaintStyle.Linear(Vector2.Zero, Vector2.One * size.X, Color.Magenta, Color.Blue) * m2;
        _dioBatch.DrawArc(pos + size / 2, ps, ps2, size.X / 2 - 80, 80, 0, float.Pi * 1.5f, 20);
        step();
        
        ps = PaintStyle.Linear(Vector2.Zero, size, Color.Blue, Color.Magenta) * m1;
        ps2 = PaintStyle.Linear(Vector2.Zero, size, Color.Magenta, Color.Blue) * m2;
        _dioBatch.DrawLine(pos, pos + size, ps, ps2, 50, 5);
        step();

        _dioBatch.DrawTexture(_gr, pos, size, rounding: 20);

        _dioBatch.End();
    }
}


public struct RngStruct {
    private ulong _s0;
    private ulong _s1;
    public RngStruct(ulong seed) {
        _s0 = seed;
        _s1 = seed + 0x9E3779B97F4A7C15;
        for (int i = 0; i < 4; i++) NextULong();
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong NextULong() {
        ulong x = _s0;
        ulong y = _s1;
        _s0 = y;
        x ^= x << 23;
        _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
        return _s1 + y;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float NextFloat() => (NextULong() >> 40) * (1.0f / 16777216.0f);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Range(float min, float max) => min + (max - min) * NextFloat();
    public int Range(int min, int max) {
        if (min >= max) return min;
        uint range = (uint)(max - min);
        return (int)(min + (int)(NextULong() % range));
    }
    public bool NextBool(float probability = 0.5f) => NextFloat() < probability;
    public void Fill(Span<byte> buffer) {
        for (int i = 0; i < buffer.Length; i++) {
            buffer[i] = (byte)(NextULong() & 0xFF);
        }
    }
}