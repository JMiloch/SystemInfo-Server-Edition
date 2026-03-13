using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Management;
using System.Threading;

namespace SystemInfoApp
{
    public class NetworkAdapterManager
    {
        public List<NetworkAdapterInfo> GetAllEthernetAdapters()
        {
            var adapters = new List<NetworkAdapterInfo>();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            foreach (var ni in interfaces)
            {
                // Ethernet: NetworkInterfaceType.Ethernet (802.3)
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    // Skip VirtualBox and Bluetooth adapters (Hyper-V and VMware are shown)
                    if (ni.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ||
                        ni.Name.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"Found Ethernet adapter: {ni.Name} - {ni.Description} - Status: {ni.OperationalStatus}");
                    adapters.Add(BuildAdapterInfo(ni, isEthernet: true, isWireless: false));
                }
            }

            // Sort by name to show VLAN adapters in order
            return adapters.OrderBy(a => a.Name).ToList();
        }

        // Keep for backwards compatibility
        public NetworkAdapterInfo? GetEthernetAdapter()
        {
            return GetAllEthernetAdapters().FirstOrDefault();
        }

        public NetworkAdapterInfo? GetWirelessAdapter()
        {
            // First try to get from NetworkInterface (active adapters)
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Collect all WiFi adapters, prioritizing connected ones
            NetworkInterface? connectedWifi = null;
            NetworkInterface? disconnectedWifi = null;

            foreach (var ni in interfaces)
            {
                // Skip virtual adapters (like Virtual WiFi Miniport, Hosted Network, etc.)
                if (ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("Miniport", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("Hosted Network", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("Microsoft Wi-Fi Direct", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Skipping virtual WiFi adapter: {ni.Description}");
                    continue;
                }

                // WiFi: NetworkInterfaceType.Wireless80211 (Native 802.11)
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    System.Diagnostics.Debug.WriteLine($"Found WiFi adapter: {ni.Name} - {ni.Description} - Status: {ni.OperationalStatus}");

                    if (ni.OperationalStatus == OperationalStatus.Up && connectedWifi == null)
                    {
                        connectedWifi = ni;
                    }
                    else if (disconnectedWifi == null)
                    {
                        disconnectedWifi = ni;
                    }
                }
                // Fallback: Check description for WiFi keywords
                else if (ni.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("WLAN", StringComparison.OrdinalIgnoreCase) ||
                    ni.Description.Contains("802.11", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Found WiFi adapter (by description): {ni.Name} - {ni.Description} - Status: {ni.OperationalStatus}");

                    if (ni.OperationalStatus == OperationalStatus.Up && connectedWifi == null)
                    {
                        connectedWifi = ni;
                    }
                    else if (disconnectedWifi == null)
                    {
                        disconnectedWifi = ni;
                    }
                }
            }

            // Prioritize connected WiFi, then disconnected, then check WMI for disabled
            if (connectedWifi != null)
            {
                System.Diagnostics.Debug.WriteLine($"Using connected WiFi adapter: {connectedWifi.Name}");
                return BuildAdapterInfo(connectedWifi, isEthernet: false, isWireless: true);
            }

            if (disconnectedWifi != null)
            {
                System.Diagnostics.Debug.WriteLine($"Using disconnected WiFi adapter: {disconnectedWifi.Name}");
                return BuildAdapterInfo(disconnectedWifi, isEthernet: false, isWireless: true);
            }

            // If not found, check WMI for disabled adapters
            return GetWirelessAdapterFromWMI();
        }

        private NetworkAdapterInfo? GetWirelessAdapterFromWMI()
        {
            try
            {
                var query = "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID LIKE '%Wi-Fi%' OR Name LIKE '%Wireless%' OR Name LIKE '%Wi-Fi%' OR Name LIKE '%WLAN%'";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject adapter in searcher.Get())
                {
                    var netConnectionId = adapter["NetConnectionID"]?.ToString();
                    if (netConnectionId != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found WiFi adapter in WMI: {netConnectionId} (disabled)");
                        return BuildAdapterInfoFromWMI(adapter, isEthernet: false, isWireless: true);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting WiFi from WMI: {ex.Message}");
            }

            return null;
        }

        private NetworkAdapterInfo BuildAdapterInfoFromWMI(ManagementObject wmiAdapter, bool isEthernet, bool isWireless)
        {
            var name = wmiAdapter["NetConnectionID"]?.ToString() ?? wmiAdapter["Name"]?.ToString() ?? "";
            var description = wmiAdapter["Name"]?.ToString() ?? "";
            var netEnabled = wmiAdapter["NetEnabled"];
            var interfaceIndex = wmiAdapter["InterfaceIndex"];
            var speed = wmiAdapter["Speed"];

            bool isDisabled = netEnabled is bool enabled && !enabled;
            bool isUp = netEnabled is bool en && en;

            var info = new NetworkAdapterInfo
            {
                Name = name,
                Description = description,
                Status = isDisabled ? "Disabled" : "Disconnected",
                MacAddress = "N/A",
                IsUp = isUp,
                IsDisabled = isDisabled,
                IsEthernet = isEthernet,
                IsWireless = isWireless,
                DeviceId = interfaceIndex != null ? Convert.ToUInt32(interfaceIndex) : 0,
                LinkSpeed = speed != null ? Convert.ToInt64(speed) : 0
            };

            // Try to get IP information from NetworkInterface if adapter is enabled
            if (!isDisabled)
            {
                try
                {
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces();

                    // Try multiple matching strategies
                    NetworkInterface? matchingInterface = null;

                    // Strategy 1: Match by exact name
                    matchingInterface = interfaces.FirstOrDefault(ni => ni.Name == name);

                    // Strategy 2: Match by description
                    if (matchingInterface == null)
                    {
                        matchingInterface = interfaces.FirstOrDefault(ni => ni.Description == description);
                    }

                    // Strategy 3: Match by partial description (for WiFi keywords)
                    if (matchingInterface == null && isWireless)
                    {
                        matchingInterface = interfaces.FirstOrDefault(ni =>
                            ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                            ni.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                            ni.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                            ni.Description.Contains("WLAN", StringComparison.OrdinalIgnoreCase));
                    }

                    System.Diagnostics.Debug.WriteLine($"WMI adapter '{name}' - Matching interface: {matchingInterface?.Name ?? "NOT FOUND"}");

                    if (matchingInterface != null)
                    {
                        var ipProperties = matchingInterface.GetIPProperties();

                        // Get MAC Address
                        info.MacAddress = GetMacAddress(matchingInterface);

                        // Get IP addresses (IPv4 only)
                        info.IpAddresses = ipProperties.UnicastAddresses
                            .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(addr => addr.Address.ToString())
                            .ToList();

                        // Get Subnet Masks (IPv4 only)
                        info.SubnetMasks = ipProperties.UnicastAddresses
                            .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(addr => addr.IPv4Mask.ToString())
                            .ToList();

                        // Get Gateway addresses
                        info.GatewayAddresses = ipProperties.GatewayAddresses
                            .Where(gw => gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(gw => gw.Address.ToString())
                            .ToList();

                        // Check Gateway reachability and latency
                        foreach (var gateway in info.GatewayAddresses)
                        {
                            var (isReachable, latency) = PingGateway(gateway);
                            info.GatewayReachable.Add(isReachable);
                            info.GatewayLatency.Add(latency);
                        }

                        // Get DNS addresses
                        info.DnsAddresses = ipProperties.DnsAddresses
                            .Where(dns => dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .Select(dns => dns.ToString())
                            .ToList();

                        // Check DNS reachability
                        foreach (var dnsServer in info.DnsAddresses)
                        {
                            bool isReachable = CheckDnsReachability(dnsServer);
                            info.DnsReachable.Add(isReachable);
                        }

                        // Update status if connected
                        if (matchingInterface.OperationalStatus == OperationalStatus.Up)
                        {
                            info.Status = "Connected";
                            info.IsUp = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting IP info for WMI adapter {name}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"WMI Adapter {info.Name}: IsUp={info.IsUp}, IsDisabled={info.IsDisabled}, DeviceId={info.DeviceId}");

            return info;
        }

        private NetworkAdapterInfo BuildAdapterInfo(NetworkInterface ni, bool isEthernet, bool isWireless)
        {
            // Check if adapter is disabled via WMI
            bool isDisabled = IsAdapterDisabled(ni.Name);

            var info = new NetworkAdapterInfo
            {
                Name = ni.Name,
                Description = ni.Description,
                Status = GetStatusString(ni.OperationalStatus, isDisabled),
                MacAddress = GetMacAddress(ni),
                IsUp = ni.OperationalStatus == OperationalStatus.Up,
                IsDisabled = isDisabled,
                IsEthernet = isEthernet,
                IsWireless = isWireless,
                DeviceId = GetInterfaceIndex(ni.Name, ni.Description),
                LinkSpeed = ni.Speed
            };

            System.Diagnostics.Debug.WriteLine($"Adapter {info.Name}: IsUp={info.IsUp}, IsDisabled={info.IsDisabled}, Status={ni.OperationalStatus}, DeviceId={info.DeviceId}");

            if (!isDisabled)
            {
                try
                {
                    var ipProperties = ni.GetIPProperties();

                    // Get IP addresses (IPv4 only)
                    info.IpAddresses = ipProperties.UnicastAddresses
                        .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(addr => addr.Address.ToString())
                        .ToList();

                    // Get Subnet Masks (IPv4 only)
                    info.SubnetMasks = ipProperties.UnicastAddresses
                        .Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(addr => addr.IPv4Mask.ToString())
                        .ToList();

                    // Get Gateway addresses
                    info.GatewayAddresses = ipProperties.GatewayAddresses
                        .Where(gw => gw.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(gw => gw.Address.ToString())
                        .ToList();

                    // Check Gateway reachability and latency
                    foreach (var gateway in info.GatewayAddresses)
                    {
                        var (isReachable, latency) = PingGateway(gateway);
                        info.GatewayReachable.Add(isReachable);
                        info.GatewayLatency.Add(latency);
                    }

                    // Get DNS addresses
                    info.DnsAddresses = ipProperties.DnsAddresses
                        .Where(dns => dns.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(dns => dns.ToString())
                        .ToList();

                    // Check DNS reachability
                    foreach (var dnsServer in info.DnsAddresses)
                    {
                        bool isReachable = CheckDnsReachability(dnsServer);
                        info.DnsReachable.Add(isReachable);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting IP config: {ex.Message}");
                }
            }

            return info;
        }

        private bool CheckDnsReachability(string dnsServer)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    // Try to connect to DNS port 53 with 1 second timeout
                    var result = client.BeginConnect(dnsServer, 53, null, null);
                    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                    if (success)
                    {
                        client.EndConnect(result);
                        System.Diagnostics.Debug.WriteLine($"DNS {dnsServer} is reachable");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"DNS {dnsServer} is not reachable (timeout)");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DNS {dnsServer} is not reachable: {ex.Message}");
                return false;
            }
        }

        private (bool isReachable, long latency) PingGateway(string gateway)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = ping.Send(gateway, 1000); // 1 second timeout

                    if (reply.Status == IPStatus.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Gateway {gateway} is reachable: {reply.RoundtripTime} ms");
                        return (true, reply.RoundtripTime);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Gateway {gateway} ping failed: {reply.Status}");
                        return (false, -1);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gateway {gateway} ping error: {ex.Message}");
                return (false, -1);
            }
        }

        private bool IsAdapterDisabled(string adapterName)
        {
            try
            {
                var query = $"SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID = '{adapterName}'";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject adapter in searcher.Get())
                {
                    var configErrorCode = adapter["ConfigManagerErrorCode"];
                    if (configErrorCode is uint errorCode)
                    {
                        // 22 = Device is disabled
                        return errorCode == 22;
                    }
                }
            }
            catch { }

            return false;
        }

        private uint GetInterfaceIndex(string name, string description)
        {
            try
            {
                // Try to get InterfaceIndex from Win32_NetworkAdapter
                var query = $"SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionID = '{name}'";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject adapter in searcher.Get())
                {
                    var index = adapter["InterfaceIndex"];
                    if (index != null)
                    {
                        return Convert.ToUInt32(index);
                    }
                }

                // Try with Description
                query = $"SELECT * FROM Win32_NetworkAdapter WHERE Name LIKE '%{description.Replace("'", "''")}%'";
                using var searcher2 = new ManagementObjectSearcher(query);

                foreach (ManagementObject adapter in searcher2.Get())
                {
                    var index = adapter["InterfaceIndex"];
                    if (index != null)
                    {
                        return Convert.ToUInt32(index);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting InterfaceIndex: {ex.Message}");
            }

            return 0;
        }

        private string GetMacAddress(NetworkInterface adapter)
        {
            var mac = adapter.GetPhysicalAddress().ToString();
            if (string.IsNullOrEmpty(mac))
                return "N/A";

            // Format as XX-XX-XX-XX-XX-XX
            return string.Join("-", Enumerable.Range(0, mac.Length / 2)
                .Select(i => mac.Substring(i * 2, 2)));
        }

        private string GetStatusString(OperationalStatus status, bool isDisabled)
        {
            if (isDisabled)
                return "Disabled";

            return status switch
            {
                OperationalStatus.Up => "Connected",
                OperationalStatus.Down => "Disconnected",
                OperationalStatus.Dormant => "Inactive",
                OperationalStatus.NotPresent => "Not Present",
                OperationalStatus.LowerLayerDown => "Disconnected",
                _ => "Unknown"
            };
        }

        public bool EnableAdapter(uint deviceId)
        {
            if (deviceId == 0)
            {
                System.Diagnostics.Debug.WriteLine("Cannot enable adapter: DeviceId is 0");
                return false;
            }

            bool result = SetAdapterState(deviceId, enable: true);

            // Wait for adapter to change state
            if (result)
            {
                Thread.Sleep(2000); // Wait 2 seconds for adapter to enable
            }

            return result;
        }

        public bool DisableAdapter(uint deviceId)
        {
            if (deviceId == 0)
            {
                System.Diagnostics.Debug.WriteLine("Cannot disable adapter: DeviceId is 0");
                return false;
            }

            bool result = SetAdapterState(deviceId, enable: false);

            // Wait for adapter to change state
            if (result)
            {
                Thread.Sleep(1000); // Wait 1 second for adapter to disable
            }

            return result;
        }

        private bool SetAdapterState(uint deviceId, bool enable)
        {
            try
            {
                // Use Win32_NetworkAdapter with InterfaceIndex
                var query = $"SELECT * FROM Win32_NetworkAdapter WHERE InterfaceIndex = {deviceId}";
                using var searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject adapter in searcher.Get())
                {
                    var name = adapter["NetConnectionID"]?.ToString() ?? adapter["Name"]?.ToString() ?? "Unknown";
                    System.Diagnostics.Debug.WriteLine($"{(enable ? "Enabling" : "Disabling")} adapter: {name} (InterfaceIndex: {deviceId})");

                    var methodName = enable ? "Enable" : "Disable";
                    var result = adapter.InvokeMethod(methodName, null);

                    System.Diagnostics.Debug.WriteLine($"{methodName} method invoked, returned: {result}");

                    return true;
                }

                System.Diagnostics.Debug.WriteLine($"No adapter found with InterfaceIndex {deviceId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting adapter state (InterfaceIndex: {deviceId}): {ex.Message}");
            }

            return false;
        }
    }
}
