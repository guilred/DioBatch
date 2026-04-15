using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using DioUI.RectangleFNS;

namespace DioBatch; 

public class Test : Game {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private DioBatch _dioBatch = null!;
    private Texture2D _aaa = null!;
    private Texture2D _ab = null!;
    private Texture2D _bgr = null!;
    private Texture2D _dw = null!;
    private Texture2D _h = null!;
    private Texture2D _ummm = null!;
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
        _aaa = Texture2D.FromFile(GraphicsDevice, "Content/aaaaaa.png");
        _ab = Texture2D.FromFile(GraphicsDevice, "Content/ab.png");
        _bgr = Texture2D.FromFile(GraphicsDevice, "Content/bgr.png");
        _ummm = Texture2D.FromFile(GraphicsDevice, "Content/ummm.png");
        _h = Texture2D.FromFile(GraphicsDevice, "Content/h.png");
        _dw = Texture2D.FromFile(GraphicsDevice, "Content/dw.png");
        _dioBatch = new DioBatch(GraphicsDevice, Content);
    }

    protected override void Update(GameTime gameTime) {
        if (Keyboard.GetState().IsKeyDown(Keys.F1))
            Exit();
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        if (!IsActive) return;
        GraphicsDevice.Clear(Color.CornflowerBlue);

        var wave = float.Pow(float.Sin((float)gameTime.TotalGameTime.TotalSeconds * 0.5f * float.Pi), 2);

        _dioBatch.Begin();
        //_dioBatch.DrawRectangle(Vector2.One * 100, Vector2.One * 200, Color.Red, new(Vector2.One * 110, Vector2.One * 100), s, float.Pi / 6);
        //_dioBatch.DrawCircle(new(300), 200, Color.Green, 20, Color.Blue, 64);
        //_dioBatch.DrawArc(new(400), 100, 50, 0, float.Pi, Color.Green, Color.Blue, s);
        //_dioBatch.DrawRectangle(new(400), new(300), 50, Color.Green, s, Color.Blue);
        var ps1 = PaintStyle.Linear(new(0), new(500), Color.Blue, Color.Magenta, true);
        var ps2 = PaintStyle.Linear(new(0), new(500), Color.Magenta, Color.Blue, true);
        for (int j = 0; j < 50; j++) {
            _dioBatch.DrawRectangle(new(100), new(500), 40, ps1, 20, ps2, float.Pi / 4, new(250), 8);
        }
        int i = 0;
        int s = 100;
        _dioBatch.DrawTexture(_aaa , new(100 + i, 100 + i), new(500), radius: 100);
        i += s;
        _dioBatch.DrawTexture(_ab , new(100 + i, 100 + i), new(500));
        i += s;
        _dioBatch.DrawTexture(_bgr , new(100 + i, 100 + i), new(500));
        i += s;
        _dioBatch.DrawTexture(_dw , new(100 + i, 100 + i), new(500));
        i += s;
        _dioBatch.DrawTexture(_h , new(100 + i, 100 + i), new(500));
        i += s;
        _dioBatch.DrawTexture(_ummm, new(100 + i, 100 + i), new(500));
        _dioBatch.End();

        base.Draw(gameTime);
    }
}
