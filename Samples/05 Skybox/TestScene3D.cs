﻿using Engine;
using Engine.Common;
using Engine.Content;
using Engine.PathFinding.RecastNavigation;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Skybox
{
    public class TestScene3D : Scene
    {
        private const int layerHUD = 99;
        private const float alpha = 0.25f;

        private readonly Vector2[] firePositions = new[]
        {
            new Vector2(+5, +5),
            new Vector2(-5, +5),
            new Vector2(+5, -5),
            new Vector2(-5, -5),
        };
        private readonly Color4 ruinsVolumeColor = new Color4(Color.Green.RGB(), alpha);
        private readonly Color4 torchVolumeColor = new Color4(Color.GreenYellow.RGB(), alpha);
        private readonly int bsphSlices = 20;
        private readonly int bsphStacks = 10;

        private readonly Agent walker = new Agent()
        {
            Name = "Walker",
            Height = 1.2f,
            Radius = 0.4f,
            MaxClimb = 0.5f,
            MaxSlope = 50,
        };

        private SceneObject<Cursor> cursor;

        private SceneObject<TextDrawer> fps = null;

        private SceneObject<Scenery> ruins = null;
        private SceneObject<PrimitiveListDrawer<Line3D>> volumesDrawer = null;
        private SceneObject<PrimitiveListDrawer<Triangle>> graphDrawer = null;

        private SceneObject<ModelInstanced> torchs = null;

        private SceneObject<Model> movingFire = null;
        private ParticleEmitter movingFireEmitter = null;
        private SceneLightPoint movingFireLight = null;

        private int directionalLightCount = 0;

        public TestScene3D(Game game)
            : base(game, SceneModes.ForwardLigthning)
        {

        }

        public override void Initialize()
        {
            base.Initialize();

            this.InitializeCamera();

            #region Cursor

            var cursorDesc = new CursorDescription()
            {
                Name = "Cursor",
                Textures = new[] { "target.png" },
                Color = Color.Purple,
                Width = 16,
                Height = 16,
            };
            this.cursor = this.AddComponent<Cursor>(cursorDesc, SceneObjectUsages.UI, layerHUD + 1);

            #endregion

            #region Text

            var title = this.AddComponent<TextDrawer>(TextDrawerDescription.Generate("Tahoma", 18, Color.White), SceneObjectUsages.UI, layerHUD);
            var help = this.AddComponent<TextDrawer>(TextDrawerDescription.Generate("Lucida Casual", 12, Color.Yellow), SceneObjectUsages.UI, layerHUD);
            this.fps = this.AddComponent<TextDrawer>(TextDrawerDescription.Generate("Lucida Casual", 12, Color.Yellow), SceneObjectUsages.UI, layerHUD);

            title.Instance.Text = "Collada Scene with Skybox";
#if DEBUG
            help.Instance.Text = "Escape: Exit | Home: Reset camera | AWSD: Move camera | Right Mouse: Drag view | Left Mouse: Pick";
#else
            help.Instance.Text = "Escape: Exit | Home: Reset camera | AWSD: Move camera | Move Mouse: View | Left Mouse: Pick";
#endif
            this.fps.Instance.Text = null;

            title.Instance.Position = Vector2.Zero;
            help.Instance.Position = new Vector2(0, 24);
            this.fps.Instance.Position = new Vector2(0, 40);

            var spDesc = new SpriteDescription()
            {
                Name = "UI Back pannel",
                AlphaEnabled = true,
                Width = this.Game.Form.RenderWidth,
                Height = this.fps.Instance.Top + this.fps.Instance.Height + 3,
                Color = new Color4(0, 0, 0, 0.75f),
            };

            this.AddComponent<Sprite>(spDesc, SceneObjectUsages.UI, layerHUD - 1);

            #endregion

            #region Skydom

            this.AddComponent<Skydom>(new SkydomDescription()
            {
                Name = "Skydom",
                ContentPath = "Resources",
                Radius = this.Camera.FarPlaneDistance,
                Texture = "sunset.dds",
            });

            #endregion

            #region Torchs

            this.torchs = this.AddComponent<ModelInstanced>(
                new ModelInstancedDescription()
                {
                    Name = "Torchs",
                    Instances = this.firePositions.Length,
                    CastShadow = true,
                    Content = new ContentDescription()
                    {
                        ContentFolder = "Resources",
                        ModelContentFilename = "torch.xml",
                    }
                });

            #endregion

            #region Terrain

            GroundDescription desc = new GroundDescription()
            {
                Name = "Terrain",
                CastShadow = true,
                Content = new ContentDescription()
                {
                    ContentFolder = "Resources",
                    ModelContentFilename = "ruins.xml",
                }
            };
            this.ruins = this.AddComponent<Scenery>(desc);

            #endregion

            #region Water

            var waterDesc = WaterDescription.CreateCalm("Ocean", 5000f, -1f);
            this.AddComponent<Water>(waterDesc, SceneObjectUsages.None);

            #endregion

            this.SetGround(this.ruins, true);
            this.AttachToGround(this.torchs, true);

            #region Particle Systems

            var pManager = this.AddComponent<ParticleManager>(new ParticleManagerDescription() { Name = "Particle Systems" });

            var pBigFire = ParticleSystemDescription.InitializeFire("resources", "fire.png", 0.5f);
            var pFire = ParticleSystemDescription.InitializeFire("resources", "fire.png", 0.1f);
            var pPlume = ParticleSystemDescription.InitializeSmokePlume("resources", "smoke.png", 0.1f);

            #endregion

            #region Moving fire

            MaterialContent mat = MaterialContent.Default;
            mat.EmissionColor = Color.Yellow;

            GeometryUtil.CreateSphere(
                0.05f, 32, 32,
                out Vector3[] v, out Vector3[] n, out Vector2[] uv, out uint[] ix);

            VertexData[] vertices = new VertexData[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                vertices[i] = new VertexData()
                {
                    Position = v[i],
                    Normal = n[i],
                    Texture = uv[i],
                };
            }

            var content = ModelContent.GenerateTriangleList(vertices, ix, mat);

            var mFireDesc = new ModelDescription()
            {
                Name = "Emitter",
                Static = false,
                CastShadow = false,
                DeferredEnabled = true,
                DepthEnabled = true,
                AlphaEnabled = false,
                Content = new ContentDescription()
                {
                    ModelContent = content,
                }
            };

            this.movingFire = this.AddComponent<Model>(mFireDesc);

            this.movingFireEmitter = new ParticleEmitter() { EmissionRate = 0.1f, InfiniteDuration = true };

            pManager.Instance.AddParticleSystem(ParticleSystemTypes.CPU, pBigFire, this.movingFireEmitter);

            #endregion

            #region DEBUG drawers

            this.volumesDrawer = this.AddComponent<PrimitiveListDrawer<Line3D>>(new PrimitiveListDrawerDescription<Line3D>() { AlphaEnabled = true, Count = 10000 });
            this.volumesDrawer.Visible = false;

            this.graphDrawer = this.AddComponent<PrimitiveListDrawer<Triangle>>(new PrimitiveListDrawerDescription<Triangle>() { AlphaEnabled = true, Count = 10000 });
            this.graphDrawer.Visible = false;

            #endregion

            #region Positioning and lights

            this.Lights.DirectionalLights[0].Enabled = true;
            this.Lights.DirectionalLights[0].CastShadow = true;

            this.Lights.DirectionalLights[1].Enabled = true;

            this.Lights.DirectionalLights[2].Enabled = false;

            this.directionalLightCount = this.Lights.DirectionalLights.Length;

            this.movingFireLight = new SceneLightPoint(
                "Moving fire light",
                false,
                Color.Orange,
                Color.Orange,
                true,
                SceneLightPointDescription.Create(Vector3.Zero, 15f, 20f));

            this.Lights.Add(this.movingFireLight);

            Vector3[] firePositions3D = new Vector3[this.firePositions.Length];
            SceneLightPoint[] torchLights = new SceneLightPoint[this.firePositions.Length];
            for (int i = 0; i < this.firePositions.Length; i++)
            {
                Color color = Color.Yellow;
                if (i == 1) color = Color.Red;
                if (i == 2) color = Color.Green;
                if (i == 3) color = Color.LightBlue;

                this.FindTopGroundPosition(
                    this.firePositions[i].X, this.firePositions[i].Y,
                    out PickingResult<Triangle> result);
                firePositions3D[i] = result.Position;

                this.torchs.Instance[i].Manipulator.SetScale(0.20f, true);
                this.torchs.Instance[i].Manipulator.SetPosition(firePositions3D[i], true);

                BoundingBox bbox = this.torchs.Instance[i].GetBoundingBox();

                firePositions3D[i].Y += (bbox.Maximum.Y - bbox.Minimum.Y) * 0.95f;

                torchLights[i] = new SceneLightPoint(
                    string.Format("Torch {0}", i),
                    false,
                    color,
                    color,
                    true,
                    SceneLightPointDescription.Create(firePositions3D[i], 4f, 20f));

                this.Lights.Add(torchLights[i]);

                pManager.Instance.AddParticleSystem(ParticleSystemTypes.CPU, pFire, new ParticleEmitter() { Position = firePositions3D[i], InfiniteDuration = true, EmissionRate = 0.1f });
                pManager.Instance.AddParticleSystem(ParticleSystemTypes.CPU, pPlume, new ParticleEmitter() { Position = firePositions3D[i], InfiniteDuration = true, EmissionRate = 0.5f });
            }

            #endregion

            #region Navigation Mesh

            var nvInput = new InputGeometry(GetTrianglesForNavigationGraph);

            var nvSettings = BuildSettings.Default;
            nvSettings.TileSize = 32;
            nvSettings.CellSize = 0.05f;
            nvSettings.CellHeight = 0.2f;
            nvSettings.PartitionType = SamplePartitionTypes.Monotone;
            nvSettings.Agents[0] = this.walker;

            this.PathFinderDescription = new Engine.PathFinding.PathFinderDescription(nvSettings, nvInput);

            #endregion
        }
        private void InitializeCamera()
        {
            this.Camera.NearPlaneDistance = 0.1f;
            this.Camera.FarPlaneDistance = 5000.0f;
            this.Camera.Goto(new Vector3(-6, this.walker.Height, 5));
            this.Camera.LookTo(Vector3.UnitY + Vector3.UnitZ);
            this.Camera.MovementDelta = 4f;
            this.Camera.SlowMovementDelta = 2f;
        }

        public override void Update(GameTime gameTime)
        {
            Vector3 previousPosition = this.Camera.Position;
            bool shift = this.Game.Input.KeyPressed(Keys.LShiftKey);

            this.UpdateInput(shift);

            #region Walk

            {
                if (this.Walk(this.walker, previousPosition, this.Camera.Position, out Vector3 walkerPosition))
                {
                    this.Camera.Goto(walkerPosition);
                }
                else
                {
                    this.Camera.Goto(previousPosition);
                }
            }

            #endregion

            #region Light

            float r = 5.5f;
            float h = 1.25f;
            float d = 0.5f;
            float v = 0.8f;

            Vector3 position = Vector3.Zero;
            position.X = r * d * (float)Math.Cos(v * this.Game.GameTime.TotalSeconds);
            position.Y = h + (0.25f * (1f + (float)Math.Sin(this.Game.GameTime.TotalSeconds)));
            position.Z = r * d * (float)Math.Sin(v * this.Game.GameTime.TotalSeconds);

            this.movingFire.Transform.SetPosition(position);
            this.movingFireEmitter.Position = position;
            this.movingFireLight.Position = position;

            this.DEBUGUpdateMovingVolumesDrawer();

            #endregion

            base.Update(gameTime);
        }

        private void UpdateInput(bool shift)
        {
            if (this.Game.Input.KeyJustReleased(Keys.Escape))
            {
                this.Game.Exit();
            }

            if (this.Game.Input.KeyJustReleased(Keys.R))
            {
                this.SetRenderMode(this.GetRenderMode() == SceneModes.ForwardLigthning ?
                    SceneModes.DeferredLightning :
                    SceneModes.ForwardLigthning);
            }

            this.UpdateInputDebug();

            this.UpdateInputCamera(shift);

            this.UpdateInputLights();

#if DEBUG
            this.fps.Instance.Text = string.Format(
                "Mouse (X:{0}; Y:{1}, Wheel: {2}) Absolute (X:{3}; Y:{4}) Cursor ({5})",
                this.Game.Input.MouseXDelta,
                this.Game.Input.MouseYDelta,
                this.Game.Input.MouseWheelDelta,
                this.Game.Input.MouseX,
                this.Game.Input.MouseY,
                this.cursor.Instance.CursorPosition);
#else
            this.fps.Instance.Text = this.Game.RuntimeText;
#endif
        }
        private void UpdateInputCamera(bool shift)
        {
            if (this.Game.Input.LeftMouseButtonPressed)
            {
                var pRay = this.GetPickingRay();

                if (this.ruins.Instance.PickNearest(pRay, out PickingResult<Triangle> r))
                {
                    var tri = Line3D.CreateWiredTriangle(r.Item);

                    this.volumesDrawer.Instance.SetPrimitives(Color.White, tri);
                }
            }

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
                this.Camera.MoveLeft(this.Game.GameTime, shift);
            }

            if (this.Game.Input.KeyPressed(Keys.D))
            {
                this.Camera.MoveRight(this.Game.GameTime, shift);
            }

            if (this.Game.Input.KeyPressed(Keys.W))
            {
                this.Camera.MoveForward(this.Game.GameTime, shift);
            }

            if (this.Game.Input.KeyPressed(Keys.S))
            {
                this.Camera.MoveBackward(this.Game.GameTime, shift);
            }
        }
        private void UpdateInputLights()
        {
            if (this.Game.Input.KeyJustReleased(Keys.Add))
            {
                this.directionalLightCount++;
                if (this.directionalLightCount > 3)
                {
                    this.directionalLightCount = 0;
                }

                this.UpdateInputEnabledLights();
            }

            if (this.Game.Input.KeyJustReleased(Keys.Subtract))
            {
                this.directionalLightCount--;
                if (this.directionalLightCount < 0)
                {
                    this.directionalLightCount = 3;
                }

                this.UpdateInputEnabledLights();
            }
        }
        private void UpdateInputEnabledLights()
        {
            this.Lights.DirectionalLights[0].Enabled = this.directionalLightCount > 0;
            this.Lights.DirectionalLights[1].Enabled = this.directionalLightCount > 1;
            this.Lights.DirectionalLights[2].Enabled = this.directionalLightCount > 2;
        }
        private void UpdateInputDebug()
        {
            if (this.Game.Input.KeyJustReleased(Keys.Home))
            {
                this.InitializeCamera();
            }

            if (this.Game.Input.KeyJustReleased(Keys.F1))
            {
                this.volumesDrawer.Visible = !this.volumesDrawer.Visible;

                if (this.volumesDrawer.Visible)
                {
                    this.DEBUGUpdateVolumesDrawer();
                }
            }

            if (this.Game.Input.KeyJustReleased(Keys.F2))
            {
                this.graphDrawer.Visible = !this.graphDrawer.Visible;

                if (this.graphDrawer.Visible)
                {
                    this.DEBUGUpdateGraphDrawer();
                }
            }
        }

        private void DEBUGUpdateVolumesDrawer()
        {
            this.volumesDrawer.Instance.SetPrimitives(this.ruinsVolumeColor, Line3D.CreateWiredBox(this.ruins.Instance.GetBoundingBox()));

            List<Line3D> volumesTorchs = new List<Line3D>();
            for (int i = 0; i < this.torchs.Count; i++)
            {
                volumesTorchs.AddRange(Line3D.CreateWiredBox(this.torchs.Instance[i].GetBoundingBox()));
            }
            this.volumesDrawer.Instance.SetPrimitives(this.torchVolumeColor, volumesTorchs.ToArray());

            for (int i = 1; i < this.Lights.PointLights.Length; i++)
            {
                var light = this.Lights.PointLights[i];

                this.volumesDrawer.Instance.SetPrimitives(
                    new Color4(light.DiffuseColor.RGB(), alpha),
                    Line3D.CreateWiredSphere(light.BoundingSphere, this.bsphSlices, this.bsphStacks));
            }
        }
        private void DEBUGUpdateMovingVolumesDrawer()
        {
            var light = this.Lights.PointLights[0];

            this.volumesDrawer.Instance.SetPrimitives(
                new Color4(light.DiffuseColor.RGB(), alpha),
                Line3D.CreateWiredSphere(light.BoundingSphere, this.bsphSlices, this.bsphStacks));
        }
        private void DEBUGUpdateGraphDrawer()
        {
            var nodes = this.GetNodes(this.walker).OfType<GraphNode>();
            if (nodes.Any())
            {
                this.graphDrawer.Instance.Clear();

                foreach (var node in nodes)
                {
                    this.graphDrawer.Instance.AddPrimitives(node.Color, node.Triangles);
                }
            }
        }
    }
}