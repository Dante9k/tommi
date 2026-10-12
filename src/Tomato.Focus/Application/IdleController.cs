using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace Tomato
{
    public sealed class IdleController : IDisposable
    {
        readonly AppController controller;
        readonly BitmapSource art;
        readonly IdleDeadline deadline = new IdleDeadline();
        Rect area, editor;
        public HangingWindow Window { get; private set; }

        public bool IsHanging
        {
            get
            {
                return Window != null;
            }
        }

        public IdleController(AppController controller, BitmapSource art)
        {
            this.controller = controller;
            this.art = art;
            Touch();
            controller.Window.PreviewMouseDown += delegate
            {
                Touch();
            };
            controller.Window.PreviewMouseMove += delegate
            {
                Touch();
            };
            controller.Window.PreviewMouseWheel += delegate
            {
                Touch();
            };
            controller.Window.PreviewKeyDown += delegate
            {
                Touch();
            };
        }

        public void Touch()
        {
            deadline.Touch(HangingWindow.Now);
        }

        public void Check()
        {
            bool eligible = controller.Phase == TimerPhase.Editing && !controller.IsThrowing && controller.Settings == null && controller.Window.IsVisible && !IsHanging && System.Windows.Input.Mouse.Captured == null;
            if (deadline.Due(HangingWindow.Now, eligible))
                Park(true);
        }

        public void Park(bool animate)
        {
            if (IsHanging || controller.Phase != TimerPhase.Editing || controller.IsThrowing || controller.Settings != null)
                return;
            controller.Window.Settle();
            controller.Window.FinishForIdle();
            editor = new Rect(controller.Window.Left, controller.Window.Top, controller.Window.Width, controller.Window.Height);
            area = WorkArea();
            Rect home = new Rect(area.Right - 112, area.Top + 54, 76, 76);
            var motion = new HangingMotion(home, new Point(area.Right + 4, area.Top + 16));
            BitmapSource snapshot = controller.Window.IdleSnapshot();
            // A slow audio driver may take up to 200ms to close. Finish that work before
            // starting the branch timeline, so its first frames are never skipped.
            controller.StopWheelFeedback();
            Window = new HangingWindow(motion, art, snapshot, Detach, Complete, controller.OpenSettings);
            bool moving = animate && SystemParameters.ClientAreaAnimation;
            if (moving)
                motion.Return(editor, HangingWindow.Now);
            Rect bounds = new Rect(home.Left - 64, area.Top - 4, 220, 310);
            if (moving)
            {
                bounds.Union(editor);
                bounds.Union(new Point(editor.Right + 76, editor.Top - 105));
                bounds.Inflate(18, 18);
            }

            Window.SetBounds(bounds);
            Window.Show();
            controller.Window.Hide();
            Window.Animate();
        }

        public void Detach()
        {
            if (Window == null || Window.Motion.Phase == HangingPhase.Detaching)
                return;
            var motion = Window.Motion;
            // Expand around the stem, then drop slightly under gravity; clamp only the destination.
            editor = new Rect(motion.Stem.X - 110, motion.Stem.Y - 220 * .22 + 32, 220, 220);
            editor.X = Math.Max(area.Left + 8, Math.Min(editor.X, area.Right - editor.Width - 8));
            editor.Y = Math.Max(area.Top + 8, Math.Min(editor.Y, area.Bottom - editor.Height - 8));
            var bounds = new Rect(Window.Left, Window.Top, Window.Width, Window.Height);
            bounds.Union(editor);
            bounds.Inflate(18, 18);
            Window.SetBounds(bounds);
            motion.Detach(editor, HangingWindow.Now);
            if (!SystemParameters.ClientAreaAnimation)
            {
                motion.Advance(HangingWindow.Now + HangingMotion.DetachDuration);
                Complete();
            }
            else
                Window.Animate();
        }

        void Complete()
        {
            if (Window == null)
                return;
            controller.Window.Left = editor.Left;
            controller.Window.Top = editor.Top;
            controller.Window.Show();
            DisposeWindow();
            controller.ClampWindow();
            controller.Window.Activate();
            Touch();
            controller.PrepareWheelFeedback();
            controller.Save();
        }

        // Explicit commands (start, preset, preview) restore the editor before changing timer state.
        public void Reveal()
        {
            if (Window != null)
            {
                controller.Window.Show();
                DisposeWindow();
            }

            Touch();
        }

        public void DisplayChanged()
        {
            if (Window == null)
                return;
            Reveal();
            controller.ClampWindow();
            Park(false);
        }

        Rect WorkArea()
        {
            var visual = PresentationSource.FromVisual(controller.Window);
            var transform = visual.CompositionTarget.TransformFromDevice;
            var screen = Forms.Screen.FromHandle(new WindowInteropHelper(controller.Window).Handle);
            var bounds = screen.WorkingArea;
            return new Rect(transform.Transform(new Point(bounds.Left, bounds.Top)), transform.Transform(new Point(bounds.Right, bounds.Bottom)));
        }

        void DisposeWindow()
        {
            var old = Window;
            Window = null;
            if (old != null)
                old.CloseForOwner();
        }

        public void Dispose()
        {
            DisposeWindow();
        }
    }
}
