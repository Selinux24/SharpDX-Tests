﻿using Engine;
using Engine.Audio;
using Engine.Common;
using Engine.Content;
using Engine.Tween;
using Engine.UI;
using Engine.UI.Tween;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SceneTest.SceneTanksGame
{
    /// <summary>
    /// Tanks game scene
    /// </summary>
    class SceneTanksGame : Scene
    {
        const int layerLoadingUI = 100;
        const int layerUI = 50;
        const int layerModels = 10;

        const string fontFilename = "SceneTanksGame/LeagueSpartan-Bold.otf";

        private bool gameReady = false;

        private UITextArea loadingText;
        private UIProgressBar loadingBar;
        private float progressValue = 0;
        private UIPanel fadePanel;

        private UITextArea gameMessage;
        private UITextArea gameKeyHelp;

        private UIPanel dialog;
        private UIButton dialogClose;
        private UIButton dialogAccept;
        private UITextArea dialogText;
        private bool dialogActive = false;
        private EventHandler lastOnCloseHandler;
        private EventHandler lastOnAcceptHandler;

        private UITextArea player1Name;
        private UITextArea player1Points;
        private UIProgressBar player1Life;
        private PlayerStatus player1Status;

        private UITextArea player2Name;
        private UITextArea player2Points;
        private UIProgressBar player2Life;
        private PlayerStatus player2Status;

        private UITextArea turnText;
        private int currentTurn = 1;
        private Sprite gameIcon;
        private int currentPlayer = 0;
        private Sprite playerTurnMarker;

        private UIPanel keyHelp;
        private Sprite keyRotate;
        private Sprite keyMove;
        private Sprite KeyPitch;
        private UITextArea keyRotateLeftText;
        private UITextArea keyRotateRightText;
        private UITextArea keyMoveForwardText;
        private UITextArea keyMoveBackwardText;
        private UITextArea keyPitchUpText;
        private UITextArea keyPitchDownText;

        private UIProgressBar pbFire;
        private UITextArea fireKeyText;

        private Sprite miniMapBackground;
        private Sprite miniMapTank1;
        private Sprite miniMapTank2;
        private readonly float maxWindVelocity = 10;
        private float currentWindVelocity = 1;
        private Vector2 windDirection = Vector2.Normalize(Vector2.One);
        private UIProgressBar windVelocity;
        private Sprite windDirectionArrow;

        private Model landScape;
        private Scenery terrain;
        private float terrainTop;
        private readonly float terrainHeight = 100;
        private readonly float terrainSize = 1024;
        private readonly int mapSize = 256;
        private ModelInstanced tanks;
        private float tankHeight = 0;
        private Model projectile;

        private Sprite[] trajectoryMarkerPool;

        private readonly Dictionary<string, ParticleSystemDescription> particleDescriptions = new Dictionary<string, ParticleSystemDescription>();
        private ParticleManager particleManager = null;

        private bool shooting = false;
        private bool gameEnding = false;
        private bool freeCamera = false;

        private ModelInstance Shooter { get { return tanks[currentPlayer]; } }
        private ModelInstance Target { get { return tanks[(currentPlayer + 1) % 2]; } }
        private PlayerStatus ShooterStatus { get { return currentPlayer == 0 ? player1Status : player2Status; } }
        private PlayerStatus TargetStatus { get { return currentPlayer == 0 ? player2Status : player1Status; } }
        private ParabolicShot shot;

        private string tankMoveEffect;
        private IAudioEffect tankMoveEffectInstance;
        private string tankDestroyedEffect;
        private string tankShootingEffect;
        private string[] impactEffects;
        private string[] damageEffects;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="game">Game</param>
        public SceneTanksGame(Game game) : base(game)
        {
            InitializePlayers();
        }

        public override void OnReportProgress(float value)
        {
            progressValue = Math.Max(progressValue, value);

            if (loadingBar != null)
            {
                loadingBar.ProgressValue = progressValue;
                loadingBar.Caption.Text = $"{(int)(progressValue * 100f)}%";
            }
        }

        public override Task Initialize()
        {
            GameEnvironment.Background = Color.CornflowerBlue;

            this.Game.VisibleMouse = false;
            this.Game.LockMouse = false;

            this.Camera.NearPlaneDistance = 0.1f;
            this.Camera.FarPlaneDistance = 2000;

            return LoadLoadingUI();
        }

        private async Task LoadLoadingUI()
        {
            await this.LoadResourcesAsync(
                InitializeLoadingUI(),
                async (res) =>
                {
                    if (!res.Completed)
                    {
                        res.ThrowExceptions();
                    }

                    fadePanel.BaseColor = Color.Black;
                    fadePanel.Visible = true;

                    loadingText.Text = "Please wait...";
                    loadingText.Visible = true;
                    loadingText.TweenAlphaBounce(1, 0, 1000, ScaleFuncs.CubicEaseInOut);

                    loadingBar.ProgressValue = 0;
                    loadingBar.Visible = true;

                    await this.LoadUI();
                });
        }
        private async Task InitializeLoadingUI()
        {
            fadePanel = await this.AddComponentUIPanel(UIPanelDescription.Screen(this, Color4.Black * 0.3333f), layerLoadingUI);
            fadePanel.Visible = false;

            loadingText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 40, true), layerLoadingUI + 1);
            loadingText.TextColor = Color.Yellow;
            loadingText.TextShadowColor = Color.Orange;
            loadingText.CenterHorizontally = CenterTargets.Screen;
            loadingText.Top = this.Game.Form.RenderCenter.Y - 75f;
            loadingText.Width = this.Game.Form.RenderWidth * 0.8f;
            loadingText.HorizontalAlign = HorizontalTextAlign.Center;
            loadingText.VerticalAlign = VerticalTextAlign.Middle;
            loadingText.AdjustAreaWithText = false;
            loadingText.Visible = false;

            loadingBar = await this.AddComponentUIProgressBar(UIProgressBarDescription.DefaultFromFile(fontFilename, 20, true), layerLoadingUI + 1);
            loadingBar.CenterHorizontally = CenterTargets.Screen;
            loadingBar.CenterVertically = CenterTargets.Screen;
            loadingBar.Width = this.Game.Form.RenderWidth * 0.8f;
            loadingBar.Height = 35;
            loadingBar.ProgressColor = Color.Yellow;
            loadingBar.BaseColor = Color.CornflowerBlue;
            loadingBar.Caption.TextColor = Color.Black;
            loadingBar.Caption.Text = "0%";
            loadingBar.Visible = false;
        }

        private async Task LoadUI()
        {
            List<Task> taskList = new List<Task>();
            taskList.AddRange(InitializeUI());
            taskList.AddRange(InitializeModels());

            await this.LoadResourcesAsync(
                taskList.ToArray(),
                (res) =>
                {
                    if (!res.Completed)
                    {
                        res.ThrowExceptions();
                    }

                    PrepareUI();
                    PrepareModels();
                    UpdateCamera(true);

                    AudioManager.MasterVolume = 1f;
                    AudioManager.Start();

                    Task.Run(async () =>
                    {
                        loadingText.ClearTween();
                        loadingText.Hide(1000);
                        loadingBar.ClearTween();
                        loadingBar.Hide(500);

                        await Task.Delay(1500);

                        await ShowMessage("Ready!", 2000);

                        fadePanel.ClearTween();
                        fadePanel.Hide(2000);

                        gameReady = true;

                        UpdateGameControls(true);

                        PaintShot(true);
                    });
                });
        }

        private Task[] InitializeUI()
        {
            return new[]
            {
                InitializeUIGameMessages(),
                InitializeUIModalDialog(),
                InitializeUIPlayers(),
                InitializeUITurn(),
                InitializeUIKeyPanel(),
                InitializeUIFire(),
                InitializeUIMinimap(),
                InitializeUIShotPath(),
            };
        }
        private async Task InitializeUIGameMessages()
        {
            gameMessage = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 120, false), layerLoadingUI + 1);
            gameMessage.CenterHorizontally = CenterTargets.Screen;
            gameMessage.CenterVertically = CenterTargets.Screen;
            gameMessage.TextColor = Color.Yellow;
            gameMessage.TextShadowColor = Color.Yellow * 0.5f;
            gameMessage.Visible = false;

            gameKeyHelp = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 25, true), layerLoadingUI + 1);
            gameKeyHelp.TextColor = Color.Yellow;
            gameKeyHelp.Text = "Press space to exit";
            gameKeyHelp.CenterHorizontally = CenterTargets.Screen;
            gameKeyHelp.Top = this.Game.Form.RenderHeight - 60;
            gameKeyHelp.Width = 500;
            gameKeyHelp.Height = 40;
            gameKeyHelp.HorizontalAlign = HorizontalTextAlign.Center;
            gameKeyHelp.VerticalAlign = VerticalTextAlign.Middle;
            gameKeyHelp.AdjustAreaWithText = false;
            gameKeyHelp.Visible = false;
        }
        private async Task InitializeUIModalDialog()
        {
            float width = this.Game.Form.RenderWidth / 2f;
            float height = width * 0.6666f;

            var descPan = new UIPanelDescription
            {
                Name = "Modal Dialog",

                Width = width,
                Height = height,
                CenterVertically = CenterTargets.Screen,
                CenterHorizontally = CenterTargets.Screen,

                Background = new SpriteDescription()
                {
                    BaseColor = Color.DarkGreen,
                }
            };
            dialog = await this.AddComponentUIPanel(descPan, layerLoadingUI + 2);

            var font = TextDrawerDescription.FromFile(fontFilename, 20);
            font.LineAdjust = true;
            font.HorizontalAlign = HorizontalTextAlign.Center;
            font.VerticalAlign = VerticalTextAlign.Middle;

            var descButton = UIButtonDescription.DefaultTwoStateButton(Color.DarkGray * 0.6666f, Color.DarkGray * 0.7777f, UITextAreaDescription.Default(font));

            float butWidth = 150;
            float butHeight = 55;
            float butMargin = 15;

            dialogAccept = new UIButton(this, descButton)
            {
                Width = butWidth,
                Height = butHeight,
                Top = dialog.Height - butMargin - butHeight,
                Left = (dialog.Width * 0.5f) - (butWidth * 0.5f) - (butWidth * 0.6666f)
            };
            dialogAccept.Caption.Text = "Ok";

            dialogClose = new UIButton(this, descButton)
            {
                Width = butWidth,
                Height = butHeight,
                Top = dialog.Height - butMargin - butHeight,
                Left = (dialog.Width * 0.5f) - (butWidth * 0.5f) + (butWidth * 0.6666f)
            };
            dialogClose.Caption.Text = "Cancel";

            var descText = UITextAreaDescription.FromFile(fontFilename, 28);
            descText.Padding = new Padding
            {
                Left = width * 0.1f,
                Right = width * 0.1f,
                Top = height * 0.1f,
                Bottom = butHeight + (butMargin * 2f),
            };
            descText.Font.HorizontalAlign = HorizontalTextAlign.Center;
            descText.Font.VerticalAlign = VerticalTextAlign.Middle;

            dialogText = new UITextArea(this, descText);

            dialog.AddChild(dialogText);
            dialog.AddChild(dialogClose, false);
            dialog.AddChild(dialogAccept, false);
            dialog.Visible = false;
        }
        private async Task InitializeUIPlayers()
        {
            float playerWidth = 300;

            player1Name = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 20, true), layerUI);
            player1Name.TextColor = player1Status.Color;
            player1Name.TextShadowColor = player1Status.Color * 0.5f;
            player1Name.AdjustAreaWithText = false;
            player1Name.HorizontalAlign = HorizontalTextAlign.Left;
            player1Name.Width = playerWidth;
            player1Name.Top = 10;
            player1Name.Left = 10;
            player1Name.Visible = false;

            player1Points = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 25, true), layerUI);
            player1Points.TextColor = player1Status.Color;
            player1Points.TextShadowColor = player1Status.Color * 0.5f;
            player1Points.AdjustAreaWithText = false;
            player1Points.HorizontalAlign = HorizontalTextAlign.Center;
            player1Points.Width = playerWidth;
            player1Points.Top = 60;
            player1Points.Left = 10;
            player1Points.Visible = false;

            player1Life = await this.AddComponentUIProgressBar(UIProgressBarDescription.DefaultFromFile(fontFilename, 10, true), layerUI);
            player1Life.Width = playerWidth;
            player1Life.Height = 30;
            player1Life.Top = 100;
            player1Life.Left = 10;
            player1Life.ProgressColor = player1Status.Color;
            player1Life.BaseColor = Color.Black;
            player1Life.Caption.TextColor = Color.White;
            player1Life.Caption.Text = "0%";
            player1Life.Visible = false;

            player2Name = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 20, true), layerUI);
            player2Name.TextColor = player2Status.Color;
            player2Name.TextShadowColor = player2Status.Color * 0.5f;
            player2Name.AdjustAreaWithText = false;
            player2Name.HorizontalAlign = HorizontalTextAlign.Right;
            player2Name.Width = playerWidth;
            player2Name.Top = 10;
            player2Name.Left = this.Game.Form.RenderWidth - 10 - player2Name.Width;
            player2Name.Visible = false;

            player2Points = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 25, true), layerUI);
            player2Points.TextColor = player2Status.Color;
            player2Points.TextShadowColor = player2Status.Color * 0.5f;
            player2Points.AdjustAreaWithText = false;
            player2Points.HorizontalAlign = HorizontalTextAlign.Center;
            player2Points.Width = playerWidth;
            player2Points.Top = 60;
            player2Points.Left = this.Game.Form.RenderWidth - 10 - player2Points.Width;
            player2Points.Visible = false;

            player2Life = await this.AddComponentUIProgressBar(UIProgressBarDescription.DefaultFromFile(fontFilename, 10, true), layerUI);
            player2Life.Width = playerWidth;
            player2Life.Height = 30;
            player2Life.Top = 100;
            player2Life.Left = this.Game.Form.RenderWidth - 10 - player2Life.Width;
            player2Life.ProgressColor = player2Status.Color;
            player2Life.BaseColor = Color.Black;
            player2Life.Caption.TextColor = Color.White;
            player2Life.Caption.Text = "0%";
            player2Life.Visible = false;
        }
        private async Task InitializeUITurn()
        {
            turnText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 40, true), layerUI);
            turnText.TextColor = Color.Yellow;
            turnText.TextShadowColor = Color.Yellow * 0.5f;
            turnText.HorizontalAlign = HorizontalTextAlign.Center;
            turnText.Width = 300;
            turnText.CenterHorizontally = CenterTargets.Screen;
            turnText.AdjustAreaWithText = false;
            turnText.Visible = false;

            gameIcon = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/GameIcon.png"), SceneObjectUsages.UI, layerUI);
            gameIcon.BaseColor = Color.Yellow;
            gameIcon.Width = 92;
            gameIcon.Height = 82;
            gameIcon.Top = 55;
            gameIcon.CenterHorizontally = CenterTargets.Screen;
            gameIcon.Visible = false;
            gameIcon.TweenRotateBounce(-0.1f, 0.1f, 500, ScaleFuncs.CubicEaseInOut);

            playerTurnMarker = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Arrow.png"), SceneObjectUsages.UI, layerUI);
            playerTurnMarker.BaseColor = Color.Turquoise;
            playerTurnMarker.Width = 112;
            playerTurnMarker.Height = 75;
            playerTurnMarker.Top = 35;
            playerTurnMarker.Left = this.Game.Form.RenderCenter.X - 112 - 120;
            playerTurnMarker.Visible = false;
            playerTurnMarker.TweenScaleBounce(1, 1.2f, 500, ScaleFuncs.CubicEaseInOut);
        }
        private async Task InitializeUIKeyPanel()
        {
            float top = this.Game.Form.RenderHeight - 150;

            keyHelp = await this.AddComponentUIPanel(UIPanelDescription.Default(Color4.Black * 0.3333f), layerUI);
            keyHelp.Left = 0;
            keyHelp.Top = top;
            keyHelp.Height = 150;
            keyHelp.Width = 250;
            keyHelp.Visible = false;

            keyRotate = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Turn.png"), SceneObjectUsages.UI, layerUI + 1);
            keyRotate.Left = 0;
            keyRotate.Top = top + 25;
            keyRotate.Width = 372 * 0.25f;
            keyRotate.Height = 365 * 0.25f;
            keyRotate.BaseColor = Color.Turquoise;
            keyRotate.Visible = false;

            keyMove = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Move.png"), SceneObjectUsages.UI, layerUI + 1);
            keyMove.Left = keyRotate.Width;
            keyMove.Top = top + 25;
            keyMove.Width = 232 * 0.25f;
            keyMove.Height = 365 * 0.25f;
            keyMove.BaseColor = Color.Turquoise;
            keyMove.Visible = false;

            KeyPitch = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Pitch.png"), SceneObjectUsages.UI, layerUI + 1);
            KeyPitch.Left = keyRotate.Width + keyMove.Width;
            KeyPitch.Top = top + 25;
            KeyPitch.Width = 322 * 0.25f;
            KeyPitch.Height = 365 * 0.25f;
            KeyPitch.BaseColor = Color.Turquoise;
            KeyPitch.Visible = false;

            keyRotateLeftText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 15, true), layerUI + 2);
            keyRotateLeftText.TextColor = Color.Yellow;
            keyRotateLeftText.Text = "A";
            keyRotateLeftText.Top = top + 20;
            keyRotateLeftText.Left = 10;
            keyRotateLeftText.Visible = false;

            keyRotateRightText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 15, true), layerUI + 2);
            keyRotateRightText.TextColor = Color.Yellow;
            keyRotateRightText.Text = "D";
            keyRotateRightText.Top = top + 20;
            keyRotateRightText.Left = keyRotate.Width - 30;
            keyRotateRightText.Visible = false;

            keyMoveForwardText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 15, true), layerUI + 2);
            keyMoveForwardText.TextColor = Color.Yellow;
            keyMoveForwardText.Text = "W";
            keyMoveForwardText.Top = top + 20;
            keyMoveForwardText.Left = keyMove.AbsoluteCenter.X - 5;
            keyMoveForwardText.Visible = false;

            keyMoveBackwardText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 15, true), layerUI + 2);
            keyMoveBackwardText.TextColor = Color.Yellow;
            keyMoveBackwardText.Text = "S";
            keyMoveBackwardText.Top = top + keyMove.Height + 10;
            keyMoveBackwardText.Left = keyMove.AbsoluteCenter.X - 5;
            keyMoveBackwardText.Visible = false;

            keyPitchUpText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 15, true), layerUI + 2);
            keyPitchUpText.TextColor = Color.Yellow;
            keyPitchUpText.Text = "Q";
            keyPitchUpText.Top = top + 20;
            keyPitchUpText.Left = KeyPitch.AbsoluteCenter.X - 15;
            keyPitchUpText.Visible = false;

            keyPitchDownText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 15, true), layerUI + 2);
            keyPitchDownText.TextColor = Color.Yellow;
            keyPitchDownText.Text = "Z";
            keyPitchDownText.Top = top + KeyPitch.Height + 10;
            keyPitchDownText.Left = KeyPitch.AbsoluteCenter.X + 10;
            keyPitchDownText.Visible = false;
        }
        private async Task InitializeUIFire()
        {
            pbFire = await this.AddComponentUIProgressBar(UIProgressBarDescription.Default(), layerUI);
            pbFire.CenterHorizontally = CenterTargets.Screen;
            pbFire.Top = this.Game.Form.RenderHeight - 100;
            pbFire.Width = 500;
            pbFire.Height = 40;
            pbFire.ProgressColor = Color.Yellow;
            pbFire.Visible = false;

            fireKeyText = await this.AddComponentUITextArea(UITextAreaDescription.FromFile(fontFilename, 25, true), layerUI + 2);
            fireKeyText.TextColor = Color.Yellow;
            fireKeyText.Text = "Press space to fire!";
            fireKeyText.CenterHorizontally = CenterTargets.Screen;
            fireKeyText.Top = this.Game.Form.RenderHeight - 60;
            fireKeyText.Width = 500;
            fireKeyText.Height = 40;
            fireKeyText.HorizontalAlign = HorizontalTextAlign.Center;
            fireKeyText.VerticalAlign = VerticalTextAlign.Middle;
            fireKeyText.AdjustAreaWithText = false;
            fireKeyText.Visible = false;
        }
        private async Task InitializeUIMinimap()
        {
            miniMapBackground = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Compass.png"), SceneObjectUsages.UI, layerUI);
            miniMapBackground.Width = 200;
            miniMapBackground.Height = 200;
            miniMapBackground.Left = this.Game.Form.RenderWidth - 200 - 10;
            miniMapBackground.Top = this.Game.Form.RenderHeight - 200 - 10;
            miniMapBackground.Alpha = 0.5f;
            miniMapBackground.Visible = false;

            miniMapTank1 = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Tank.png"), SceneObjectUsages.UI, layerUI + 1);
            miniMapTank1.Width = 273 * 0.1f;
            miniMapTank1.Height = 365 * 0.1f;
            miniMapTank1.Left = this.Game.Form.RenderWidth - 150 - 10;
            miniMapTank1.Top = this.Game.Form.RenderHeight - 150 - 10;
            miniMapTank1.BaseColor = Color.Blue;
            miniMapTank1.Visible = false;

            miniMapTank2 = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Tank.png"), SceneObjectUsages.UI, layerUI + 1);
            miniMapTank2.Width = 273 * 0.1f;
            miniMapTank2.Height = 365 * 0.1f;
            miniMapTank2.Left = this.Game.Form.RenderWidth - 85 - 10;
            miniMapTank2.Top = this.Game.Form.RenderHeight - 85 - 10;
            miniMapTank2.BaseColor = Color.Red;
            miniMapTank2.Visible = false;

            windVelocity = await this.AddComponentUIProgressBar(UIProgressBarDescription.DefaultFromFile(fontFilename, 8), layerUI + 2);
            windVelocity.Caption.Text = "Wind velocity";
            windVelocity.Caption.TextColor = Color.Yellow * 0.85f;
            windVelocity.Width = 180;
            windVelocity.Height = 15;
            windVelocity.Left = miniMapBackground.AbsoluteCenter.X - 90;
            windVelocity.Top = miniMapBackground.AbsoluteCenter.Y - 130;
            windVelocity.ProgressColor = Color.DeepSkyBlue;
            windVelocity.Visible = false;

            windDirectionArrow = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Wind.png"), SceneObjectUsages.UI, layerUI + 1);
            windDirectionArrow.Width = 100;
            windDirectionArrow.Height = 100;
            windDirectionArrow.Left = miniMapBackground.AbsoluteCenter.X - 50;
            windDirectionArrow.Top = miniMapBackground.AbsoluteCenter.Y - 50;
            windDirectionArrow.BaseColor = Color.Green;
            windDirectionArrow.Visible = false;
        }
        private async Task InitializeUIShotPath()
        {
            trajectoryMarkerPool = new Sprite[5];
            for (int i = 0; i < trajectoryMarkerPool.Length; i++)
            {
                var trajectoryMarker = await this.AddComponentSprite(SpriteDescription.FromFile("SceneTanksGame/Dot_w.png"), SceneObjectUsages.UI, layerUI + 1);
                trajectoryMarker.Width = 50;
                trajectoryMarker.Height = 50;
                trajectoryMarker.BaseColor = Color.Transparent;
                trajectoryMarker.Active = false;
                trajectoryMarker.Visible = false;
                trajectoryMarker.TweenRotateRepeat(0, MathUtil.TwoPi, 1000, ScaleFuncs.Linear);

                trajectoryMarkerPool[i] = trajectoryMarker;
            }
        }
        private void PrepareUI()
        {

        }

        private Task[] InitializeModels()
        {
            return new Task[]
            {
                InitializeModelsTanks(),
                InitializeModelsTerrain(),
                InitializeLandscape(),
                InitializeModelProjectile(),
                InitializeParticleManager(),
                InitializeAudio(),
            };
        }
        private async Task InitializeModelsTanks()
        {
            var tDesc = new ModelInstancedDescription()
            {
                Name = "Tanks",
                CastShadow = true,
                Optimize = false,
                Content = new ContentDescription()
                {
                    ContentFolder = "SceneTanksGame/Leopard",
                    ModelContentFilename = "Leopard.xml",
                },
                Instances = 2,
                TransformNames = new[] { "Barrel-mesh", "Turret-mesh", "Hull-mesh" },
                TransformDependences = new[] { 1, 2, -1 },
            };

            tanks = await this.AddComponentModelInstanced(tDesc, SceneObjectUsages.Agent, layerModels);
            tanks.Visible = false;

            tankHeight = tanks[0].GetBoundingBox().Height * 0.5f;
        }
        private async Task InitializeModelsTerrain()
        {
            // Generates a random terrain using perlin noise
            NoiseMapDescriptor nmDesc = new NoiseMapDescriptor
            {
                MapWidth = mapSize,
                MapHeight = mapSize,
                Scale = 0.5f,
                Lacunarity = 2f,
                Persistance = 0.5f,
                Octaves = 4,
                Offset = Vector2.One,
                Seed = Helper.RandomGenerator.Next(),
            };
            var noiseMap = NoiseMap.CreateNoiseMap(nmDesc);

            Curve heightCurve = new Curve();
            heightCurve.Keys.Add(0, 0);
            heightCurve.Keys.Add(0.4f, 0f);
            heightCurve.Keys.Add(1f, 1f);

            float cellSize = terrainSize / mapSize;

            var textures = new HeightmapTexturesDescription
            {
                ContentPath = "SceneTanksGame/terrain",
                TexturesLR = new[] { "Diffuse.jpg" },
                NormalMaps = new[] { "Normal.jpg" },
                Scale = 0.2f,
            };
            GroundDescription groundDesc = GroundDescription.FromHeightmap(noiseMap, cellSize, terrainHeight, heightCurve, textures, 2);
            groundDesc.HeightmapDescription.UseFalloff = true;

            terrain = await this.AddComponentScenery(groundDesc, SceneObjectUsages.Ground, layerModels);
            terrain.Visible = false;

            terrainTop = terrain.GetBoundingBox().Maximum.Y;

            this.SetGround(terrain, true);
        }
        private async Task InitializeLandscape()
        {
            float w = 1920f * 0.5f;
            float h = 1080f * 0.5f;
            float d = 2000f * 0.5f;
            float elevation = 500 * 0.5f;

            VertexData[] vertices = new VertexData[]
            {
                new VertexData{ Position = new Vector3(-w*3, +h+elevation, d), Normal = Vector3.Up, Texture = new Vector2(0f, 0f) },
                new VertexData{ Position = new Vector3(+w*3, +h+elevation, d), Normal = Vector3.Up, Texture = new Vector2(3f, 0f) },
                new VertexData{ Position = new Vector3(-w*3, -h+elevation, d), Normal = Vector3.Up, Texture = new Vector2(0f, 1f) },
                new VertexData{ Position = new Vector3(+w*3, -h+elevation, d), Normal = Vector3.Up, Texture = new Vector2(3f, 1f) },
            };

            uint[] indices = new uint[]
            {
                0, 1, 2,
                1, 3, 2,
            };

            var material = MaterialContent.Default;
            material.DiffuseTexture = "SceneTanksGame/Landscape.png";

            var content = ModelDescription.FromData(vertices, indices, material);

            landScape = await this.AddComponentModel(content, SceneObjectUsages.UI, layerModels);
            landScape.Visible = false;
        }
        private async Task InitializeModelProjectile()
        {
            var sphereDesc = GeometryUtil.CreateSphere(1, 5, 5);
            var material = MaterialContent.Default;
            material.DiffuseColor = Color.DarkGray;

            var content = ModelDescription.FromData(sphereDesc, material);
            content.DepthEnabled = false;

            projectile = await this.AddComponentModel(content, SceneObjectUsages.None, layerModels + 100);
            projectile.Visible = false;
        }
        private async Task InitializeParticleManager()
        {
            particleManager = await this.AddComponentParticleManager(ParticleManagerDescription.Default());

            var pPlume = ParticleSystemDescription.InitializeSmokePlume("SceneTanksGame/particles", "smoke.png", 5);
            var pFire = ParticleSystemDescription.InitializeFire("SceneTanksGame/particles", "fire.png", 5);
            var pDust = ParticleSystemDescription.InitializeDust("SceneTanksGame/particles", "smoke.png", 5);
            var pProjectile = ParticleSystemDescription.InitializeProjectileTrail("SceneTanksGame/particles", "smoke.png", 5);
            var pExplosion = ParticleSystemDescription.InitializeExplosion("SceneTanksGame/particles", "fire.png", 5);
            var pSmokeExplosion = ParticleSystemDescription.InitializeExplosion("SceneTanksGame/particles", "smoke.png", 5);

            particleDescriptions.Add("Plume", pPlume);
            particleDescriptions.Add("Fire", pFire);
            particleDescriptions.Add("Dust", pDust);
            particleDescriptions.Add("Projectile", pProjectile);
            particleDescriptions.Add("Explosion", pExplosion);
            particleDescriptions.Add("SmokeExplosion", pSmokeExplosion);
        }
        private async Task InitializeAudio()
        {
            float nearRadius = 1000;
            ReverbPresets preset = ReverbPresets.Mountains;

            tankMoveEffect = "TankMove";
            tankDestroyedEffect = "TankDestroyed";
            tankShootingEffect = "TankShooting";
            impactEffects = new[] { "Impact1", "Impact2", "Impact3", "Impact4" };
            damageEffects = new[] { "Damage1", "Damage2", "Damage3", "Damage4" };

            AudioManager.LoadSound("Tank", "SceneTanksGame/Audio", "tank_engine.wav");
            AudioManager.LoadSound("TankDestroyed", "SceneTanksGame/Audio", "explosion_vehicle_small_close_01.wav");
            AudioManager.LoadSound("TankShooting", "SceneTanksGame/Audio", "cannon-shooting.wav");
            AudioManager.LoadSound(impactEffects[0], "SceneTanksGame/Audio", "metal_grate_large_01.wav");
            AudioManager.LoadSound(impactEffects[1], "SceneTanksGame/Audio", "metal_grate_large_02.wav");
            AudioManager.LoadSound(impactEffects[2], "SceneTanksGame/Audio", "metal_grate_large_03.wav");
            AudioManager.LoadSound(impactEffects[3], "SceneTanksGame/Audio", "metal_grate_large_04.wav");
            AudioManager.LoadSound(damageEffects[0], "SceneTanksGame/Audio", "metal_pipe_large_01.wav");
            AudioManager.LoadSound(damageEffects[1], "SceneTanksGame/Audio", "metal_pipe_large_02.wav");
            AudioManager.LoadSound(damageEffects[2], "SceneTanksGame/Audio", "metal_pipe_large_03.wav");
            AudioManager.LoadSound(damageEffects[3], "SceneTanksGame/Audio", "metal_pipe_large_04.wav");

            AudioManager.AddEffectParams(
                tankMoveEffect,
                new GameAudioEffectParameters
                {
                    SoundName = "Tank",
                    DestroyWhenFinished = false,
                    IsLooped = true,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 0.5f,
                });

            AudioManager.AddEffectParams(
                tankDestroyedEffect,
                new GameAudioEffectParameters
                {
                    SoundName = "TankDestroyed",
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });

            AudioManager.AddEffectParams(
                tankShootingEffect,
                new GameAudioEffectParameters
                {
                    SoundName = "TankShooting",
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });

            AudioManager.AddEffectParams(
                impactEffects[0],
                new GameAudioEffectParameters
                {
                    SoundName = impactEffects[0],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });
            AudioManager.AddEffectParams(
                impactEffects[1],
                new GameAudioEffectParameters
                {
                    SoundName = impactEffects[1],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });
            AudioManager.AddEffectParams(
                impactEffects[2],
                new GameAudioEffectParameters
                {
                    SoundName = impactEffects[2],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });
            AudioManager.AddEffectParams(
                impactEffects[3],
                new GameAudioEffectParameters
                {
                    SoundName = impactEffects[3],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });

            AudioManager.AddEffectParams(
                damageEffects[0],
                new GameAudioEffectParameters
                {
                    SoundName = damageEffects[0],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });
            AudioManager.AddEffectParams(
                damageEffects[1],
                new GameAudioEffectParameters
                {
                    SoundName = damageEffects[1],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });
            AudioManager.AddEffectParams(
                damageEffects[2],
                new GameAudioEffectParameters
                {
                    SoundName = damageEffects[2],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });
            AudioManager.AddEffectParams(
                damageEffects[3],
                new GameAudioEffectParameters
                {
                    SoundName = damageEffects[3],
                    IsLooped = false,
                    UseAudio3D = true,
                    EmitterRadius = nearRadius,
                    ReverbPreset = preset,
                    Volume = 1f,
                });

            await Task.CompletedTask;
        }
        private void PrepareModels()
        {
            landScape.Visible = true;
            terrain.Visible = true;

            Vector3 p1 = new Vector3(-100, 100, 0);
            Vector3 n1 = Vector3.Up;
            Vector3 p2 = new Vector3(+100, 100, 0);
            Vector3 n2 = Vector3.Up;

            if (this.FindTopGroundPosition<Triangle>(p1.X, p1.Z, out var r1))
            {
                p1 = r1.Position - (Vector3.Up * 0.1f);
                n1 = r1.Item.Normal;
            }
            if (this.FindTopGroundPosition<Triangle>(p2.X, p2.Z, out var r2))
            {
                p2 = r2.Position - (Vector3.Up * 0.1f);
                n2 = r2.Item.Normal;
            }

            tanks[0].Manipulator.SetPosition(p1);
            tanks[0].Manipulator.RotateTo(p2);
            tanks[0].Manipulator.SetNormal(n1);

            tanks[1].Manipulator.SetPosition(p2);
            tanks[1].Manipulator.RotateTo(p1);
            tanks[1].Manipulator.SetNormal(n2);

            tanks.Visible = true;
        }

        private void InitializePlayers()
        {
            player1Status = new PlayerStatus
            {
                Name = "Player 1",
                Points = 0,
                MaxLife = 100,
                CurrentLife = 100,
                MaxMove = 25,
                CurrentMove = 25,
                Color = Color.Blue,
            };

            player2Status = new PlayerStatus
            {
                Name = "Player 2",
                Points = 0,
                MaxLife = 100,
                CurrentLife = 100,
                MaxMove = 25,
                CurrentMove = 25,
                Color = Color.Red,
            };
        }
        private void UpdateGameControls(bool visible)
        {
            player1Name.Visible = visible;
            player1Points.Visible = visible;
            player1Life.Visible = visible;
            player2Name.Visible = visible;
            player2Points.Visible = visible;
            player2Life.Visible = visible;

            turnText.Visible = visible;
            gameIcon.Visible = visible;
            playerTurnMarker.Visible = visible;

            keyHelp.Visible = visible;
            keyRotate.Visible = visible;
            keyMove.Visible = visible;
            KeyPitch.Visible = visible;
            keyRotateLeftText.Visible = visible;
            keyRotateRightText.Visible = visible;
            keyMoveForwardText.Visible = visible;
            keyMoveBackwardText.Visible = visible;
            keyPitchUpText.Visible = visible;
            keyPitchDownText.Visible = visible;

            pbFire.Visible = visible;
            fireKeyText.Visible = visible;
            fireKeyText.TweenScaleBounce(1, 1.01f, 500, ScaleFuncs.CubicEaseInOut);

            miniMapBackground.Visible = visible;
            miniMapTank1.Visible = visible;
            miniMapTank2.Visible = visible;
            windVelocity.Visible = visible;
            windDirectionArrow.Visible = visible;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!gameReady)
            {
                return;
            }

            UpdateTurnStatus();
            UpdatePlayersStatus();

            if (gameEnding)
            {
                UpdateInputEndGame();

                return;
            }

            if (dialogActive)
            {
                return;
            }

            UpdateInputGame();

            if (shooting && shot != null)
            {
                IntegrateShot(gameTime);

                return;
            }

            if (freeCamera)
            {
                UpdateInputFree(gameTime);
                PaintShot(true);

                return;
            }

            UpdateInputPlayer(gameTime);
            UpdateInputShooting(gameTime);

            UpdateTanks();
            UpdateCamera(false);
        }

        private void UpdateInputGame()
        {
            if (freeCamera)
            {
                if (this.Game.Input.KeyJustReleased(Keys.F) ||
                    this.Game.Input.KeyJustReleased(Keys.Escape))
                {
                    ToggleFreeCamera();

                    return;
                }
            }
            else
            {
                if (this.Game.Input.KeyJustReleased(Keys.F))
                {
                    ToggleFreeCamera();

                    return;
                }

                if (this.Game.Input.KeyJustReleased(Keys.Escape))
                {
                    this.ShowDialog(
                        @"Press Ok if you want to exit.

You will lost all the game progress.",
                        CloseDialog,
                        () =>
                        {
                            this.Game.SetScene<SceneStart.SceneStart>();
                        });
                }
            }
        }
        private void ToggleFreeCamera()
        {
            freeCamera = !freeCamera;

            if (freeCamera)
            {
                this.Camera.MovementDelta *= 10f;
                this.Game.LockMouse = true;
            }
            else
            {
                this.Camera.MovementDelta /= 10f;
                this.Game.LockMouse = false;
            }
        }
        private void UpdateInputPlayer(GameTime gameTime)
        {
            if (this.Game.Input.KeyPressed(Keys.A))
            {
                Shooter.Manipulator.Rotate(-gameTime.ElapsedSeconds, 0, 0);

                PlayEffectMove(Shooter);
            }
            if (this.Game.Input.KeyPressed(Keys.D))
            {
                Shooter.Manipulator.Rotate(+gameTime.ElapsedSeconds, 0, 0);

                PlayEffectMove(Shooter);
            }

            if (this.Game.Input.KeyPressed(Keys.Q))
            {
                Shooter["Barrel-mesh"].Manipulator.Rotate(0, gameTime.ElapsedSeconds, 0);
            }
            if (this.Game.Input.KeyPressed(Keys.Z))
            {
                Shooter["Barrel-mesh"].Manipulator.Rotate(0, -gameTime.ElapsedSeconds, 0);
            }

            if (ShooterStatus.CurrentMove <= 0)
            {
                return;
            }

            Vector3 prevPosition = Shooter.Manipulator.Position;

            if (this.Game.Input.KeyPressed(Keys.W))
            {
                Shooter.Manipulator.MoveForward(gameTime, 10);

                PlayEffectMove(Shooter);
            }
            if (this.Game.Input.KeyPressed(Keys.S))
            {
                Shooter.Manipulator.MoveBackward(gameTime, 10);

                PlayEffectMove(Shooter);
            }

            Vector3 position = Shooter.Manipulator.Position;

            ShooterStatus.CurrentMove -= Vector3.Distance(prevPosition, position);
            ShooterStatus.CurrentMove = Math.Max(0, ShooterStatus.CurrentMove);
        }
        private void UpdateInputShooting(GameTime gameTime)
        {
            if (this.Game.Input.KeyPressed(Keys.Space))
            {
                pbFire.ProgressValue += gameTime.ElapsedSeconds * 0.5f;
                pbFire.ProgressValue %= 1f;
                pbFire.ProgressColor = pbFire.ProgressValue < 0.75f ? Color.Yellow : Color4.Lerp(Color.Yellow, Color.Red, (pbFire.ProgressValue - 0.75f) / 0.25f);
            }

            if (this.Game.Input.KeyJustReleased(Keys.Space))
            {
                Shoot(pbFire.ProgressValue);
            }
        }
        private void UpdateInputEndGame()
        {
            if (this.Game.Input.KeyJustReleased(Keys.Space))
            {
                this.Game.SetScene<SceneStart.SceneStart>();
            }
        }
        private void UpdateInputFree(GameTime gameTime)
        {
#if DEBUG
            if (this.Game.Input.RightMouseButtonPressed)
            {
                this.Camera.RotateMouse(
                    gameTime,
                    this.Game.Input.MouseXDelta,
                    this.Game.Input.MouseYDelta);
            }
#else
            this.Camera.RotateMouse(
                gameTime,
                this.Game.Input.MouseXDelta,
                this.Game.Input.MouseYDelta);
#endif

            Vector3 prevPosition = this.Camera.Position;

            if (this.Game.Input.KeyPressed(Keys.A))
            {
                this.Camera.MoveLeft(gameTime, this.Game.Input.ShiftPressed);
            }

            if (this.Game.Input.KeyPressed(Keys.D))
            {
                this.Camera.MoveRight(gameTime, this.Game.Input.ShiftPressed);
            }

            if (this.Game.Input.KeyPressed(Keys.W))
            {
                this.Camera.MoveForward(gameTime, this.Game.Input.ShiftPressed);
            }

            if (this.Game.Input.KeyPressed(Keys.S))
            {
                this.Camera.MoveBackward(gameTime, this.Game.Input.ShiftPressed);
            }

            if (this.terrain.Intersects(new IntersectionVolumeSphere(this.Camera.Position, this.Camera.CameraRadius), out var res))
            {
                this.Camera.Position = prevPosition;
            }
        }

        private void UpdateTurnStatus()
        {
            turnText.Text = $"Turn {currentTurn}";

            if (currentPlayer == 0)
            {
                playerTurnMarker.Left = this.Game.Form.RenderCenter.X - 112 - 120;
                playerTurnMarker.Rotation = 0;
            }
            else
            {
                playerTurnMarker.Left = this.Game.Form.RenderCenter.X + 120;
                playerTurnMarker.Rotation = MathUtil.Pi;
            }
        }
        private void UpdatePlayersStatus()
        {
            player1Name.Text = player1Status.Name;
            player1Points.Text = $"{player1Status.Points} points";
            player1Life.Caption.Text = $"{player1Status.CurrentLife}";
            player1Life.ProgressValue = player1Status.Health;
            tanks[0].TextureIndex = player1Status.TextureIndex;

            player2Name.Text = player2Status.Name;
            player2Points.Text = $"{player2Status.Points} points";
            player2Life.Caption.Text = $"{player2Status.CurrentLife}";
            player2Life.ProgressValue = player2Status.Health;
            tanks[1].TextureIndex = player2Status.TextureIndex;
        }
        private void UpdateTanks()
        {
            if (this.FindTopGroundPosition<Triangle>(Shooter.Manipulator.Position.X, Shooter.Manipulator.Position.Z, out var r))
            {
                Shooter.Manipulator.SetPosition(r.Position - (Vector3.Up * 0.1f));
                Shooter.Manipulator.SetNormal(r.Item.Normal, 0.05f);
            }

            Shooter["Turret-mesh"].Manipulator.RotateTo(Target.Manipulator.Position, Vector3.Up, Axis.Y, 0.01f);

            PaintMinimap();

            PaintShot(true);
        }
        private void UpdateCamera(bool firstUpdate)
        {
            // Find tanks distance vector
            Vector3 diffV = tanks[1].Manipulator.Position - tanks[0].Manipulator.Position;
            Vector3 distV = Vector3.Normalize(diffV);
            float dist = diffV.Length();

            // Interest to medium point
            Vector3 interest = tanks[0].Manipulator.Position + (distV * dist * 0.5f);

            // Perpendicular to diff
            Vector3 perp = Vector3.Normalize(Vector3.Cross(Vector3.Up, diffV));
            float y = Math.Max(100f, dist * 0.5f);
            float z = Math.Max(200f, dist);
            Vector3 position = interest + (perp * z) + (Vector3.Up * y);

            if (firstUpdate)
            {
                this.Camera.Position = position;
            }
            else
            {
                this.Camera.Goto(position, CameraTranslations.Quick);
            }

            this.Camera.Interest = interest;
        }

        private void PaintShot(bool visible)
        {
            trajectoryMarkerPool.ToList().ForEach(m =>
            {
                m.Active = false;
                m.Visible = false;
            });

            if (!visible)
            {
                return;
            }

            Vector3 from = Shooter.Manipulator.Position;
            from.Y += tankHeight;

            var shotDirection = Shooter["Barrel-mesh"].Manipulator.FinalTransform.Forward;

            Vector3 to = from + (shotDirection * 1000f);

            float sampleDist = 20;
            float distance = Vector3.Distance(from, to);
            Vector3 shootDirection = Vector3.Normalize(to - from);
            int markers = Math.Min(trajectoryMarkerPool.Length, (int)(distance / sampleDist));
            if (markers == 0)
            {
                return;
            }

            // Initialize sample dist
            float dist = sampleDist;
            for (int i = 0; i < markers; i++)
            {
                Vector3 markerPos = from + (shootDirection * dist);
                Vector3 screenPos = Vector3.Project(markerPos,
                    this.Game.Graphics.Viewport.X,
                    this.Game.Graphics.Viewport.Y,
                    this.Game.Graphics.Viewport.Width,
                    this.Game.Graphics.Viewport.Height,
                    this.Game.Graphics.Viewport.MinDepth,
                    this.Game.Graphics.Viewport.MaxDepth,
                    this.Camera.View * this.Camera.Projection);
                float scale = (1f - screenPos.Z) * 1000f;

                trajectoryMarkerPool[i].Left = screenPos.X - (trajectoryMarkerPool[i].Width * 0.5f);
                trajectoryMarkerPool[i].Top = screenPos.Y - (trajectoryMarkerPool[i].Height * 0.5f);
                trajectoryMarkerPool[i].Scale = scale;
                trajectoryMarkerPool[i].BaseColor = ShooterStatus.Color;
                trajectoryMarkerPool[i].Alpha = 1f - (i / (float)markers);
                trajectoryMarkerPool[i].Active = true;
                trajectoryMarkerPool[i].Visible = true;

                dist += sampleDist;
            }
        }
        private void PaintMinimap()
        {
            // Set wind velocity and direction
            windVelocity.ProgressValue = currentWindVelocity / maxWindVelocity;
            windDirectionArrow.Rotation = Helper.AngleSigned(Vector2.UnitY, windDirection);

            // Get terrain minimap rectangle
            BoundingBox bbox = terrain.GetBoundingBox();
            RectangleF terrainRect = new RectangleF(bbox.Minimum.X, bbox.Minimum.Z, bbox.Width, bbox.Depth);

            // Get object space positions and transform to screen space
            Vector2 tank1 = tanks[0].Manipulator.Position.XZ() - terrainRect.TopLeft;
            Vector2 tank2 = tanks[1].Manipulator.Position.XZ() - terrainRect.TopLeft;

            // Get the mini map rectangle
            RectangleF miniMapRect = miniMapBackground.GrandpaRectangle;

            // Get the marker sprite bounds
            Vector2 markerBounds1 = new Vector2(miniMapTank1.Width, miniMapTank1.Height);
            Vector2 markerBounds2 = new Vector2(miniMapTank2.Width, miniMapTank2.Height);

            // Calculate proportional 2D locations (tank to terrain)
            float tank1ToTerrainX = tank1.X / terrainRect.Width;
            float tank1ToTerrainY = tank1.Y / terrainRect.Height;
            float tank2ToTerrainX = tank2.X / terrainRect.Width;
            float tank2ToTerrainY = tank2.Y / terrainRect.Height;

            // Marker to minimap inverting Y coordinates
            Vector2 markerToMinimap1 = new Vector2(miniMapRect.Width * tank1ToTerrainX, miniMapRect.Height * (1f - tank1ToTerrainY));
            Vector2 markerToMinimap2 = new Vector2(miniMapRect.Width * tank2ToTerrainX, miniMapRect.Height * (1f - tank2ToTerrainY));

            // Translate and center into the minimap
            Vector2 mt1Position = markerToMinimap1 + miniMapRect.TopLeft - (markerBounds1 * 0.5f);
            Vector2 mt2Position = markerToMinimap2 + miniMapRect.TopLeft - (markerBounds2 * 0.5f);

            // Set marker position
            miniMapTank1.SetPosition(mt1Position);
            miniMapTank2.SetPosition(mt2Position);

            // Set marker rotation
            miniMapTank1.Rotation = Helper.AngleSigned(Vector2.UnitY, Vector2.Normalize(tanks[0].Manipulator.Forward.XZ()));
            miniMapTank2.Rotation = Helper.AngleSigned(Vector2.UnitY, Vector2.Normalize(tanks[1].Manipulator.Forward.XZ()));
        }

        private void Shoot(float shotForce)
        {
            var shotDirection = Shooter["Barrel-mesh"].Manipulator.FinalTransform.Forward;

            shot = new ParabolicShot();
            shot.Configure(this.Game.GameTime, shotDirection, shotForce * 200, windDirection, currentWindVelocity);

            shooting = true;

            PlayEffectShooting(Shooter);
        }
        private void IntegrateShot(GameTime gameTime)
        {
            // Set projectile position
            Vector3 shotPos = shot.Integrate(gameTime, Vector3.Zero, Vector3.Zero);
            Vector3 projectilePosition = Shooter.Manipulator.Position + shotPos;
            projectilePosition.Y += tankHeight;
            projectile.Manipulator.SetPosition(projectilePosition, true);
            var projVolume = projectile.GetBoundingSphere(true);
            projectile.Visible = true;

            // Test collision with target
            if (Target.Intersects(projVolume, out var targetImpact))
            {
                ResolveShot(true, targetImpact.Position);

                return;
            }

            // Test if projectile is under the terrain box
            var terrainBox = terrain.GetBoundingBox();
            if (projVolume.Center.Y + projVolume.Radius < terrainBox.Minimum.Y)
            {
                ResolveShot(false, null);

                return;
            }

            // Test full collision with terrain mesh
            if (terrain.Intersects(projVolume, out var terrainImpact))
            {
                ResolveShot(false, terrainImpact.Position);
            }
        }
        private void ResolveShot(bool impact, Vector3? impactPosition)
        {
            shot = null;
            shooting = false;

            Vector3 outPosition = Vector3.Up * (terrainTop + 1);
            projectile.Manipulator.SetPosition(outPosition, true);
            var sph = projectile.GetBoundingSphere(true);
            projectile.Visible = false;

            if (impact)
            {
                int res = Helper.RandomGenerator.Next(10, 50);

                ShooterStatus.Points += res * 100;
                TargetStatus.CurrentLife = MathUtil.Clamp(TargetStatus.CurrentLife - res, 0, TargetStatus.MaxLife);

                if (impactPosition.HasValue)
                {
                    this.AddExplosionSystem(impactPosition.Value);
                    PlayEffectDamage(Target);
                    PlayEffectImpact(Target);
                }

                if (TargetStatus.CurrentLife == 0)
                {
                    Task.Run(async () =>
                    {
                        Vector3 min = Vector3.One * -5f;
                        Vector3 max = Vector3.One * +5f;

                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));
                        PlayEffectDestroyed(Target);

                        await Task.Delay(500);

                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));

                        await Task.Delay(500);

                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));
                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));

                        await Task.Delay(3000);

                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));
                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));
                        this.AddExplosionSystem(Target.Manipulator.Position + Helper.RandomGenerator.NextVector3(min, max));
                        PlayEffectDestroyed(Target);
                    });
                }
            }
            else
            {
                if (impactPosition.HasValue)
                {
                    this.AddSmokePlumeSystem(impactPosition.Value);
                    PlayEffectDestroyed(impactPosition.Value);
                }
            }

            Task.Run(async () =>
            {
                dialogActive = true;

                await ShowMessage(impact ? "Impact!" : "You miss!", 2000);

                await EvaluateTurn(ShooterStatus, TargetStatus);

                if (!gameEnding)
                {
                    await ShowMessage($"Your turn {ShooterStatus.Name}", 2000);
                }

                dialogActive = false;
            });
        }

        private async Task EvaluateTurn(PlayerStatus shooter, PlayerStatus target)
        {
            pbFire.ProgressValue = 0;

            if (target.CurrentLife == 0)
            {
                gameEnding = true;

                gameMessage.Text = $"The winner is {shooter.Name}!";
                gameMessage.TextColor = shooter.Color;
                gameMessage.TextShadowColor = shooter.Color * 0.5f;
                gameMessage.Show(1000);
                gameMessage.TweenScale(0, 1, 1000, ScaleFuncs.CubicEaseIn);

                fadePanel.Show(3000);

                await Task.Delay(3000);

                gameKeyHelp.Show(1000);
                gameKeyHelp.TweenScaleBounce(1, 1.01f, 500, ScaleFuncs.CubicEaseInOut);

                return;
            }

            currentPlayer++;
            currentPlayer %= 2;

            PaintShot(true);

            if (currentPlayer == 0)
            {
                currentTurn++;

                ShooterStatus.NewTurn();
                TargetStatus.NewTurn();

                currentWindVelocity = Helper.RandomGenerator.NextFloat(0f, maxWindVelocity);
                windDirection = Helper.RandomGenerator.NextVector2(-Vector2.One, Vector2.One);

                Parallel.ForEach(particleManager.ParticleSystems, p =>
                {
                    var particleParams = p.GetParameters();
                    particleParams.Gravity = new Vector3(windDirection.X, 0, windDirection.Y) * currentWindVelocity;
                    p.SetParameters(particleParams);
                });
            }
        }

        private void AddExplosionSystem(Vector3 position)
        {
            Vector3 velocity = Vector3.Up;
            float duration = 0.5f;
            float rate = 0.01f;

            var emitter1 = new ParticleEmitter()
            {
                Position = position,
                Velocity = velocity,
                Duration = duration,
                EmissionRate = rate,
                InfiniteDuration = false,
                MaximumDistance = 1000f,
            };
            var emitter2 = new ParticleEmitter()
            {
                Position = position,
                Velocity = velocity,
                Duration = duration * 5f,
                EmissionRate = rate * 10f,
                InfiniteDuration = false,
                MaximumDistance = 1000f,
            };

            this.particleManager.AddParticleSystem(ParticleSystemTypes.CPU, this.particleDescriptions["Explosion"], emitter1);
            this.particleManager.AddParticleSystem(ParticleSystemTypes.CPU, this.particleDescriptions["SmokeExplosion"], emitter2);
        }
        private void AddSmokePlumeSystem(Vector3 position)
        {
            Vector3 velocity = Vector3.Up;
            float duration = Helper.RandomGenerator.NextFloat(10, 30);
            float rate = Helper.RandomGenerator.NextFloat(0.1f, 1f);

            var emitter1 = new ParticleEmitter()
            {
                Position = position,
                Velocity = velocity,
                Duration = duration,
                EmissionRate = rate * 0.5f,
                InfiniteDuration = false,
                MaximumDistance = 1000f,
            };

            var emitter2 = new ParticleEmitter()
            {
                Position = position,
                Velocity = velocity,
                Duration = duration + (duration * 0.1f),
                EmissionRate = rate,
                InfiniteDuration = false,
                MaximumDistance = 5000f,
            };

            this.particleManager.AddParticleSystem(ParticleSystemTypes.CPU, this.particleDescriptions["Fire"], emitter1);
            this.particleManager.AddParticleSystem(ParticleSystemTypes.CPU, this.particleDescriptions["Plume"], emitter2);
        }

        private async Task ShowMessage(string text, int delay)
        {
            gameMessage.Text = text;
            gameMessage.TweenScale(0, 1, 500, ScaleFuncs.CubicEaseIn);
            gameMessage.Show(500);

            await Task.Delay(delay);

            gameMessage.ClearTween();
            gameMessage.Hide(100);

            await Task.Delay(100);
        }

        private void PlayEffectMove(ITransformable3D emitter)
        {
            if (tankMoveEffectInstance == null)
            {
                tankMoveEffectInstance = AudioManager.CreateEffectInstance(tankMoveEffect, emitter, this.Camera);
                tankMoveEffectInstance.Play();

                Task.Run(async () =>
                {
                    await Task.Delay(10000);
                    tankMoveEffectInstance.Stop();
                    tankMoveEffectInstance.Dispose();
                    tankMoveEffectInstance = null;
                });
            }
        }
        private void PlayEffectShooting(ITransformable3D emitter)
        {
            AudioManager.CreateEffectInstance(tankShootingEffect, emitter, this.Camera)?.Play();
        }
        private void PlayEffectImpact(ITransformable3D emitter)
        {
            int index = Helper.RandomGenerator.Next(0, impactEffects.Length);
            index %= impactEffects.Length - 1;
            AudioManager.CreateEffectInstance(impactEffects[index], emitter, this.Camera)?.Play();
        }
        private void PlayEffectDamage(ITransformable3D emitter)
        {
            int index = Helper.RandomGenerator.Next(0, damageEffects.Length);
            index %= damageEffects.Length - 1;
            AudioManager.CreateEffectInstance(damageEffects[index], emitter, this.Camera)?.Play();
        }
        private void PlayEffectDestroyed(ITransformable3D emitter)
        {
            AudioManager.CreateEffectInstance(tankDestroyedEffect, emitter, this.Camera)?.Play();
        }
        private void PlayEffectDestroyed(Vector3 emitter)
        {
            AudioManager.CreateEffectInstance(tankDestroyedEffect, emitter, this.Camera)?.Play();
        }

        private void ShowDialog(string message, Action onCloseCallback, Action onAcceptCallback)
        {
            dialogActive = true;

            if (lastOnCloseHandler != null)
            {
                dialogClose.JustReleased -= lastOnCloseHandler;
            }
            if (onCloseCallback != null)
            {
                lastOnCloseHandler = (sender, args) =>
                {
                    onCloseCallback.Invoke();
                };

                dialogClose.JustReleased += lastOnCloseHandler;
            }

            if (lastOnAcceptHandler != null)
            {
                dialogAccept.JustReleased -= lastOnAcceptHandler;
            }
            if (onAcceptCallback != null)
            {
                lastOnAcceptHandler = (sender, args) =>
                {
                    onAcceptCallback.Invoke();
                };

                dialogAccept.JustReleased += lastOnAcceptHandler;
            }

            dialogText.Text = message;

            dialog.Show(500);
            fadePanel.TweenAlpha(0, 0.5f, 500, ScaleFuncs.Linear);
            Game.VisibleMouse = true;
        }
        private void CloseDialog()
        {
            dialog.Hide(500);
            fadePanel.TweenAlpha(0.5f, 0f, 500, ScaleFuncs.Linear);
            Game.VisibleMouse = false;

            Task.Run(async () =>
            {
                await Task.Delay(500);

                dialogActive = false;
            });
        }
    }

    /// <summary>
    /// Paralbolic shot helper
    /// </summary>
    public class ParabolicShot
    {
        /// <summary>
        /// Gravity acceleration
        /// </summary>
        private readonly float g = 50f;

        /// <summary>
        /// Initial shot time
        /// </summary>
        private TimeSpan initialTime;
        /// <summary>
        /// Initial velocity
        /// </summary>
        public Vector3 initialVelocity;
        /// <summary>
        /// Horizontal velocity component
        /// </summary>
        public Vector2 horizontalVelocity;
        /// <summary>
        /// Vertical velocity component
        /// </summary>
        public float verticalVelocity;
        /// <summary>
        /// Wind force (direction plus magnitude)
        /// </summary>
        public Vector3 wind;

        /// <summary>
        /// Configures the parabolic shot
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="shotDirection">Shot direction</param>
        /// <param name="shotForce">Shot force</param>
        /// <param name="windDirection">Wind direction</param>
        /// <param name="windForce">Wind force</param>
        public void Configure(GameTime gameTime, Vector3 shotDirection, float shotForce, Vector2 windDirection, float windForce)
        {
            initialTime = TimeSpan.FromMilliseconds(gameTime.TotalMilliseconds);
            initialVelocity = shotDirection * shotForce;
            horizontalVelocity = initialVelocity.XZ();
            verticalVelocity = initialVelocity.Y;
            wind = new Vector3(windDirection.X, 0, windDirection.Y) * windForce;
        }

        /// <summary>
        /// Gets the horizontal shot distance at the specified time
        /// </summary>
        /// <param name="time">Time</param>
        public Vector2 GetHorizontalDistance(float time)
        {
            return horizontalVelocity * time;
        }
        /// <summary>
        /// Gets the vertical shot distance at the specified time
        /// </summary>
        /// <param name="time">Time</param>
        /// <param name="shooterPosition">Shooter position</param>
        /// <param name="targetPosition">Target position</param>
        /// <remarks>Shooter and target positions were used for height difference calculation</remarks>
        public float GetVerticalDistance(float time, Vector3 shooterPosition, Vector3 targetPosition)
        {
            float h = shooterPosition.Y - targetPosition.Y;

            return h + (verticalVelocity * time) - (g * time * time / 2f);
        }

        /// <summary>
        /// Gets the horizontal velocity
        /// </summary>
        public Vector2 GetHorizontalVelocity()
        {
            return horizontalVelocity;
        }
        /// <summary>
        /// Gets the vertical velocity at the specified time
        /// </summary>
        /// <param name="time">Time</param>
        public float GetVerticalVelocity(float time)
        {
            return verticalVelocity - g * time;
        }

        /// <summary>
        /// Gets the horizontal acceleration
        /// </summary>
        public float GetHorizontalAcceleration()
        {
            return 0f;
        }
        /// <summary>
        /// Gets the vertical acceleration
        /// </summary>
        public float GetVerticalAcceleration()
        {
            return -g;
        }

        /// <summary>
        /// Gets the total time of flight of the projectile, from shooter to target
        /// </summary>
        /// <param name="shooterPosition">Shooter position</param>
        /// <param name="targetPosition">Target position</param>
        /// <returns>Returns the total time of flight of the projectile</returns>
        public float GetTimeOfFlight(Vector3 shooterPosition, Vector3 targetPosition)
        {
            float h = shooterPosition.Y - targetPosition.Y;
            if (h == 0)
            {
                return 2f * verticalVelocity / g;
            }
            else
            {
                return (verticalVelocity + (float)Math.Sqrt((verticalVelocity * verticalVelocity) + 2f * g * h)) / g;
            }
        }

        /// <summary>
        /// Gets the trajectory curve of the shot
        /// </summary>
        /// <param name="shooterPosition">Shooter position</param>
        /// <param name="targetPosition">Target position</param>
        /// <returns>Returns the trajectory curve of the shot</returns>
        public Curve3D ComputeCurve(Vector3 shooterPosition, Vector3 targetPosition)
        {
            Curve3D curve = new Curve3D();

            float flightTime = GetTimeOfFlight(shooterPosition, targetPosition);
            float sampleTime = 0.1f;
            for (float time = 0; time < flightTime; time += sampleTime)
            {
                Vector2 horizontalDist = GetHorizontalDistance(time);
                float verticalDist = GetVerticalDistance(time, shooterPosition, targetPosition);

                Vector3 position = new Vector3(horizontalDist.X, verticalDist, horizontalDist.Y) + (wind * time);
                curve.AddPosition(time, position);
            }

            return curve;
        }

        /// <summary>
        /// Integrates the shot in time
        /// </summary>
        /// <param name="gameTime">Game time</param>
        /// <param name="shooter">Shooter position</param>
        /// <param name="target">Target position</param>
        /// <returns>Returns the current parabolic shot position (relative to shooter position)</returns>
        public Vector3 Integrate(GameTime gameTime, Vector3 shooter, Vector3 target)
        {
            float time = (float)(gameTime.TotalSeconds - initialTime.TotalSeconds);

            Vector2 horizontalDist = GetHorizontalDistance(time);
            float verticalDist = GetVerticalDistance(time, shooter, target);

            return new Vector3(horizontalDist.X, verticalDist, horizontalDist.Y) + (wind * time);
        }
    }

    public class PlayerStatus
    {
        public string Name { get; set; }
        public int Points { get; set; }
        public int MaxLife { get; set; }
        public int CurrentLife { get; set; }
        public int MaxMove { get; set; }
        public float CurrentMove { get; set; }
        public Color Color { get; set; }
        public float Health
        {
            get
            {
                return (float)CurrentLife / MaxLife;
            }
        }
        public uint TextureIndex
        {
            get
            {
                if (Health > 0.6666f)
                {
                    return 0;
                }
                else if (Health > 0)
                {
                    return 1;
                }

                return 2;
            }
        }

        public void NewTurn()
        {
            CurrentMove = MaxMove;
        }
    }
}
