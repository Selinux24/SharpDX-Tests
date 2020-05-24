﻿using SharpDX;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Engine
{
    using Engine.Helpers;

    /// <summary>
    /// Mouse and keyboard input
    /// </summary>
    public class Input : IDisposable
    {
        /// <summary>
        /// Frame time
        /// </summary>
        public const float InputTime = 1f / 60f;

        /// <summary>
        /// Engine render form
        /// </summary>
        private readonly EngineForm form;
        /// <summary>
        /// First state update flag
        /// </summary>
        private bool firstUpdate = true;
        /// <summary>
        /// Mouse over active form flag
        /// </summary>
        private bool mouseIn = true;
        /// <summary>
        /// Mouse visble flag
        /// </summary>
        private bool visibleMouse = true;

        /// <summary>
        /// Mouse position of last update
        /// </summary>
        private Point lastMousePos;
        /// <summary>
        /// Mouse wheel delta of current update
        /// </summary>
        private int mouseWheel;
        /// <summary>
        /// Mouse buttons of last update
        /// </summary>
        private readonly List<MouseButtons> lastMouseButtons = new List<MouseButtons>();
        /// <summary>
        /// Current mouse buttons
        /// </summary>
        private readonly List<MouseButtons> currentMouseButtons = new List<MouseButtons>();
        /// <summary>
        /// Keys of last update
        /// </summary>
        private readonly List<Keys> lastKeyboardKeys = new List<Keys>();
        /// <summary>
        /// Current keys
        /// </summary>
        private readonly List<Keys> currentKeyboardKeys = new List<Keys>();

        /// <summary>
        /// Elapsed time
        /// </summary>
        public float Elapsed { get; set; } = 0f;
        /// <summary>
        /// Mouse X axis value
        /// </summary>
        public int MouseXDelta { get; private set; }
        /// <summary>
        /// Mouse Y axis value
        /// </summary>
        public int MouseYDelta { get; private set; }
        /// <summary>
        /// Mouse wheel value
        /// </summary>
        public int MouseWheelDelta { get; private set; }
        /// <summary>
        /// Absolute Mouse X axis value
        /// </summary>
        public int MouseX { get; private set; }
        /// <summary>
        /// Absolute Mouse Y axis value
        /// </summary>
        public int MouseY { get; private set; }
        /// <summary>
        /// Absolute Mouse position
        /// </summary>
        public Point MousePosition { get; private set; }
        /// <summary>
        /// Gets if left mouse button is just released
        /// </summary>
        public bool LeftMouseButtonJustReleased { get; private set; }
        /// <summary>
        /// Gets if left mouse button is just pressed
        /// </summary>
        public bool LeftMouseButtonJustPressed { get; private set; }
        /// <summary>
        /// Gets if left mouse button is pressed now
        /// </summary>
        public bool LeftMouseButtonPressed { get; private set; }
        /// <summary>
        /// Gets if right mouse button is just released
        /// </summary>
        public bool RightMouseButtonJustReleased { get; private set; }
        /// <summary>
        /// Gets if right mouse button is just pressed
        /// </summary>
        public bool RightMouseButtonJustPressed { get; private set; }
        /// <summary>
        /// Gets if right mouse button is pressed now
        /// </summary>
        public bool RightMouseButtonPressed { get; private set; }
        /// <summary>
        /// Gets if middle mouse button is just released
        /// </summary>
        public bool MiddleMouseButtonJustReleased { get; private set; }
        /// <summary>
        /// Gets if middle mouse button is just pressed
        /// </summary>
        public bool MiddleMouseButtonJustPressed { get; private set; }
        /// <summary>
        /// Gets if middle mouse button is pressed now
        /// </summary>
        public bool MiddleMouseButtonPressed { get; private set; }
        /// <summary>
        /// Gets if X1 mouse button is just released
        /// </summary>
        public bool X1MouseButtonJustReleased { get; private set; }
        /// <summary>
        /// Gets if X1 mouse button is just pressed
        /// </summary>
        public bool X1MouseButtonJustPressed { get; private set; }
        /// <summary>
        /// Gets if X1 mouse button is pressed now
        /// </summary>
        public bool X1MouseButtonPressed { get; private set; }
        /// <summary>
        /// Gets if X2 mouse button is just released
        /// </summary>
        public bool X2MouseButtonJustReleased { get; private set; }
        /// <summary>
        /// Gets if X2 mouse button is just pressed
        /// </summary>
        public bool X2MouseButtonJustPressed { get; private set; }
        /// <summary>
        /// Gets if X2 mouse button is pressed now
        /// </summary>
        public bool X2MouseButtonPressed { get; private set; }
        /// <summary>
        /// Gets if left or right shift key were pressed now
        /// </summary>
        public bool ShiftPressed
        {
            get
            {
                return this.KeyPressed(Keys.LShiftKey) || this.KeyPressed(Keys.RShiftKey);
            }
        }
        /// <summary>
        /// Gets if left or right control key were pressed now
        /// </summary>
        public bool ControlPressed
        {
            get
            {
                return this.KeyPressed(Keys.LControlKey) || this.KeyPressed(Keys.RControlKey);
            }
        }
        /// <summary>
        /// Sets mouse on center after update
        /// </summary>
        public bool LockMouse { get; set; }
        /// <summary>
        /// Sets cursor visible
        /// </summary>
        public bool VisibleMouse
        {
            get
            {
                return this.visibleMouse;
            }
            set
            {
                this.visibleMouse = value;

                if (this.visibleMouse)
                {
                    Cursor.Show();
                }
                else
                {
                    Cursor.Hide();
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public Input(EngineForm form)
        {
            this.form = form;
            this.form.MouseWheel += new MouseEventHandler(OnMouseWheel);
            this.form.MouseLeave += new EventHandler(OnMouseLeave);
            this.form.MouseEnter += new EventHandler(OnMouseEnter);
        }
        /// <summary>
        /// Destructor
        /// </summary>
        ~Input()
        {
            // Finalizer calls Dispose(false)  
            Dispose(false);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose resources
        /// </summary>
        /// <param name="disposing">Free managed resources</param>
        protected virtual void Dispose(bool disposing)
        {

        }

        /// <summary>
        /// Updates input state
        /// </summary>
        /// <param name="gameTime">Game time</param>
        public void Update(GameTime gameTime)
        {
            this.Elapsed += gameTime.ElapsedSeconds;

            if (this.Elapsed >= InputTime)
            {
                this.Elapsed -= InputTime;

                if (this.mouseIn)
                {
                    if (this.firstUpdate)
                    {
                        this.SetMousePosition(this.form.RenderCenter);

                        this.ClearInputData();

                        this.firstUpdate = false;
                    }
                    else
                    {
                        this.UpdateMousePositionState();

                        this.UpdateMouseButtonsState();

                        this.UpdateKeyboardState();
                    }
                }
                else
                {
                    this.ClearInputData();
                }
            }
        }
        /// <summary>
        /// Updates the keyboard state
        /// </summary>
        private void UpdateKeyboardState()
        {
            this.lastKeyboardKeys.Clear();
            this.lastKeyboardKeys.AddRange(this.currentKeyboardKeys);
            this.currentKeyboardKeys.Clear();

            Keys[] keyboard = this.GetPressedKeys();
            if (keyboard.Length > 0)
            {
                this.currentKeyboardKeys.AddRange(keyboard);
            }
        }
        /// <summary>
        /// Updates the mouse position state
        /// </summary>
        private void UpdateMousePositionState()
        {
            var mousePos = this.form.PointToClient(Cursor.ScreenPosition);

            this.MousePosition = new Point(mousePos.X, mousePos.Y);

            this.MouseXDelta = this.MousePosition.X - this.lastMousePos.X;
            this.MouseYDelta = this.MousePosition.Y - this.lastMousePos.Y;

            this.MouseX = this.MousePosition.X;
            this.MouseY = this.MousePosition.Y;

            this.lastMousePos = this.MousePosition;

            if (this.LockMouse)
            {
                this.SetMousePosition(this.form.RenderCenter);
            }
        }
        /// <summary>
        /// Updates the mouse buttons state
        /// </summary>
        private void UpdateMouseButtonsState()
        {
            this.lastMouseButtons.Clear();
            this.lastMouseButtons.AddRange(this.currentMouseButtons);
            this.currentMouseButtons.Clear();

            MouseButtons[] buttons = this.GetPressedButtons();
            if (buttons.Length > 0)
            {
                this.currentMouseButtons.AddRange(buttons);
            }

            bool prev;
            bool curr;

            prev = this.lastMouseButtons.Contains(MouseButtons.Left);
            curr = this.currentMouseButtons.Contains(MouseButtons.Left);
            this.LeftMouseButtonPressed = curr;
            this.LeftMouseButtonJustPressed = curr && !prev;
            this.LeftMouseButtonJustReleased = !curr && prev;

            prev = this.lastMouseButtons.Contains(MouseButtons.Right);
            curr = this.currentMouseButtons.Contains(MouseButtons.Right);
            this.RightMouseButtonPressed = curr;
            this.RightMouseButtonJustPressed = curr && !prev;
            this.RightMouseButtonJustReleased = !curr && prev;

            prev = this.lastMouseButtons.Contains(MouseButtons.Middle);
            curr = this.currentMouseButtons.Contains(MouseButtons.Middle);
            this.MiddleMouseButtonPressed = curr;
            this.MiddleMouseButtonJustPressed = curr && !prev;
            this.MiddleMouseButtonJustReleased = !curr && prev;

            prev = this.lastMouseButtons.Contains(MouseButtons.XButton1);
            curr = this.currentMouseButtons.Contains(MouseButtons.XButton1);
            this.X1MouseButtonPressed = curr;
            this.X1MouseButtonJustPressed = curr && !prev;
            this.X1MouseButtonJustReleased = !curr && prev;

            prev = this.lastMouseButtons.Contains(MouseButtons.XButton2);
            curr = this.currentMouseButtons.Contains(MouseButtons.XButton2);
            this.X2MouseButtonPressed = curr;
            this.X2MouseButtonJustPressed = curr && !prev;
            this.X2MouseButtonJustReleased = !curr && prev;

            this.MouseWheelDelta = this.mouseWheel;
            this.mouseWheel = 0;
        }

        /// <summary>
        /// Gets if specified key is just released
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Returns true if the specified key is just released</returns>
        public bool KeyJustReleased(Keys key)
        {
            return this.lastKeyboardKeys.Contains(key) && !this.currentKeyboardKeys.Contains(key);
        }
        /// <summary>
        /// Gets if specified key is just pressed
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Returns true if the specified key is just pressed</returns>
        public bool KeyJustPressed(Keys key)
        {
            return !this.lastKeyboardKeys.Contains(key) && this.currentKeyboardKeys.Contains(key);
        }
        /// <summary>
        /// Gets if specified key is pressed now
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Returns true if the specified key is pressed now</returns>
        public bool KeyPressed(Keys key)
        {
            return this.currentKeyboardKeys.Contains(key);
        }
        /// <summary>
        /// Sets mouse position
        /// </summary>
        /// <param name="x">X component</param>
        /// <param name="y">Y component</param>
        public void SetMousePosition(int x, int y)
        {
            this.SetMousePosition(new Point(x, y));
        }
        /// <summary>
        /// Sets mouse position
        /// </summary>
        /// <param name="location">Position</param>
        public void SetMousePosition(Point location)
        {
            Cursor.ScreenPosition = new System.Drawing.Point(location.X, location.Y);

            var mousePos = this.form.PointToClient(Cursor.ScreenPosition);
            this.lastMousePos = new Point(mousePos.X, mousePos.Y);
        }

        /// <summary>
        /// Clear input data variables
        /// </summary>
        private void ClearInputData()
        {
            #region Mouse position

            this.MouseXDelta = 0;
            this.MouseYDelta = 0;

            this.MouseX = this.lastMousePos.X;
            this.MouseY = this.lastMousePos.Y;

            #endregion

            #region Mouse buttons

            this.LeftMouseButtonPressed = false;
            this.LeftMouseButtonJustPressed = false;
            this.LeftMouseButtonJustReleased = false;

            this.RightMouseButtonPressed = false;
            this.RightMouseButtonJustPressed = false;
            this.RightMouseButtonJustReleased = false;

            this.MiddleMouseButtonPressed = false;
            this.MiddleMouseButtonJustPressed = false;
            this.MiddleMouseButtonJustReleased = false;

            this.X1MouseButtonPressed = false;
            this.X1MouseButtonJustPressed = false;
            this.X1MouseButtonJustReleased = false;

            this.X2MouseButtonPressed = false;
            this.X2MouseButtonJustPressed = false;
            this.X2MouseButtonJustReleased = false;

            this.lastMouseButtons.Clear();
            this.currentMouseButtons.Clear();

            #endregion

            #region Mouse Wheel

            this.MouseWheelDelta = 0;
            this.mouseWheel = 0;

            #endregion

            #region Keyboard keys

            this.lastKeyboardKeys.Clear();
            this.currentKeyboardKeys.Clear();

            #endregion
        }
        /// <summary>
        /// Get mouse pressed buttons
        /// </summary>
        /// <returns>Return pressed buttons collection</returns>
        private MouseButtons[] GetPressedButtons()
        {
            List<MouseButtons> res = new List<MouseButtons>();

            if (((int)Control.MouseButtons & (int)MouseButtons.Left) == (int)MouseButtons.Left) res.Add(MouseButtons.Left);
            if (((int)Control.MouseButtons & (int)MouseButtons.Right) == (int)MouseButtons.Right) res.Add(MouseButtons.Right);
            if (((int)Control.MouseButtons & (int)MouseButtons.Middle) == (int)MouseButtons.Middle) res.Add(MouseButtons.Middle);
            if (((int)Control.MouseButtons & (int)MouseButtons.XButton1) == (int)MouseButtons.XButton1) res.Add(MouseButtons.XButton1);
            if (((int)Control.MouseButtons & (int)MouseButtons.XButton2) == (int)MouseButtons.XButton2) res.Add(MouseButtons.XButton2);

            return res.ToArray();
        }
        /// <summary>
        /// Get keyboard pressed keys
        /// </summary>
        /// <returns>Return pressed keys collection</returns>
        private Keys[] GetPressedKeys()
        {
            List<Keys> pressedKeys = new List<Keys>();

            byte[] array = new byte[256];
            if (NativeMethods.GetKeyboardState(array))
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if ((array[i] & 0x80) != 0)
                    {
                        //Pressed
                        Keys key = (Keys)i;

                        pressedKeys.Add(key);
                    }
                }
            }

            return pressedKeys.ToArray();
        }

        /// <summary>
        /// When mouse enters form
        /// </summary>
        /// <param name="sender">Form</param>
        /// <param name="e">Event arguments</param>
        private void OnMouseEnter(object sender, EventArgs e)
        {
            this.mouseIn = true;
        }
        /// <summary>
        /// When mouse leaves form
        /// </summary>
        /// <param name="sender">Form</param>
        /// <param name="e">Event arguments</param>
        private void OnMouseLeave(object sender, EventArgs e)
        {
            this.mouseIn = false;
        }
        /// <summary>
        /// When mouse wheel moves
        /// </summary>
        /// <param name="sender">Form</param>
        /// <param name="e">Event arguments</param>
        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            this.mouseWheel = e.Delta;
        }
    }
}
