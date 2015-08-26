﻿using System;
using System.Diagnostics;
using Engine;
using Engine.PathFinding;
using SharpDX;
using Engine.Common;

namespace DeferredTest
{
    public class TestScene3D : Scene
    {
        private TextDrawer title = null;
        private TextDrawer load = null;
        private TextDrawer help = null;

        private Model tank = null;
        private Model helicopter = null;
        private ModelInstanced helicopters = null;
        private Terrain terrain = null;

        private SpriteTexture bufferDrawer = null;
        private int textIntex = 0;
        private bool animateLights = false;

        private LineListDrawer lineDrawer = null;

        public TestScene3D(Game game)
            : base(game, SceneModesEnum.DeferredLightning)
        {

        }

        public override void Initialize()
        {
            base.Initialize();

            this.Camera.NearPlaneDistance = 0.5f;
            this.Camera.FarPlaneDistance = 5000f;

            #region Texts

            this.title = this.AddText("Tahoma", 18, Color.White);
            this.load = this.AddText("Lucida Casual", 12, Color.Yellow);
            this.help = this.AddText("Lucida Casual", 12, Color.Yellow);

            this.title.Text = "Deferred Ligthning test";
            this.load.Text = "";
            this.help.Text = "";

            this.title.Position = Vector2.Zero;
            this.load.Position = new Vector2(0, 24);
            this.help.Position = new Vector2(0, 48);

            #endregion

            #region Models

            string resources = @"Resources";

            Stopwatch sw = Stopwatch.StartNew();

            string loadingText = null;

            #region Terrain

            sw.Restart();
            this.terrain = this.AddTerrain(new TerrainDescription()
            {
                ContentPath = resources,
                ModelFileName = "terrain.dae",
                UseQuadtree = true,
                UsePathFinding = true,
                PathNodeSize = 2f,
                PathNodeInclination = MathUtil.DegreesToRadians(35),
                AddSkydom = true,
                SkydomTexture = "sunset.dds",
                Opaque = true,
                AddVegetation = true,
                Vegetation = new[]
                {
                    new TerrainDescription.VegetationDescription()
                    {
                        VegetarionTextures = new[] { "tree0.dds", "tree1.dds" },
                        Saturation = 0.15f,
                        Radius = 300f,
                        MinSize = Vector2.One * 2.50f,
                        MaxSize = Vector2.One * 3.50f,
                        Opaque = true,
                    },
                    new TerrainDescription.VegetationDescription()
                    {
                        VegetarionTextures = new[] { "grass.png" },
                        Saturation = 20f,
                        Radius = 50f,
                        MinSize = Vector2.One * 0.20f,
                        MaxSize = Vector2.One * 0.25f,
                        Opaque = false,
                    }
                },
            });
            sw.Stop();
            loadingText += string.Format("terrain: {0} ", sw.Elapsed.TotalSeconds);

            this.SceneVolume = this.terrain.GetBoundingSphere();

            #endregion

            #region Helicopter

            sw.Restart();
            this.helicopter = this.AddModel(new ModelDescription()
            {
                ContentPath = resources,
                ModelFileName = "helicopter.dae",
                Opaque = true,
                TextureIndex = 2,
            });
            sw.Stop();
            loadingText += string.Format("helicopter: {0} ", sw.Elapsed.TotalSeconds);

            #endregion

            #region Helicopters

            sw.Restart();
            this.helicopters = this.AddInstancingModel(new ModelInstancedDescription()
            {
                ContentPath = resources,
                ModelFileName = "helicopter.dae",
                Opaque = true,
                Instances = 2,
            });
            sw.Stop();
            loadingText += string.Format("helicopters: {0} ", sw.Elapsed.TotalSeconds);

            #endregion

            #region Tank

            sw.Restart();
            this.tank = this.AddModel(new ModelDescription()
            {
                ContentPath = resources,
                ModelFileName = "tank.dae",
                Opaque = true,
            });
            sw.Stop();
            loadingText += string.Format("tank: {0} ", sw.Elapsed.TotalSeconds);

            this.tank.Manipulator.SetScale(3);

            #endregion

            this.load.Text = loadingText;

            #endregion

            #region Debug Buffer Drawer

            int width = (int)(this.Game.Form.RenderWidth * 0.33f);
            int height = (int)(this.Game.Form.RenderHeight * 0.33f);
            int smLeft = this.Game.Form.RenderWidth - width;
            int smTop = this.Game.Form.RenderHeight - height;

            this.bufferDrawer = this.AddSpriteTexture(new SpriteTextureDescription()
            {
                Left = smLeft,
                Top = smTop,
                Width = width,
                Height = height,
                Channel = SpriteTextureChannelsEnum.NoAlpha,
            });

            this.bufferDrawer.Visible = false;

            #endregion

            #region Object locations

            Vector3 cameraPosition = Vector3.Zero;
            int modelCount = 0;

            Vector3 tankPosition;
            Triangle tankTriangle;
            if (this.terrain.FindTopGroundPosition(0, 0, out tankPosition, out tankTriangle))
            {
                //Inclination
                this.tank.Manipulator.SetPosition(tankPosition, true);
                this.tank.Manipulator.SetNormal(tankTriangle.Normal);
                cameraPosition += tankPosition;
                modelCount++;
            }

            Vector3 helicopterPosition;
            if (this.terrain.FindTopGroundPosition(20, -20, out helicopterPosition))
            {
                helicopterPosition.Y += 10f;
                this.helicopter.Manipulator.SetPosition(helicopterPosition, true);
                cameraPosition += helicopterPosition;
                modelCount++;
            }

            for (int i = 0; i < this.helicopters.Count; i++)
            {
                Vector3 heliPos;
                if (this.terrain.FindTopGroundPosition((i * 10) - 20, 20, out heliPos))
                {
                    heliPos.Y += 10f;
                    this.helicopters.Instances[i].Manipulator.SetPosition(heliPos, true);
                    cameraPosition += heliPos;
                    modelCount++;
                }
            }

            cameraPosition /= (float)modelCount;
            this.Camera.Goto(cameraPosition + new Vector3(-30, 30, -30));
            this.Camera.LookTo(cameraPosition + Vector3.Up);

            #endregion

            #region Lights

            this.Lights.EnableShadows = true;

            this.Lights.FogColor = Color.WhiteSmoke;
            this.Lights.FogStart = 50f;
            this.Lights.FogRange = 150f;

            this.Lights.DirectionalLights[0].Enabled = true;
            this.Lights.DirectionalLights[1].Enabled = false;
            this.Lights.DirectionalLights[2].Enabled = false;

            int f = 12;
            int l = (f - 1) * 5;

            Random rnd = new Random(0);

            for (int i = 0; i < f; i++)
            {
                for (int x = 0; x < f; x++)
                {
                    Vector3 lightPosition;
                    if (!this.terrain.FindTopGroundPosition((i * 10) - l, (x * 10) - l, out lightPosition))
                    {
                        lightPosition = new Vector3((i * 10) - l, 1f, (x * 10) - l);
                    }
                    else
                    {
                        lightPosition.Y += 1f;
                    }

                    SceneLightPoint pointLight = new SceneLightPoint()
                    {
                        Enabled = true,
                        Ambient = new Color4(1.0f, 1.0f, 1.0f, 1.0f),
                        Diffuse = new Color4(rnd.NextFloat(0, 1), rnd.NextFloat(0, 1), rnd.NextFloat(0, 1), 1.0f),
                        Specular = new Color4(0.5f, 0.5f, 0.5f, 1.0f),
                        Attenuation = new Vector3(rnd.NextFloat(0, 1) > 0.5f ? 1 : -1, 0.0f, 0.0f),
                        Range = rnd.NextFloat(1, 25),
                        Position = lightPosition,
                    };

                    this.Lights.Add(pointLight);
                }
            }

            SceneLightSpot spotLight = new SceneLightSpot()
            {
                Enabled = true,
                Ambient = new Color4(1.0f, 1.0f, 1.0f, 1.0f),
                Diffuse = new Color4(1.0f, 0.0f, 0.0f, 1.0f),
                Specular = new Color4(0.5f, 0.5f, 0.5f, 1.0f),
                Attenuation = new Vector3(1.0f, 0.0f, 0.1f),
                Position = new Vector3(0, 15, 0),
                Direction = Vector3.Down,
                Range = 30,
                Spot = 20,
            };

            this.Lights.Add(spotLight);

            #region Light Sphere Marker

            Line[] axis = GeometryUtil.CreateAxis(Matrix.Identity, 5f);

            this.lineDrawer = this.AddLineListDrawer(axis, Color.Red);
            this.lineDrawer.Opaque = false;

            #endregion

            #endregion
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (this.Game.Input.KeyJustReleased(Keys.Escape))
            {
                this.Game.Exit();
            }

            Ray cursorRay = this.GetPickingRay();

            #region Cursor picking and positioning

            Vector3 position;
            Triangle triangle;
            bool picked = this.terrain.PickNearest(ref cursorRay, out position, out triangle);

            #endregion

            #region Debug

            if (this.DrawContext.GeometryMap != null && this.DrawContext.GeometryMap.Length > 0)
            {
                if (this.Game.Input.KeyJustReleased(Keys.F1))
                {
                    //Colors
                    this.bufferDrawer.Texture = this.DrawContext.GeometryMap[0];
                    this.bufferDrawer.Channels = SpriteTextureChannelsEnum.All;
                    this.bufferDrawer.Visible = true;
                    this.help.Text = "Colors";
                }

                if (this.Game.Input.KeyJustReleased(Keys.F2))
                {
                    if (this.bufferDrawer.Texture == this.DrawContext.GeometryMap[1] &&
                        this.bufferDrawer.Channels == SpriteTextureChannelsEnum.NoAlpha)
                    {
                        //Shadow factor map
                        this.bufferDrawer.Texture = this.DrawContext.GeometryMap[1];
                        this.bufferDrawer.Channels = SpriteTextureChannelsEnum.Alpha;
                        this.help.Text = "Shadow factor map";
                    }
                    else
                    {
                        //Normals
                        this.bufferDrawer.Texture = this.DrawContext.GeometryMap[1];
                        this.bufferDrawer.Channels = SpriteTextureChannelsEnum.NoAlpha;
                        this.help.Text = "Normals";
                    }
                    this.bufferDrawer.Visible = true;
                }

                if (this.Game.Input.KeyJustReleased(Keys.F3))
                {
                    if (this.bufferDrawer.Texture == this.DrawContext.GeometryMap[2] &&
                        this.bufferDrawer.Channels == SpriteTextureChannelsEnum.NoAlpha)
                    {
                        //Depth
                        this.bufferDrawer.Texture = this.DrawContext.GeometryMap[2];
                        this.bufferDrawer.Channels = SpriteTextureChannelsEnum.Alpha;
                        this.bufferDrawer.Visible = true;
                        this.help.Text = "Depth";
                    }
                    else
                    {
                        //Position
                        this.bufferDrawer.Texture = this.DrawContext.GeometryMap[2];
                        this.bufferDrawer.Channels = SpriteTextureChannelsEnum.NoAlpha;
                        this.bufferDrawer.Visible = true;
                        this.help.Text = "Position";
                    }
                }
            }

            if (this.DrawContext.LightMap != null)
            {
                if (this.Game.Input.KeyJustReleased(Keys.F4))
                {
                    if (this.bufferDrawer.Texture == this.DrawContext.LightMap &&
                        this.bufferDrawer.Channels == SpriteTextureChannelsEnum.NoAlpha)
                    {
                        //Specular map
                        this.bufferDrawer.Texture = this.DrawContext.LightMap;
                        this.bufferDrawer.Channels = SpriteTextureChannelsEnum.Alpha;
                        this.bufferDrawer.Visible = true;
                        this.help.Text = "Specular map";
                    }
                    else
                    {
                        //Light map
                        this.bufferDrawer.Texture = this.DrawContext.LightMap;
                        this.bufferDrawer.Channels = SpriteTextureChannelsEnum.NoAlpha;
                        this.bufferDrawer.Visible = true;
                        this.help.Text = "Light map";
                    }
                }
            }

            if (this.DrawContext.ShadowMap != null)
            {
                if (this.Game.Input.KeyJustReleased(Keys.F5))
                {
                    //Shadow map
                    this.bufferDrawer.Texture = this.DrawContext.ShadowMap;
                    this.bufferDrawer.Channels = SpriteTextureChannelsEnum.Red;
                    this.bufferDrawer.Visible = true;
                    this.help.Text = "Shadow map";
                }
            }

            if (this.Game.Input.KeyJustReleased(Keys.F6))
            {
                this.bufferDrawer.Visible = !this.bufferDrawer.Visible;
                this.help.Visible = this.bufferDrawer.Visible;
            }

            if (this.Game.Input.KeyJustReleased(Keys.F7))
            {
                this.terrain.Visible = !this.terrain.Visible;
            }

            if (this.Game.Input.KeyJustReleased(Keys.F8))
            {
                this.tank.Visible = !this.tank.Visible;
            }

            if (this.Game.Input.KeyJustReleased(Keys.F9))
            {
                this.helicopter.Visible = !this.helicopter.Visible;
            }

            if (this.Game.Input.KeyJustReleased(Keys.F10))
            {
                this.helicopters.Visible = !this.helicopters.Visible;
            }

            if (this.Game.Input.KeyJustReleased(Keys.D1))
            {
                this.textIntex = this.textIntex > 0 ? this.textIntex - 1 : 0;
            }

            if (this.Game.Input.KeyJustReleased(Keys.D2))
            {
                int max = (this.Statistics != null ? this.Statistics.Length : 0) - 1;

                this.textIntex = this.textIntex < max ? this.textIntex + 1 : max;
            }

            #endregion

            #region Tank

            if (this.Game.Input.LeftMouseButtonPressed)
            {
                if (picked)
                {
                    Path p = this.terrain.FindPath(this.tank.Manipulator.Position, position);
                    if (p != null)
                    {
                        this.tank.Manipulator.Follow(p.GenerateBezierPath(), 0.2f);
                    }
                }
            }

            if (this.tank.Manipulator.IsFollowingPath)
            {
                Vector3 pos = this.tank.Manipulator.Position;

                Vector3 tankPosition;
                Triangle tankTriangle;
                if (this.terrain.FindTopGroundPosition(pos.X, pos.Z, out tankPosition, out tankTriangle))
                {
                    this.tank.Manipulator.SetNormal(tankTriangle.Normal);
                }
            }

            #endregion

            #region Camera

#if DEBUG
            if (this.Game.Input.RightMouseButtonPressed)
#endif
            {
                this.Camera.RotateMouse(
                    this.Game.GameTime,
                    this.Game.Input.MouseXDelta,
                    this.Game.Input.MouseYDelta);
            }

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

            #endregion

            #region Lights

            if (this.Game.Input.KeyJustReleased(Keys.Space))
            {
                this.Lights.EnableShadows = !this.Lights.EnableShadows;
            }

            if (this.Lights.SpotLights.Length > 0)
            {
                if (this.Game.Input.KeyPressed(Keys.Left))
                {
                    this.Lights.SpotLights[0].Position += (Vector3.Left) * 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.Right))
                {
                    this.Lights.SpotLights[0].Position += (Vector3.Right) * 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.Up))
                {
                    this.Lights.SpotLights[0].Position += (Vector3.ForwardLH) * 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.Down))
                {
                    this.Lights.SpotLights[0].Position += (Vector3.BackwardLH) * 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.PageUp))
                {
                    this.Lights.SpotLights[0].Position += (Vector3.Up) * 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.PageDown))
                {
                    this.Lights.SpotLights[0].Position += (Vector3.Down) * 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.Add))
                {
                    this.Lights.SpotLights[0].Range += 0.1f;
                }

                if (this.Game.Input.KeyPressed(Keys.Subtract))
                {
                    this.Lights.SpotLights[0].Range -= 0.1f;
                }

                this.lineDrawer.Manipulator.SetPosition(this.Lights.SpotLights[0].Position);
                this.lineDrawer.Manipulator.LookAt(this.Lights.SpotLights[0].Position + this.Lights.SpotLights[0].Direction, false);
            }

            if (this.Game.Input.KeyJustReleased(Keys.P))
            {
                this.animateLights = !this.animateLights;
            }

            if (animateLights)
            {
                for (int i = 1; i < this.Lights.PointLights.Length; i++)
                {
                    var l = this.Lights.PointLights[i];

                    if (l.Attenuation.X == 1) l.Range += 0.5f;
                    if (l.Attenuation.X == -1) l.Range -= 2f;

                    if (l.Range <= 0)
                    {
                        l.Range = 0;
                        l.Attenuation.X = 1;
                    }

                    if (l.Range >= 25)
                    {
                        l.Range = 25;
                        l.Attenuation.X = -1;
                    }
                }
            }

            #endregion

            if (this.Game.Form.IsFullscreen)
            {
                this.load.Text = this.Game.RuntimeText;
            }
        }
    }
}