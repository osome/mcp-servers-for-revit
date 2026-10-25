using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Keeps the MCP ribbon buttons' icons in step with the service state and Revit's light/dark theme.
    /// The PNGs in Ressources are black shapes on transparent; only their alpha is used, and the colour
    /// is applied here, so one set of images works for every state and theme.
    /// </summary>
    internal static class RibbonIcons
    {
        private static readonly Color Connected = Color.FromRgb(46, 158, 68);
        private static readonly Color LightThemeForeground = Color.FromRgb(30, 30, 30);
        private static readonly Color DarkThemeForeground = Color.FromRgb(230, 230, 230);

        private static PushButton _serviceButton;
        private static PushButton _settingsButton;

        public static void Register(PushButton serviceButton, PushButton settingsButton)
        {
            _serviceButton = serviceButton;
            _settingsButton = settingsButton;
            Refresh();
        }

        /// <summary>
        /// Call after the service starts or stops, and when Revit's theme changes.
        /// </summary>
        public static void Refresh()
        {
            Color foreground = IsDarkTheme() ? DarkThemeForeground : LightThemeForeground;

            if (_serviceButton != null)
            {
                bool running = SocketService.Instance.IsRunning;
                string shape = running ? "plug-connected" : "plug-disconnected";
                Color color = running ? Connected : foreground;
                _serviceButton.Image = Tinted($"{shape}-16.png", color);
                _serviceButton.LargeImage = Tinted($"{shape}-32.png", color);
                _serviceButton.ToolTip = running
                    ? $"The MCP service is on (127.0.0.1:{SocketService.Instance.Port}). Click to switch it off."
                    : "The MCP service is off. Click to switch it on, so AI assistants on this computer can work with the open model.";
            }

            if (_settingsButton != null)
            {
                _settingsButton.Image = Tinted("settings-16.png", foreground);
                _settingsButton.LargeImage = Tinted("settings-32.png", foreground);
            }
        }

        private static bool IsDarkTheme()
        {
#if NET
            // UIThemeManager arrived in Revit 2024; the .NET Framework builds (2020-2024) stay on light-theme colours.
            return UIThemeManager.CurrentTheme == UITheme.Dark;
#else
            return false;
#endif
        }

        private static BitmapSource Tinted(string fileName, Color color)
        {
            var source = new FormatConvertedBitmap(
                new BitmapImage(new Uri($"/RevitMCPPlugin;component/Core/Ressources/{fileName}", UriKind.RelativeOrAbsolute)),
                PixelFormats.Bgra32, null, 0);

            int stride = source.PixelWidth * 4;
            byte[] pixels = new byte[stride * source.PixelHeight];
            source.CopyPixels(pixels, stride, 0);

            // Bgra32 isn't premultiplied, so the colour channels can be set outright and alpha kept.
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
            }

            BitmapSource tinted = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96,
                PixelFormats.Bgra32, null, pixels, stride);
            tinted.Freeze();
            return tinted;
        }
    }
}
