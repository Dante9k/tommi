using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Tomato
{
    public sealed class SettingsWindow : Window
    {
        readonly AppController controller;
        ToggleButton sound, wheel, effects, haptics, startup;
        readonly BitmapSource art;
        TextBlock startupStatus;
        bool closing;
        static readonly ControlTemplate ActionTemplate = (ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
 <Border x:Name='Surface' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='12' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' Padding='{TemplateBinding Padding}'>
  <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
 </Border>
 <ControlTemplate.Triggers>
  <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Surface' Property='Opacity' Value='.82'/></Trigger>
  <Trigger Property='IsPressed' Value='True'><Setter TargetName='Surface' Property='Opacity' Value='.62'/></Trigger>
  <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Surface' Property='BorderBrush' Value='#C4D9B9'/></Trigger>
  <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Surface' Property='Opacity' Value='.35'/></Trigger>
 </ControlTemplate.Triggers>
</ControlTemplate>");
        static readonly ControlTemplate ToggleTemplate = (ControlTemplate)XamlReader.Parse(@"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ToggleButton'>
 <Border x:Name='Track' Width='40' Height='24' CornerRadius='12' Background='#3E5045' BorderThickness='1' BorderBrush='#607366'>
  <Ellipse x:Name='Thumb' Width='16' Height='16' Margin='3,0' HorizontalAlignment='Left' Fill='#EDF1E8'/>
 </Border>
 <ControlTemplate.Triggers>
  <Trigger Property='IsChecked' Value='True'>
   <Setter TargetName='Track' Property='Background' Value='#EE8068'/><Setter TargetName='Track' Property='BorderBrush' Value='#F7A48D'/>
   <Setter TargetName='Thumb' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='Thumb' Property='Fill' Value='#FFF8ED'/>
  </Trigger>
  <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Track' Property='Opacity' Value='.84'/></Trigger>
  <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Track' Property='BorderBrush' Value='#FFFFFF'/></Trigger>
 </ControlTemplate.Triggers>
</ControlTemplate>");
        public SettingsWindow(AppController controller, BitmapSource art)
        {
            this.controller = controller;
            this.art = art;
            Title = Texts.Get("settings.title");
            Width = 376;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            FontFamily = new FontFamily(Texts.FontFamily);
            UseLayoutRounding = true;
            BuildContent();
            Closing += delegate
            {
                closing = true;
            };
            Deactivated += delegate
            {
                if (!closing)
                    Close();
            };
            PreviewKeyDown += delegate (object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    Close();
                }
            };
        }

        public void RefreshLanguage()
        {
            Title = Texts.Get("settings.title");
            FontFamily = new FontFamily(Texts.FontFamily);
            BuildContent();
        }

        void BuildContent()
        {
            var body = new StackPanel
            {
                Margin = new Thickness(22, 21, 22, 18)
            };
            var frame = new Border
            {
                Margin = new Thickness(12),
                CornerRadius = new CornerRadius(24),
                Background = new LinearGradientBrush(Color.FromRgb(35, 56, 43), Color.FromRgb(20, 37, 29), 90),
                BorderBrush = Art.Brush("#566B52"),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 18,
                    ShadowDepth = 3,
                    Opacity = .24
                },
                Child = new ScrollViewer
                {
                    Content = body,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                }
            };
            Content = frame;
            var header = new Grid
            {
                Margin = new Thickness(0, 0, 0, 17)
            };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            var brand = new StackPanel();
            brand.Children.Add(Text("Tommi", 23, "#F4F3E7", true));
            brand.Children.Add(Text(Texts.Get("settings.tagline"), 11, "#A7B7A2"));
            header.Children.Add(brand);
            var icon = new Image
            {
                Source = art,
                Width = 56,
                Height = 56
            };
            Grid.SetColumn(icon, 1);
            header.Children.Add(icon);
            body.Children.Add(header);
            var status = new TextBlock
            {
                Text = controller.IsThrowing ? Texts.Get("settings.breakStatus") : controller.Phase == TimerPhase.Running ? Texts.Get("settings.focusStatus") : Texts.Get("settings.readyStatus"),
                FontSize = 11,
                Foreground = Art.Brush("#C4D5A8"),
                Margin = new Thickness(0, 0, 0, 17)
            };
            body.Children.Add(status);
            body.Children.Add(Text(Texts.Get("settings.duration"), 11, "#9BAE96"));
            var presets = new UniformGrid
            {
                Columns = 3,
                Margin = new Thickness(-3, 9, -3, 15)
            };
            AddPreset(presets, "25", Texts.Get("preset.focus"), 1500, true);
            AddPreset(presets, "05", Texts.Get("preset.short"), 300, false);
            AddPreset(presets, "15", Texts.Get("preset.long"), 900, false);
            body.Children.Add(presets);
            Divider(body);
            sound = AddToggle(body, Texts.Get("settings.chime"), "ToggleSound", controller.ToggleSound);
            wheel = AddToggle(body, Texts.Get("settings.wheel"), "ToggleWheel", controller.ToggleWheelSound);
            effects = AddToggle(body, Texts.Get("settings.effects"), "ToggleEffects", controller.ToggleEffectsSound);
            if (controller.HapticsAvailable)
                haptics = AddToggle(body, Texts.Get("settings.haptics"), "ToggleHaptics", controller.ToggleHaptics);
            startup = AddToggle(body, Texts.Get("settings.startup"), "ToggleStartup", controller.ToggleLaunchAtLogin);
            startup.IsEnabled = controller.StartupAvailable;
            startupStatus = Text(controller.StartupStatus, 10, "#A7B7A2");
            startupStatus.TextWrapping = TextWrapping.Wrap;
            startupStatus.Margin = new Thickness(0, 0, 0, 8);
            body.Children.Add(startupStatus);
            Divider(body);
            body.Children.Add(Text(Texts.Get("language.label"), 11, "#9BAE96"));
            var languages = new UniformGrid
            {
                Columns = 3,
                Margin = new Thickness(-3, 8, -3, 12)
            };
            AddLanguage(languages, Texts.Get("language.system"), "system");
            AddLanguage(languages, "English", "en");
            AddLanguage(languages, "简体中文", "zh-CN");
            body.Children.Add(languages);
            Divider(body);
            var preview = Action(Texts.Get("settings.preview"), "PreviewThrow", delegate
            {
                Close();
                controller.Preview();
            });
            preview.Height = 40;
            preview.Margin = new Thickness(0, 4, 0, 9);
            preview.IsEnabled = controller.Phase != TimerPhase.Running;
            if (!preview.IsEnabled)
                preview.ToolTip = Texts.Get("settings.previewBusy");
            body.Children.Add(preview);
            var show = Action(Texts.Get("settings.show"), "ShowTomato", delegate
            {
                Close();
                controller.Show();
            });
            show.Height = 40;
            show.Background = Art.Brush("#D7DFBF");
            show.Foreground = Art.Brush("#243B2B");
            body.Children.Add(show);
            var footer = new Grid
            {
                Margin = new Thickness(0, 14, 0, 0)
            };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel = Action(controller.IsThrowing ? Texts.Get("settings.dismiss") : Texts.Get("settings.cancel"), "CancelFocus", delegate
            {
                Close();
                controller.Cancel();
            });
            cancel.IsEnabled = controller.Phase != TimerPhase.Editing || controller.IsThrowing;
            cancel.Background = Brushes.Transparent;
            cancel.Foreground = Art.Brush("#B5C0AF");
            var quit = Action(Texts.Get("settings.quit"), "QuitTomato", delegate
            {
                Close();
                controller.Quit();
            });
            quit.Background = Brushes.Transparent;
            quit.Foreground = Art.Brush("#D6A293");
            Grid.SetColumn(quit, 1);
            footer.Children.Add(cancel);
            footer.Children.Add(quit);
            body.Children.Add(footer);
            RefreshToggles();
        }

        void AddLanguage(Panel parent, string label, string language)
        {
            var button = Action(label, language == "en" ? "LanguageEnglish" : language == "zh-CN" ? "LanguageChinese" : "LanguageSystem", delegate
            {
                controller.SetLanguage(language);
            });
            button.Height = 34;
            button.Margin = new Thickness(3, 0, 3, 0);
            if (controller.Language == language)
            {
                button.Background = Art.Brush("#D7DFBF");
                button.Foreground = Art.Brush("#243B2B");
            }

            parent.Children.Add(button);
        }

        void AddPreset(Panel parent, string value, string label, int seconds, bool primary)
        {
            var content = new StackPanel();
            var number = Text(value, 27, primary ? "#FFF5E8" : "#EBEDDD", true);
            number.FontFamily = new FontFamily("Segoe UI");
            number.TextAlignment = TextAlignment.Center;
            content.Children.Add(number);
            var caption = Text(label + Texts.Get("preset.suffix"), 10, primary ? "#F9E0CF" : "#A6B6A0");
            caption.TextAlignment = TextAlignment.Center;
            content.Children.Add(caption);
            var button = Action(label + " " + seconds / 60 + Texts.Get("preset.accessibleSuffix"), "Preset" + seconds, delegate
            {
                Close();
                controller.Preset(seconds);
                controller.Show();
            });
            button.Content = content;
            button.Height = 80;
            button.Margin = new Thickness(3, 0, 3, 0);
            button.Background = Art.Brush(primary ? "#B95643" : "#2C4233");
            button.BorderBrush = Art.Brush(primary ? "#D37861" : "#40553F");
            parent.Children.Add(button);
        }

        ToggleButton AddToggle(Panel parent, string label, string name, Action action)
        {
            var row = new Grid
            {
                Height = 44
            };
            row.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Art.Brush("#E1E7D9"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 54, 0), TextWrapping = TextWrapping.Wrap });
            var toggle = new ToggleButton
            {
                Name = name,
                Template = ToggleTemplate,
                Width = 48,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            AutomationProperties.SetName(toggle, label);
            toggle.Click += delegate
            {
                action();
                RefreshToggles();
            };
            row.Children.Add(toggle);
            parent.Children.Add(row);
            return toggle;
        }

        void RefreshToggles()
        {
            sound.IsChecked = controller.Sound;
            wheel.IsChecked = controller.WheelSound;
            effects.IsChecked = controller.EffectsSound;
            startup.IsChecked = controller.LaunchAtLogin;
            startupStatus.Text = controller.StartupStatus;
            if (haptics != null)
                haptics.IsChecked = controller.Haptics;
        }

        static TextBlock Text(string text, double size, string color, bool strong = false)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = Art.Brush(color),
                FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal
            };
        }

        static Button Action(string text, string name, Action action)
        {
            var button = new Button
            {
                Name = name,
                Content = text,
                Template = ActionTemplate,
                Background = Art.Brush("#2C4233"),
                Foreground = Art.Brush("#DEE8D4"),
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(8),
                FontSize = 12,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand
            };
            AutomationProperties.SetName(button, text);
            button.Click += delegate
            {
                action();
            };
            return button;
        }

        static void Divider(Panel parent)
        {
            parent.Children.Add(new Border { Height = 1, Background = Art.Brush("#40513D"), Margin = new Thickness(0, 5, 0, 5) });
        }
    }
}
