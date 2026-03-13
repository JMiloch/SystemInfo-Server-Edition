using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace SystemInfoApp
{
    public class TrayApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly NetworkAdapterManager adapterManager;
        private readonly Timer updateTimer;
        private List<NetworkAdapterInfo> ethernetAdapters = new List<NetworkAdapterInfo>();
        private readonly AdapterTooltipForm tooltipForm;
        private Timer? tooltipShowTimer;
        private bool isTooltipVisible = false;
        private SystemInfo? systemInfo;

        public TrayApplicationContext()
        {
            adapterManager = new NetworkAdapterManager();

            // Initialize custom tooltip
            tooltipForm = new AdapterTooltipForm();
            tooltipForm.TooltipHidden += (s, e) => isTooltipVisible = false;

            // Initialize Tray Icon
            trayIcon = new NotifyIcon
            {
                Icon = LoadIcon(),
                Visible = true,
                Text = "System Info - Server Edition"
            };

            // Setup mouse events for custom tooltip
            trayIcon.MouseMove += TrayIcon_MouseMove;

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            var refreshItem = new ToolStripMenuItem("Refresh Now");
            refreshItem.Click += (s, e) => UpdateNetworkState();
            contextMenu.Items.Add(refreshItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += AboutItem_Click;
            contextMenu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += ExitItem_Click;
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;

            // Setup timer for periodic updates (every 10 seconds)
            updateTimer = new Timer
            {
                Interval = 10000
            };
            updateTimer.Tick += (s, e) => UpdateNetworkState();
            updateTimer.Start();

            // Initial update
            UpdateNetworkState();
        }

        private Icon LoadIcon()
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "Appicon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
            }
            catch { }

            return SystemIcons.Application;
        }

        private void UpdateNetworkState()
        {
            // Get all ethernet adapters (including VLAN adapters)
            ethernetAdapters = adapterManager.GetAllEthernetAdapters();

            // Get system information
            systemInfo = SystemInfo.GetSystemInformation();

            System.Diagnostics.Debug.WriteLine($"=== Network State Update ===");
            System.Diagnostics.Debug.WriteLine($"Found {ethernetAdapters.Count} Ethernet adapter(s):");
            foreach (var adapter in ethernetAdapters)
            {
                System.Diagnostics.Debug.WriteLine($"  - {adapter.Name}: {adapter.Status}");
            }
        }

        private void TrayIcon_MouseMove(object? sender, MouseEventArgs e)
        {
            // If tooltip is already visible, don't do anything
            if (isTooltipVisible)
                return;

            // Show tooltip with a small delay
            if (tooltipShowTimer == null)
            {
                tooltipShowTimer = new Timer();
                tooltipShowTimer.Interval = 800; // 800ms delay for more stable behavior
                tooltipShowTimer.Tick += (s, args) =>
                {
                    tooltipShowTimer?.Stop();
                    tooltipShowTimer?.Dispose();
                    tooltipShowTimer = null;

                    // Show the custom tooltip with icon
                    isTooltipVisible = true;
                    tooltipForm.ShowTooltip(Cursor.Position, ethernetAdapters, systemInfo, trayIcon.Icon);
                };
                tooltipShowTimer.Start();
            }
        }

        private void AboutItem_Click(object? sender, EventArgs e)
        {
            using (var aboutForm = new AboutForm())
            {
                aboutForm.ShowDialog();
            }
        }

        private void ExitItem_Click(object? sender, EventArgs e)
        {
            updateTimer.Stop();
            trayIcon.Visible = false;
            tooltipForm.Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                updateTimer?.Dispose();
                trayIcon?.Dispose();
                tooltipShowTimer?.Dispose();
                tooltipForm?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
