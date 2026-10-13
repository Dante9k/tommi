using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Tomato;

namespace Tomato.Tests
{
    // Opt-in local desktop test: exercises the actual WPF capture and Win32 alpha hit-testing path.
    internal static class HangingInputVerification
    {
        [StructLayout(LayoutKind.Sequential)]
        struct PixelPoint
        {
            public int X, Y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out PixelPoint point);
        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
        [DllImport("user32.dll")]
        static extern IntPtr WindowFromPoint(PixelPoint point);
        static void Require(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        public static void Smoke(AppController c)
        {
            PixelPoint original;
            GetCursorPos(out original);
            PixelPoint start = new PixelPoint();
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(60)
            };
            var watch = Stopwatch.StartNew();
            int stage = 0, moves = 0;
            double since = 0, scale = 1;
            bool down = false;
            timer.Tick += delegate
            {
                try
                {
                    if (watch.Elapsed.TotalSeconds - since < (stage == 5 || stage == 8 || stage == 12 ? 1.1 : .08))
                        return;
                    if (stage == 0)
                        c.Idle.Park(false);
                    else if (stage == 1)
                    {
                        var w = c.Idle.Window;
                        var f = w.Motion.Fruit;
                        var center = w.PointToScreen(new Point(f.X - w.Left + f.Width * .5, f.Y - w.Top + f.Height * .65));
                        start.X = (int)center.X;
                        start.Y = (int)center.Y;
                        IntPtr handle = new WindowInteropHelper(w).Handle;
                        Require(WindowFromPoint(start) == handle, "Fruit must receive actual mouse input");
                        var empty = w.PointToScreen(new Point(12, w.Height - 14));
                        Require(WindowFromPoint(new PixelPoint { X = (int)empty.X, Y = (int)empty.Y }) != handle, "Transparent empty space must pass through to the desktop");
                        scale = PresentationSource.FromVisual(w).CompositionTarget.TransformToDevice.M22;
                        SetCursorPos(start.X, start.Y);
                        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                        down = true;
                    }
                    else if (stage == 2)
                    {
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                        down = false;
                    }
                    else if (stage == 3)
                    {
                        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                        down = true;
                    }
                    else if (stage == 4)
                    {
                        Require(c.Idle.IsHanging && !c.Window.IsVisible && c.Idle.Window.Motion.Phase == HangingPhase.Pulling, "Double-click must not expand the hanging fruit");
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                        down = false;
                    }
                    else if (stage == 5)
                    {
                        Require(c.Idle.Window.Motion.Phase == HangingPhase.Hanging, "Double-click settles without detaching");
                        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                        down = true;
                    }
                    else if (stage == 6)
                    {
                        Require(c.Idle.Window.Motion.Phase == HangingPhase.Pulling && c.Idle.Window.IsMouseCaptured, "Mouse down must capture the fruit");
                        SetCursorPos(start.X + (int)(20 * scale), start.Y + (int)(45 * scale));
                    }
                    else if (stage == 7)
                    {
                        Require(c.Idle.Window.Motion.Phase == HangingPhase.Pulling && c.Idle.Window.Motion.Fruit.Y > c.Idle.Window.Motion.Root.Y + 45, "Actual drag bends the branch");
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                        down = false;
                    }
                    else if (stage == 8)
                    {
                        Require(c.Idle.Window.Motion.Phase == HangingPhase.Hanging, "Short mouse drag springs back");
                        SetCursorPos(start.X, start.Y);
                        mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                        down = true;
                    }
                    else if (stage == 9)
                    {
                        SetCursorPos(start.X, start.Y + (int)(++moves * 18 * scale));
                        if (moves < 6)
                        {
                            since = watch.Elapsed.TotalSeconds;
                            return;
                        }
                    }
                    else if (stage == 10)
                    {
                        Require(c.Idle.Window != null && c.Idle.Window.Motion.Phase == HangingPhase.Detaching, "Crossing the threshold starts picking automatically; phase=" + (c.Idle.Window == null ? "none" : c.Idle.Window.Motion.Phase.ToString()) + ", scale=" + scale + ", captured=" + (c.Idle.Window != null && c.Idle.Window.IsMouseCaptured));
                        Require(!c.Idle.Window.IsMouseCaptured, "Picking releases native mouse capture");
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                        down = false;
                    }
                    else if (stage == 12)
                    {
                        Require(!c.Idle.IsHanging && c.Window.IsVisible && c.Window.Width == 220 && c.Phase == TimerPhase.Editing, "Mouse pickup hands off to editable tomato without starting focus");
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hanging-input-results.txt"), "PASS double-click does not expand; native alpha click-through, real pointer capture, resisted pull, short-pull rebound, threshold detach, capture release and editor handoff");
                        timer.Stop();
                        SetCursorPos(original.X, original.Y);
                        c.Quit();
                        return;
                    }

                    stage++;
                    since = watch.Elapsed.TotalSeconds;
                }
                catch (Exception ex)
                {
                    if (down)
                        mouse_event(4, 0, 0, 0, UIntPtr.Zero);
                    SetCursorPos(original.X, original.Y);
                    Verification.SmokeExitCode = 1;
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hanging-input-results.txt"), "FAIL stage " + stage + ": " + ex);
                    timer.Stop();
                    c.Quit();
                }
            };
            timer.Start();
        }
    }
}
