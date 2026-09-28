using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tomato
{
    public sealed class ThrowWindow : Window
    {
        public ThrowSurface Surface { get; private set; }

        public ThrowWindow(BitmapSource texture, Rect bounds, Point origin)
        {
            Title = Texts.Get("throw.title");
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            Surface = new ThrowSurface(texture)
            {
                Origin = origin
            };
            Content = Surface;
            SourceInitialized += delegate
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                int style = Native.GetWindowLong(hwnd, -20);
                Native.SetWindowLong(hwnd, -20, style | 0x20 | 0x08000000 | 0x80);
            };
            Loaded += delegate
            {
                Surface.Begin();
            };
            Closed += delegate
            {
                Surface.End();
            };
        }
    }
}
