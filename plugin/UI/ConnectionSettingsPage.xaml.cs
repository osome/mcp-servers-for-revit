using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Core;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Interaction logic for ConnectionSettingsPage.xaml
    /// </summary>
    public partial class ConnectionSettingsPage : Page
    {
        public ConnectionSettingsPage()
        {
            InitializeComponent();

            PortHintTextBlock.Text =
                $"Between {ConnectionSettings.MinPort} and {ConnectionSettings.MaxPort}. Default {ConnectionSettings.DefaultPort}. " +
                "Pick another port if something else on this computer already uses it. " +
                "The MCP server finds the port automatically; it doesn't need to be configured.";
            PortTextBox.Text = ConnectionSettings.Load().Port.ToString();
            ShowRunningPort();
        }

        private void ShowRunningPort()
        {
            if (SocketService.Instance.IsRunning)
                ShowStatus($"The service is running on port {SocketService.Instance.Port}.", isError: false);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            PortTextBox.Text = ConnectionSettings.DefaultPort.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortTextBox.Text.Trim(), out int port) || !ConnectionSettings.IsValidPort(port))
            {
                ShowStatus($"Enter a whole number between {ConnectionSettings.MinPort} and {ConnectionSettings.MaxPort}.", isError: true);
                return;
            }

            try
            {
                new ConnectionSettings { Port = port }.Save();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ShowStatus($"Couldn't save {ConnectionSettings.FilePath}: {ex.Message}", isError: true);
                return;
            }

            if (SocketService.Instance.IsRunning && SocketService.Instance.Port != port)
                ShowStatus($"Saved. The service is still on port {SocketService.Instance.Port}; switch it off and on to use port {port}.", isError: false);
            else
                ShowStatus($"Saved. The service will use port {port}.", isError: false);
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusTextBlock.Text = message;
            StatusTextBlock.Foreground = isError ? Brushes.Firebrick : Brushes.DimGray;
        }
    }
}
