﻿using Engine;
using Engine.Content.FmtCollada;
using Engine.Content.FmtObj;
using System;
using System.IO;

namespace Skybox
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
#if DEBUG
                using (Game cl = new Game("5 Skybox", false, 1600, 900, true, 0, 0))
#else
                using (Game cl = new Game("5 Skybox", true, 0, 0, true, 0, 4))
#endif
                {
#if DEBUG
                    cl.VisibleMouse = false;
                    cl.LockMouse = false;
#else
                    cl.VisibleMouse = false;
                    cl.LockMouse = true;
#endif

                    GameResourceManager.RegisterLoader<LoaderCollada>();
                    GameResourceManager.RegisterLoader<LoaderObj>();

                    cl.SetScene<TestScene3D>();

                    cl.Run();
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText("dump.txt", ex.ToString());
            }
        }
    }
}
