using System;
using System.Diagnostics;
using System.IO;
using System.Resources;
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
            // Icons are cosmetic: a failure here must never stop the add-in loading or a command finishing.
            try
            {
                Color foreground = IsDarkTheme() ? DarkThemeForeground : LightThemeForeground;

                if (_serviceButton != null)
                {
                    bool running = SocketService.Instance.IsRunning;
                    _serviceButton.ToolTip = running
                        ? $"The MCP service is on (127.0.0.1:{SocketService.Instance.Port}). Click to switch it off."
                        : "The MCP service is off. Click to switch it on, so AI assistants on this computer can work with the open model.";

                    string shape = running ? "plug-connected" : "plug-disconnected";
                    Color color = running ? Connected : foreground;
                    _serviceButton.Image = Tinted($"{shape}-16.png", color);
                    _serviceButton.LargeImage = Tinted($"{shape}-32.png", color);
                }

                if (_settingsButton != null)
                {
                    _settingsButton.Image = Tinted("settings-16.png", foreground);
                    _settingsButton.LargeImage = Tinted("settings-32.png", foreground);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"NPC MCP: couldn't update the ribbon icons: {ex}");
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

        // The WPF resources compiled into this assembly (the <Resource> items in the .csproj).
        private static readonly ResourceManager WpfResources = new ResourceManager(
            typeof(RibbonIcons).Assembly.GetName().Name + ".g", typeof(RibbonIcons).Assembly);

        private static BitmapSource Tinted(string fileName, Color color)
        {
            // Read straight from the embedded resources: a relative pack URI has no base to resolve against
            // when decoded here, and WPF then treats it as a file path.
            Stream stream = WpfResources.GetStream("core/ressources/" + fileName.ToLowerInvariant())
                ?? throw new FileNotFoundException($"Ribbon icon {fileName} isn't embedded in the plugin.");
            BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];

            var source = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

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
