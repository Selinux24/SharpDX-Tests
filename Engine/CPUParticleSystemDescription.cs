﻿using SharpDX;
using System;

namespace Engine
{
    public class CPUParticleSystemDescription
    {
        public static CPUParticleSystemDescription InitializeDust(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 1000;

            settings.MaxDuration = 1;

            settings.MinHorizontalVelocity = 0;
            settings.MaxHorizontalVelocity = 2;

            settings.MinVerticalVelocity = 0;
            settings.MaxVerticalVelocity = 2;

            settings.Gravity = new Vector3(-0.15f, -0.15f, 0);

            settings.EndVelocity = 0.1f;

            settings.MinColor = Color.SandyBrown;
            settings.MaxColor = Color.SandyBrown;

            settings.MinRotateSpeed = -1;
            settings.MaxRotateSpeed = 1;

            settings.MinStartSize = 1;
            settings.MaxStartSize = 2;

            settings.MinEndSize = 5;
            settings.MaxEndSize = 10;

            return settings;
        }
        public static CPUParticleSystemDescription InitializeExplosion(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 1000;

            settings.MaxDuration = 2;
            settings.MaxDurationRandomness = 1;

            settings.MinHorizontalVelocity = 20;
            settings.MaxHorizontalVelocity = 30;

            settings.MinVerticalVelocity = -20;
            settings.MaxVerticalVelocity = 20;

            settings.EndVelocity = 0;

            settings.MinColor = Color.DarkGray;
            settings.MaxColor = Color.Gray;

            settings.MinRotateSpeed = -1;
            settings.MaxRotateSpeed = 1;

            settings.MinStartSize = 10;
            settings.MaxStartSize = 10;

            settings.MinEndSize = 100;
            settings.MaxEndSize = 200;

            settings.Transparent = true;

            return settings;
        }
        public static CPUParticleSystemDescription InitializeExplosionSmoke(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 1000;

            settings.MaxDuration = 4;

            settings.MinHorizontalVelocity = 0;
            settings.MaxHorizontalVelocity = 50;

            settings.MinVerticalVelocity = -10;
            settings.MaxVerticalVelocity = 50;

            settings.Gravity = new Vector3(0, -20, 0);

            settings.EndVelocity = 0;

            settings.MinColor = Color.LightGray;
            settings.MaxColor = Color.White;

            settings.MinRotateSpeed = -2;
            settings.MaxRotateSpeed = 2;

            settings.MinStartSize = 10;
            settings.MaxStartSize = 10;

            settings.MinEndSize = 100;
            settings.MaxEndSize = 200;

            return settings;
        }
        public static CPUParticleSystemDescription InitializeFire(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 500;

            settings.MaxDuration = 2;
            settings.MaxDurationRandomness = 1;

            settings.MinHorizontalVelocity = 0;
            settings.MaxHorizontalVelocity = 15;

            settings.MinVerticalVelocity = -10;
            settings.MaxVerticalVelocity = 10;

            settings.Gravity = new Vector3(0, 15, 0);

            settings.MinColor = new Color(255, 255, 255, 10);
            settings.MaxColor = new Color(255, 255, 255, 40);

            settings.MinStartSize = 5;
            settings.MaxStartSize = 10;

            settings.MinEndSize = 10;
            settings.MaxEndSize = 40;

            settings.Transparent = true;

            return settings;
        }
        public static CPUParticleSystemDescription InitializePlasmaEngine(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 500;

            settings.MaxDuration = 0.5f;
            settings.MaxDurationRandomness = 0f;

            settings.MinHorizontalVelocity = 0;
            settings.MaxHorizontalVelocity = 0;

            settings.MinVerticalVelocity = 0;
            settings.MaxVerticalVelocity = 0;

            settings.Gravity = new Vector3(0, 0, 0);

            settings.MinColor = Color.AliceBlue;
            settings.MaxColor = Color.LightBlue;

            settings.MinStartSize = 1f;
            settings.MaxStartSize = 1f;

            settings.MinEndSize = 0.1f;
            settings.MaxEndSize = 0.1f;

            settings.Transparent = true;

            return settings;
        }
        public static CPUParticleSystemDescription InitializeProjectileTrail(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 250;

            settings.MaxDuration = 0.5f;
            settings.MaxDurationRandomness = 1.5f;

            settings.EmitterVelocitySensitivity = 0.1f;

            settings.MinHorizontalVelocity = -1;
            settings.MaxHorizontalVelocity = 1;

            settings.MinVerticalVelocity = -1;
            settings.MaxVerticalVelocity = 1;

            settings.MinColor = Color.Gray;
            settings.MaxColor = Color.White;

            settings.MinRotateSpeed = 1;
            settings.MaxRotateSpeed = 1;

            settings.MinStartSize = 0.5f;
            settings.MaxStartSize = 1f;

            settings.MinEndSize = 1f;
            settings.MaxEndSize = 2f;

            return settings;
        }
        public static CPUParticleSystemDescription InitializeSmokeEngine(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 1000;

            settings.MaxDuration = 1;

            settings.MinHorizontalVelocity = 0;
            settings.MaxHorizontalVelocity = 2;

            settings.MinVerticalVelocity = 0;
            settings.MaxVerticalVelocity = 2;

            settings.Gravity = new Vector3(-1, -1, 0);

            settings.EndVelocity = 0.15f;

            settings.MinRotateSpeed = -1;
            settings.MaxRotateSpeed = 1;

            settings.MinStartSize = 1;
            settings.MaxStartSize = 2;

            settings.MinEndSize = 2;
            settings.MaxEndSize = 4;

            return settings;
        }
        public static CPUParticleSystemDescription InitializeSmokePlume(string contentPath, string texture)
        {
            CPUParticleSystemDescription settings = new CPUParticleSystemDescription();

            settings.ContentPath = contentPath;
            settings.TextureName = texture;

            settings.MaxParticles = 5000;

            settings.MaxDuration = 10;

            settings.MinHorizontalVelocity = 0;
            settings.MaxHorizontalVelocity = 0.5f;

            settings.MinVerticalVelocity = 1.0f;
            settings.MaxVerticalVelocity = 2.0f;

            settings.Gravity = new Vector3(0.0f, +0.5f, 0.0f);

            settings.EndVelocity = 0.75f;

            settings.MinRotateSpeed = -1f;
            settings.MaxRotateSpeed = 1f;

            settings.MinStartSize = 0.5f;
            settings.MaxStartSize = 1.0f;

            settings.MinEndSize = 5.0f;
            settings.MaxEndSize = 20.0f;

            return settings;
        }

        public CPUParticleSystemTypes ParticleType { get; set; }

        public int MaxParticles { get; set; }

        public string ContentPath { get; set; }
        public string TextureName { get; set; }

        public float MaxDuration { get; set; }
        public float MaxDurationRandomness { get; set; }

        public float MaxHorizontalVelocity { get; set; }
        public float MinHorizontalVelocity { get; set; }

        public float MaxVerticalVelocity { get; set; }
        public float MinVerticalVelocity { get; set; }

        public Vector3 Gravity { get; set; }

        public float EndVelocity { get; set; }

        public Color MinColor { get; set; }
        public Color MaxColor { get; set; }

        public float MinRotateSpeed { get; set; }
        public float MaxRotateSpeed { get; set; }

        public float MinStartSize { get; set; }
        public float MaxStartSize { get; set; }

        public float MinEndSize { get; set; }
        public float MaxEndSize { get; set; }

        public bool Transparent { get; set; }

        public float EmitterVelocitySensitivity { get; set; }

        public CPUParticleSystemDescription()
        {
            this.ParticleType = CPUParticleSystemTypes.None;
            this.MaxParticles = 0;
            this.ContentPath = "Resources";
            this.TextureName = null;
            this.MaxDuration = 0;
            this.MaxDurationRandomness = 1;
            this.MaxHorizontalVelocity = 0;
            this.MinHorizontalVelocity = 0;
            this.MaxVerticalVelocity = 0;
            this.MinVerticalVelocity = 0;
            this.Gravity = new Vector3(0, -1, 0);
            this.EndVelocity = 1;
            this.MinColor = new Color(1f, 1f, 1f, 1f);
            this.MaxColor = new Color(1f, 1f, 1f, 1f);
            this.MinRotateSpeed = 0;
            this.MaxRotateSpeed = 0;
            this.MinStartSize = 1;
            this.MaxStartSize = 1;
            this.MinEndSize = 1;
            this.MaxEndSize = 1;
            this.Transparent = false;
            this.EmitterVelocitySensitivity = 0;
        }
    }
}
