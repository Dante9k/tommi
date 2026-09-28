using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tomato
{
    public static class Art
    {
        public static readonly Color Cream = Color.FromRgb(255, 242, 215);
        public static SolidColorBrush Brush(string color)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            b.Freeze();
            return b;
        }

        public static void Text(DrawingContext dc, string text, double size, Brush color, double x, double y, string font, bool bold)
        {
            var ft = new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface(new FontFamily(font), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, color, 1.0);
            dc.DrawText(ft, new Point(x, y));
        }

        public static void CenterText(DrawingContext dc, string text, double size, Brush color, double x, double y, string font, bool bold)
        {
            var ft = new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface(new FontFamily(font), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, color, 1.0);
            dc.DrawText(ft, new Point(x - ft.Width / 2, y));
        }

        public static BitmapSource TomatoImage(int pixels)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(pixels / 440.0, pixels / 440.0));
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Tomato.Texture.png"))
                {
                    if (stream != null)
                    {
                        var source = new BitmapImage();
                        source.BeginInit();
                        source.CacheOption = BitmapCacheOption.OnLoad;
                        source.StreamSource = stream;
                        source.EndInit();
                        source.Freeze();
                        dc.DrawImage(source, new Rect(22, 18, 396, 396));
                    }
                    else
                        throw new InvalidOperationException(Texts.Get("art.missing"));
                }

                dc.Pop();
            }

            var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        public static void Save(Visual visual, int width, int height, string path)
        {
            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var file = System.IO.File.Create(path))
                encoder.Save(file);
        }
    }
}
