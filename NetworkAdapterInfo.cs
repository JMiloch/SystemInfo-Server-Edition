using System;
using System.Collections.Generic;
using System.Linq;

namespace SystemInfoApp
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public List<string> IpAddresses { get; set; } = new List<string>();
        public List<string> SubnetMasks { get; set; } = new List<string>();
        public List<string> GatewayAddresses { get; set; } = new List<string>();
        public List<bool> GatewayReachable { get; set; } = new List<bool>(); // Parallel to GatewayAddresses
        public List<long> GatewayLatency { get; set; } = new List<long>(); // Latency in milliseconds
        public List<string> DnsAddresses { get; set; } = new List<string>();
        public List<bool> DnsReachable { get; set; } = new List<bool>(); // Parallel to DnsAddresses
        public bool IsUp { get; set; }
        public bool IsDisabled { get; set; } // Adapter is administratively disabled
        public bool IsWireless { get; set; }
        public bool IsEthernet { get; set; }
        public uint DeviceId { get; set; } // WMI DeviceID for Enable/Disable
        public long LinkSpeed { get; set; } // Link speed in bits per second

        public string GetFormattedInfo()
        {
            var lines = new List<string>();

            lines.Add($"{(IsEthernet ? "Ethernet" : IsWireless ? "WiFi" : "Adapter")}: {Name}");
            lines.Add($"Device: {Description}");
            lines.Add($"Status: {Status}");
            lines.Add("");

            if (IpAddresses.Any())
            {
                lines.Add($"IP: {string.Join(", ", IpAddresses)}");
            }
            else
            {
                lines.Add("IP: None");
            }

            if (GatewayAddresses.Any())
            {
                lines.Add($"Gateway: {string.Join(", ", GatewayAddresses)}");
            }
            else
            {
                lines.Add("Gateway: None");
            }

            if (DnsAddresses.Any())
            {
                lines.Add($"DNS: {string.Join(", ", DnsAddresses)}");
            }
            else
            {
                lines.Add("DNS: None");
            }

            lines.Add($"MAC Address: {MacAddress}");

            return string.Join(Environment.NewLine, lines);
        }

        public string GetShortStatus()
        {
            if (Status == "Disconnected" || Status == "Disabled")
            {
                return $"({Status})";
            }
            return "";
        }

        public string GetFormattedLinkSpeed()
        {
            if (LinkSpeed <= 0)
                return "";

            // Convert from bits per second to Mbps/Gbps
            double speedMbps = LinkSpeed / 1_000_000.0;

            if (speedMbps >= 1000)
            {
                double speedGbps = speedMbps / 1000.0;
                return $"{speedGbps:0.##} Gbps";
            }
            else
            {
                return $"{speedMbps:0.##} Mbps";
            }
        }
    }
}
