using DioUI.RectangleFNS;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Runtime.CompilerServices;

namespace DioBatch; 

public class Test : Game {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private DioBatch _dioBatch = null!;
    private Texture2D _mg = null!;
    private Texture2D _gr = null!;
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
        _dioBatch = new DioBatch(GraphicsDevice, Content);
        _mg = Content.Load<Texture2D>("mg");
        _gr = Content.Load<Texture2D>("gr");
    }

    protected override void Update(GameTime gameTime) {
        if (Keyboard.GetState().IsKeyDown(Keys.F1))
            Exit();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        if (!IsActive) return;
        GraphicsDevice.Clear(new Color(0, 191, 255));

        var wave = float.Pow(float.Sin((float)gameTime.TotalGameTime.TotalSeconds * 0.75f * float.Pi), 2);
        var mpos = Mouse.GetState().Position.ToVector2();
        float time = (float)gameTime.TotalGameTime.TotalSeconds;

        _dioBatch.Begin();
        var bgps = PaintStyle.Linear(Vector2.Zero, Vector2.UnitY * 900, new Color(0, 191, 255), Color.Blue).SetEasing(PaintStyle.EasingType.EaseIn, 1.5f);
        _dioBatch.FillRectangle(Vector2.Zero, new(1600, 900), 0, bgps);


        var clipRect = new RectangleF(mpos.X - 200, mpos.Y - 200 - 100 * wave, 400, 400 + 200 * wave);
        var clipRot = time * 0.5f;

        var rng = new RngStruct(420);
        var nextF = rng.NextFloat;
        var csDir = Vector2.Rotate(Vector2.UnitX, float.Pi * 0.1f);
        float depth = 0.2f;
        for (int i = 0; i < 100; i++) {
            var pos = new Vector2(1600 * nextF(), 900 * nextF()) + csDir * time * 500 * depth;
            if (pos.X > 2000) pos.X = pos.X % 2000 - 300;
            if (pos.Y > 1200) pos.Y = pos.Y % 1200 - 300;
            var size = new Vector2(200 + 50 * nextF(), 50 + 50 * nextF()) * depth;
            var ps = PaintStyle.Linear(Vector2.Zero, Vector2.UnitY * size.Y, Color.White, Color.LightBlue).SetEasing(PaintStyle.EasingType.EaseIn, 1.5f);
            _dioBatch.FillRectangle(pos, size, 10, ps, float.Pi * 0.1f * nextF());
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
            _dioBatch.DrawTexture(i % 2 == 0 ? _mg : _gr, pos, size, radius: r);
            var Midbottom = pos + new Vector2(size.X / 2, size.Y);
            _dioBatch.DrawLine(Midbottom, Midbottom + Vector2.UnitY * 200 * depth, 5 * depth, Color.Black);
            depth += 0.8f / 20;
        }

        _dioBatch.PopClip();
        var sunCenter = new Vector2(800, 450);
        var sunR = 300 - 30 * wave;
        var sunG = PaintStyle.Radial(Vector2.Zero, Vector2.UnitX * sunR, Color.Yellow, Color.LightYellow).SetEasing(PaintStyle.EasingType.EaseIn, 4);
        _dioBatch.FillCircle(sunCenter, sunR, sunG, 48);
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
            _dioBatch.DrawLine(start, end, 50, ps);
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
                var ps = PaintStyle.Linear(-Vector2.UnitX * (85 + (j * 10)), Vector2.UnitX * (85 + (j * 10)), bc(Color.Green, 0.5f - j * 0.1f), bc(Color.Green, 1 - j * 0.1f));
                _dioBatch.FillCircle(pos, 85 + (j * 10), ps);
            }
        }

        if (wave > 0.5f)
            _dioBatch.BorderRectangle(clipRect.Position, clipRect.Size, 50, 2, Color.Black, clipRot, clipRect.Size / 2);

        _dioBatch.End(); // ONE SINGLE DRAW CALL

        base.Draw(gameTime);
    }
    private static Color bc(Color color, float amount) => new(color.R * amount / 255, color.G * amount / 255, color.B * amount / 255, color.A / 255);
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