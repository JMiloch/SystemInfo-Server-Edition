using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Xml.Linq;
using System.Reflection;

namespace SystemInfoApp
{
    public class AboutForm : Form
    {
        // Default values (used when config file is missing or invalid)
        private string aboutDescription = "This application is an internal development by the IT team.\n\n" +
                       "It displays selected system information and provides\n" +
                       "a quick overview of the current system status.\n" +
                       "The application runs in the background and is accessible via the system tray.";
        private string supportEmailDisplay = "mail.it@mail.com";
        private string supportEmailAddress = "mailto:mail@mailprovider.com";

        public AboutForm()
        {
            LoadConfig();
            InitializeComponents();
        }

        private void LoadConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "about.xml");
                if (File.Exists(configPath))
                {
                    var doc = XDocument.Load(configPath);
                    var root = doc.Element("AboutConfig");
                    if (root != null)
                    {
                        var desc = root.Element("Description")?.Value;
                        if (!string.IsNullOrWhiteSpace(desc))
                            aboutDescription = desc;

                        var email = root.Element("SupportEmail")?.Value;
                        if (!string.IsNullOrWhiteSpace(email))
                            supportEmailDisplay = email;

                        var emailAddr = root.Element("SupportEmailAddress")?.Value;
                        if (!string.IsNullOrWhiteSpace(emailAddr))
                            supportEmailAddress = emailAddr;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading about config: {ex.Message}");
            }
        }

        private void InitializeComponents()
        {
            // Get version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}" : "2.0.26.3";

            // Form settings
            this.Text = "About System Info - Server Edition";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Width = 500;
            this.Height = 520;
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ShowInTaskbar = false;

            int yPos = 20;

            // App Icon
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "Appicon.ico");
                if (File.Exists(iconPath))
                {
                    var icon = new Icon(iconPath);
                    var pictureBox = new PictureBox
                    {
                        Image = icon.ToBitmap(),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Width = 64,
                        Height = 64,
                        Location = new Point((this.ClientSize.Width - 64) / 2, yPos),
                        BackColor = Color.Transparent
                    };
                    this.Controls.Add(pictureBox);
                }
            }
            catch { }

            yPos += 80;

            // App Name
            var lblAppName = new Label
            {
                Text = "System Info - Server Edition",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(0, yPos),
                Width = this.ClientSize.Width,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblAppName.Location = new Point((this.ClientSize.Width - lblAppName.PreferredWidth) / 2, yPos);
            this.Controls.Add(lblAppName);

            yPos += 40;

            // Version (read from assembly)
            var lblVersion = new Label
            {
                Text = $"Version {versionString}",
                Font = new Font("Segoe UI", 10f),
                ForeColor = Color.FromArgb(200, 200, 200),
                AutoSize = true,
                Location = new Point(0, yPos),
                Width = this.ClientSize.Width,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblVersion.Location = new Point((this.ClientSize.Width - lblVersion.PreferredWidth) / 2, yPos);
            this.Controls.Add(lblVersion);

            yPos += 30;

            // Copyright
            var lblCopyright = new Label
            {
                Text = "© 2026 Miloch",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(180, 180, 180),
                AutoSize = true,
                Location = new Point(0, yPos),
                Width = this.ClientSize.Width,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblCopyright.Location = new Point((this.ClientSize.Width - lblCopyright.PreferredWidth) / 2, yPos);
            this.Controls.Add(lblCopyright);

            yPos += 35;

            // Description (from config or default)
            var lblDescription = new Label
            {
                Text = aboutDescription,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(30, yPos),
                Width = this.ClientSize.Width - 60,
                Height = 110,
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(lblDescription);

            yPos += 120;

            // Support Header
            var lblSupportHeader = new Label
            {
                Text = "Support & Kontakt:",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 149, 237),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            this.Controls.Add(lblSupportHeader);

            yPos += 25;

            // Email Link (from config or default)
            var emailDisplay = supportEmailDisplay;
            var emailAddress = supportEmailAddress;
            var lblEmail = new LinkLabel
            {
                Text = emailDisplay,
                Font = new Font("Segoe UI", 9f),
                LinkColor = Color.FromArgb(100, 149, 237),
                ActiveLinkColor = Color.FromArgb(130, 179, 255),
                VisitedLinkColor = Color.FromArgb(100, 149, 237),
                AutoSize = true,
                Location = new Point(30, yPos)
            };
            lblEmail.LinkClicked += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo(emailAddress) { UseShellExecute = true }); }
                catch { }
            };
            this.Controls.Add(lblEmail);

            yPos += 40;

            // License Info
            var lblLicense = new Label
            {
                Text = "LICENSE - FREEWARE\n" +
                       "This software is provided \"as is\", without warranty of any kind.",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location = new Point(30, yPos),
                Width = this.ClientSize.Width - 60,
                Height = 40,
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(lblLicense);

            yPos += 50;

            // OK Button
            var btnOK = new Button
            {
                Text = "OK",
                Font = new Font("Segoe UI", 9f),
                Width = 100,
                Height = 30,
                Location = new Point((this.ClientSize.Width - 100) / 2, yPos),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => this.Close();
            this.Controls.Add(btnOK);

            // Set OK button as accept button
            this.AcceptButton = btnOK;
        }
    }
}
