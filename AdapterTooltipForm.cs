using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SystemInfoApp
{
    public class AdapterTooltipForm : Form
    {
        private List<NetworkAdapterInfo> ethernetAdapters = new List<NetworkAdapterInfo>();
        private SystemInfo? systemInfo;
        private Icon? appIcon;
        private Image? companyLogo;
        private Timer fadeTimer;
        private Timer hideTimer;
        private double opacity = 0;
        private bool isFadingIn = false;

        public event EventHandler? TooltipHidden;

        // Windows API for rounded corners
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        public AdapterTooltipForm()
        {
            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.Width = 420;
            this.Height = 670;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.Opacity = 0;

            // Load company logo
            LoadCompanyLogo();

            // Enable double buffering
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint, true);

            // Fade timer
            fadeTimer = new Timer();
            fadeTimer.Interval = 20;
            fadeTimer.Tick += FadeTimer_Tick;

            // Auto-hide timer
            hideTimer = new Timer();
            hideTimer.Interval = 15000;
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                HideTooltip();
            };

            this.Paint += AdapterTooltipForm_Paint;

            this.MouseEnter += (s, e) => hideTimer.Stop();
            this.MouseLeave += (s, e) => HideTooltip();
        }

        private void LoadCompanyLogo()
        {
            try
            {
                var logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "CompanyLogo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    companyLogo = Image.FromFile(logoPath);
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(this.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }

        public void ShowTooltip(Point location, List<NetworkAdapterInfo> adapters, SystemInfo? sysInfo, Icon? icon = null)
        {
            ethernetAdapters = adapters ?? new List<NetworkAdapterInfo>();
            systemInfo = sysInfo;
            appIcon = icon;

            // Calculate dynamic height based on number of adapters and disks
            // Base: Title (65) + System section (200) + CPU line (18) + Network header (40) + bottom padding (50)
            int baseHeight = 373;
            // Per adapter: Name line (22) + Description (20) + IP/Subnet/Gateway/DNS/MAC (18*5=90) + spacing (20)
            int adapterHeight = 155;
            // Per extra disk (beyond first): label (18) + progress bar (20) = 38
            int diskCount = sysInfo?.Disks?.Count ?? 1;
            int extraDisks = Math.Max(0, diskCount - 1);
            int diskHeight = extraDisks * 38;
            // Extra DNS servers (beyond first) per adapter: 18px each
            int extraDnsHeight = 0;
            foreach (var adapter in ethernetAdapters)
            {
                if (adapter.DnsAddresses.Count > 1)
                {
                    extraDnsHeight += (adapter.DnsAddresses.Count - 1) * 18;
                }
            }
            // Extra bottom padding for breathing room
            int bottomPadding = 40;
            int calculatedHeight = baseHeight + Math.Max(1, ethernetAdapters.Count) * adapterHeight + diskHeight + extraDnsHeight + bottomPadding;
            this.Height = Math.Max(520, Math.Min(calculatedHeight, 1100));

            var screen = Screen.FromPoint(location);
            this.Location = new Point(
                screen.WorkingArea.Right - this.Width - 10,
                screen.WorkingArea.Bottom - this.Height - 10
            );

            isFadingIn = true;
            opacity = 0;
            this.Show();
            fadeTimer.Start();
            hideTimer.Start();
            this.Invalidate();
        }

        public void HideTooltip()
        {
            isFadingIn = false;
            fadeTimer.Start();
        }

        private void FadeTimer_Tick(object? sender, EventArgs e)
        {
            if (isFadingIn)
            {
                opacity += 0.1;
                if (opacity >= 0.95)
                {
                    opacity = 0.95;
                    fadeTimer.Stop();
                }
            }
            else
            {
                opacity -= 0.15;
                if (opacity <= 0)
                {
                    opacity = 0;
                    fadeTimer.Stop();
                    this.Hide();
                    TooltipHidden?.Invoke(this, EventArgs.Empty);
                }
            }

            this.Opacity = opacity;
        }

        private void AdapterTooltipForm_Paint(object? sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Background
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(45, 45, 48)))
            using (Pen borderPen = new Pen(Color.FromArgb(70, 70, 74), 1))
            {
                Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect);
            }

            int xPos = 15;
            int yPos = 15;

            // Draw company logo (or app icon as fallback)
            if (companyLogo != null)
            {
                // Draw company logo
                g.DrawImage(companyLogo, new Rectangle(xPos, yPos, 40, 40));
                xPos += 50;
            }
            else if (appIcon != null)
            {
                // Fallback to app icon
                using (SolidBrush iconBgBrush = new SolidBrush(Color.FromArgb(60, 60, 64)))
                {
                    Rectangle iconRect = new Rectangle(xPos, yPos, 40, 40);
                    g.FillEllipse(iconBgBrush, iconRect);
                    g.DrawIcon(appIcon, new Rectangle(xPos + 6, yPos + 6, 28, 28));
                }
                xPos += 50;
            }

            // Title
            using (Font titleFont = new Font("Segoe UI", 11f, FontStyle.Bold))
            using (SolidBrush titleBrush = new SolidBrush(Color.White))
            {
                g.DrawString("System Info - Server Edition", titleFont, titleBrush, xPos, yPos + 10);
            }

            yPos += 65;

            // SYSTEM INFORMATION SECTION
            DrawSectionHeader(g, "SYSTEM", yPos);
            yPos += 25;

            if (systemInfo != null)
            {
                DrawInfoLine(g, "Computer", systemInfo.ComputerName, yPos);
                yPos += 18;

                string domainType = systemInfo.IsDomain ? "Domain" : "Workgroup";
                DrawInfoLine(g, domainType, systemInfo.DomainOrWorkgroup, yPos);
                yPos += 18;

                DrawInfoLine(g, "User", systemInfo.LoggedInUser, yPos);
                yPos += 18;

                DrawInfoLine(g, "OS", systemInfo.OSCaption, yPos);
                yPos += 18;

                // Format: "21H2 (OS Build 10.0.20348.4171)"
                string versionDisplay = !string.IsNullOrEmpty(systemInfo.OSDisplayVersion)
                    ? $"{systemInfo.OSDisplayVersion} (OS Build {systemInfo.OSBuildFull})"
                    : $"OS Build {systemInfo.OSBuildFull}";
                DrawInfoLine(g, "Version", versionDisplay, yPos);
                yPos += 18;

                DrawInfoLine(g, "Uptime", systemInfo.GetFormattedUptime(), yPos);
                yPos += 18;

                // Separator before RAM/Disk
                yPos += 10;

                // RAM Information
                if (systemInfo.TotalRAM > 0)
                {
                    DrawInfoLine(g, "RAM", systemInfo.GetFormattedRAM(), yPos);
                    yPos += 18;
                }

                // CPU Information
                if (!string.IsNullOrEmpty(systemInfo.CPUName))
                {
                    string cpuDisplay = $"{systemInfo.CPUCores} Cores / {systemInfo.CPULogicalProcessors} Threads";
                    DrawInfoLine(g, "CPU", cpuDisplay, yPos);
                    yPos += 18;
                }

                // Separator before Disks
                yPos += 10;

                // All Disks with progress bars
                foreach (var disk in systemInfo.Disks)
                {
                    string diskLabel = $"Disk {disk.DriveLetter.TrimEnd(':')}";
                    DrawInfoLine(g, diskLabel, disk.GetFormattedDisk(), yPos);
                    yPos += 18;

                    // Draw progress bar
                    DrawProgressBar(g, yPos, disk.GetUsagePercent());
                    yPos += 20;
                }
            }

            yPos += 15;

            // NETWORK ADAPTERS SECTION
            DrawSectionHeader(g, "NETWORK ADAPTERS", yPos);
            yPos += 25;

            // Draw all Ethernet adapters
            if (ethernetAdapters.Count > 0)
            {
                foreach (var adapter in ethernetAdapters)
                {
                    yPos = DrawAdapterInfo(g, adapter, yPos, adapter.Name);
                    yPos += 10;
                }
            }
            else
            {
                yPos = DrawNoAdapter(g, yPos, "LAN");
            }
        }

        private void DrawSectionHeader(Graphics g, string text, int yPos)
        {
            using (Font font = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(100, 149, 237))) // CornflowerBlue
            {
                g.DrawString(text, font, brush, 15, yPos);
            }

            // Underline
            using (Pen pen = new Pen(Color.FromArgb(70, 70, 74), 1))
            {
                g.DrawLine(pen, 15, yPos + 15, this.Width - 15, yPos + 15);
            }
        }

        private int DrawAdapterInfo(Graphics g, NetworkAdapterInfo info, int yPos, string adapterName)
        {
            int xPos = 15;

            // Determine colors
            Color iconColor;
            Color statusColor;

            if (info.IsDisabled)
            {
                iconColor = Color.FromArgb(150, 150, 150);
                statusColor = Color.FromArgb(150, 150, 150);
            }
            else if (info.IsUp)
            {
                iconColor = Color.FromArgb(100, 149, 237); // CornflowerBlue - same as headers
                statusColor = Color.FromArgb(16, 185, 129);
            }
            else
            {
                iconColor = Color.FromArgb(150, 150, 150);
                statusColor = Color.FromArgb(239, 68, 68);
            }

            // Draw icon (always ethernet icon for server edition)
            string iconChar = "\ue968"; // Ethernet icon
            using (Font iconFont = new Font("Segoe MDL2 Assets", 14f))
            using (SolidBrush iconBrush = new SolidBrush(iconColor))
            {
                g.DrawString(iconChar, iconFont, iconBrush, xPos, yPos - 2);
            }
            xPos += 25;

            // Draw adapter name (e.g., VLAN_10, Ethernet, etc.)
            using (Font labelFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                g.DrawString(adapterName, labelFont, labelBrush, xPos, yPos);
            }

            // Measure adapter name width for status positioning
            float nameWidth;
            using (Font labelFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            {
                nameWidth = g.MeasureString(adapterName, labelFont).Width;
            }
            xPos += (int)nameWidth + 10;

            // Draw status
            string statusText = $"({info.Status})";
            string linkSpeedText = "";

            if (info.IsUp && !info.IsDisabled)
            {
                linkSpeedText = info.GetFormattedLinkSpeed();
                if (!string.IsNullOrEmpty(linkSpeedText))
                {
                    statusText = $"({info.Status} - {linkSpeedText})";
                }
            }

            using (Font statusFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (SolidBrush statusBrush = new SolidBrush(statusColor))
            {
                g.DrawString(statusText, statusFont, statusBrush, xPos, yPos);
            }

            yPos += 22;

            // Description (hardware name)
            using (Font descFont = new Font("Segoe UI", 8f))
            using (SolidBrush descBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                g.DrawString(info.Description, descFont, descBrush, 15, yPos);
            }

            yPos += 20;

            // Network details
            if (info.IpAddresses.Count > 0)
            {
                DrawInfoLine(g, "IP", info.IpAddresses[0], yPos);
                yPos += 18;
            }

            if (info.SubnetMasks.Count > 0 && !info.IsDisabled)
            {
                DrawInfoLine(g, "Subnet", info.SubnetMasks[0], yPos);
                yPos += 18;
            }

            if (info.GatewayAddresses.Count > 0 && !info.IsDisabled)
            {
                bool isReachable = info.GatewayReachable.Count > 0 && info.GatewayReachable[0];
                long latency = info.GatewayLatency.Count > 0 ? info.GatewayLatency[0] : -1;
                DrawInfoLineWithLatency(g, "Gateway", info.GatewayAddresses[0], yPos, isReachable, latency);
                yPos += 18;
            }

            if (info.DnsAddresses.Count > 0 && !info.IsDisabled)
            {
                for (int i = 0; i < info.DnsAddresses.Count; i++)
                {
                    bool isReachable = i < info.DnsReachable.Count && info.DnsReachable[i];
                    string label = i == 0 ? "DNS" : "DNS " + (i + 1);
                    DrawInfoLineWithStatus(g, label, info.DnsAddresses[i], yPos, isReachable);
                    yPos += 18;
                }
            }

            if (!string.IsNullOrEmpty(info.MacAddress) && info.MacAddress != "N/A")
            {
                DrawInfoLine(g, "MAC", info.MacAddress, yPos);
                yPos += 18;
            }

            return yPos;
        }

        private int DrawNoAdapter(Graphics g, int yPos, string adapterType)
        {
            int xPos = 15;

            // Always use ethernet icon for server edition
            string iconChar = "\ue968";
            using (Font iconFont = new Font("Segoe MDL2 Assets", 14f))
            using (SolidBrush iconBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                g.DrawString(iconChar, iconFont, iconBrush, xPos, yPos - 2);
            }
            xPos += 25;

            using (Font labelFont = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                g.DrawString(adapterType, labelFont, labelBrush, xPos, yPos);
            }
            xPos += 60;

            using (Font font = new Font("Segoe UI", 9f))
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 150, 150)))
            {
                g.DrawString("(No adapters found)", font, brush, xPos, yPos);
            }

            return yPos + 25;
        }

        private void DrawInfoLine(Graphics g, string label, string value, int yPos)
        {
            using (Font labelFont = new Font("Segoe UI", 8.5f))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                g.DrawString($"{label}:", labelFont, labelBrush, 25, yPos);
                g.DrawString(value, labelFont, valueBrush, 110, yPos);
            }
        }

        private void DrawInfoLineWithStatus(Graphics g, string label, string value, int yPos, bool isReachable)
        {
            using (Font labelFont = new Font("Segoe UI", 8.5f))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                g.DrawString($"{label}:", labelFont, labelBrush, 25, yPos);
                g.DrawString(value, labelFont, valueBrush, 110, yPos);

                // Draw status icon (CheckMark or Info)
                using (Font iconFont = new Font("Segoe MDL2 Assets", 9f))
                {
                    string iconChar;
                    Color iconColor;

                    if (isReachable)
                    {
                        iconChar = "\ue73e"; // CheckMark
                        iconColor = Color.FromArgb(16, 185, 129); // Green
                    }
                    else
                    {
                        iconChar = "\ue946"; // Info
                        iconColor = Color.FromArgb(239, 68, 68); // Red
                    }

                    using (SolidBrush iconBrush = new SolidBrush(iconColor))
                    {
                        // Position icon after the value
                        SizeF valueSize = g.MeasureString(value, labelFont);
                        float iconX = 110 + valueSize.Width + 5;
                        g.DrawString(iconChar, iconFont, iconBrush, iconX, yPos);
                    }
                }
            }
        }

        private void DrawInfoLineWithLatency(Graphics g, string label, string value, int yPos, bool isReachable, long latency)
        {
            using (Font labelFont = new Font("Segoe UI", 8.5f))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                g.DrawString($"{label}:", labelFont, labelBrush, 25, yPos);
                g.DrawString(value, labelFont, valueBrush, 110, yPos);

                // Draw status icon and latency
                using (Font iconFont = new Font("Segoe MDL2 Assets", 9f))
                {
                    SizeF valueSize = g.MeasureString(value, labelFont);
                    float iconX = 110 + valueSize.Width + 5;

                    if (isReachable && latency >= 0)
                    {
                        // Determine color based on latency
                        Color latencyColor;
                        if (latency < 20)
                            latencyColor = Color.FromArgb(16, 185, 129); // Green
                        else if (latency < 50)
                            latencyColor = Color.FromArgb(251, 191, 36); // Yellow
                        else
                            latencyColor = Color.FromArgb(239, 68, 68); // Red

                        // Draw CheckMark
                        using (SolidBrush iconBrush = new SolidBrush(latencyColor))
                        {
                            g.DrawString("\ue73e", iconFont, iconBrush, iconX, yPos);
                        }

                        // Draw latency text in parentheses (skip if < 1ms)
                        string latencyText = latency < 1 ? "" : $"({latency} ms)";
                        using (SolidBrush latencyBrush = new SolidBrush(latencyColor))
                        {
                            float latencyX = iconX + 15;
                            g.DrawString(latencyText, labelFont, latencyBrush, latencyX, yPos);
                        }
                    }
                    else
                    {
                        // Draw red Info icon for unreachable
                        using (SolidBrush iconBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                        {
                            g.DrawString("\ue946", iconFont, iconBrush, iconX, yPos);
                        }
                    }
                }
            }
        }

        private void DrawProgressBar(Graphics g, int yPos, int percentage)
        {
            int barWidth = 280;
            int barHeight = 10;
            int xPos = 25;

            // Background
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(60, 60, 64)))
            {
                Rectangle bgRect = new Rectangle(xPos, yPos, barWidth, barHeight);
                g.FillRectangle(bgBrush, bgRect);
            }

            // Progress bar - color based on percentage
            Color progressColor;
            if (percentage < 70)
                progressColor = Color.FromArgb(16, 185, 129); // Green
            else if (percentage < 85)
                progressColor = Color.FromArgb(251, 191, 36); // Yellow
            else
                progressColor = Color.FromArgb(239, 68, 68); // Red

            int fillWidth = (int)(barWidth * percentage / 100.0);
            using (SolidBrush progressBrush = new SolidBrush(progressColor))
            {
                Rectangle progressRect = new Rectangle(xPos, yPos, fillWidth, barHeight);
                g.FillRectangle(progressBrush, progressRect);
            }

            // Percentage text
            using (Font percentFont = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (SolidBrush percentBrush = new SolidBrush(Color.FromArgb(220, 220, 220)))
            {
                string percentText = $"{percentage}%";
                SizeF textSize = g.MeasureString(percentText, percentFont);
                float textX = xPos + barWidth + 5;
                float textY = yPos - 1;
                g.DrawString(percentText, percentFont, percentBrush, textX, textY);
            }

            // Border
            using (Pen borderPen = new Pen(Color.FromArgb(70, 70, 74), 1))
            {
                Rectangle borderRect = new Rectangle(xPos, yPos, barWidth, barHeight);
                g.DrawRectangle(borderPen, borderRect);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                fadeTimer?.Dispose();
                hideTimer?.Dispose();
                companyLogo?.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }
    }
}
