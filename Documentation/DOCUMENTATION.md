# System Info - Server Edition
## Technische Dokumentation

**Version:** 2.0.26.3
**Autor:** Miloch
**Datum:** März 2026
**Lizenz:** Freeware

---

## Inhaltsverzeichnis

1. [Projektübersicht](#projektübersicht)
2. [Architektur](#architektur)
3. [Dateistruktur](#dateistruktur)
4. [Komponenten](#komponenten)
5. [Netzwerk-Funktionalität](#netzwerk-funktionalität)
6. [System-Informationen](#system-informationen)
7. [Build & Deployment](#build--deployment)
8. [Changelog](#changelog)

---

## Projektübersicht

System Info - Server Edition ist eine Windows System-Tray-Anwendung zur Anzeige von Netzwerk- und Systeminformationen. Die Anwendung wurde speziell für Server-Umgebungen entwickelt und zeigt nur Ethernet-Adapter an (kein WLAN/Bluetooth).

### Hauptmerkmale
- System-Tray-basierte Anwendung
- Echtzeit-Netzwerkinformationen
- Mehrere DNS-Server-Anzeige mit Erreichbarkeitsprüfung
- Gateway-Latenz-Messung
- System-Uptime, RAM und Festplatten-Nutzung
- Dark Theme UI

---

## Architektur

```
┌─────────────────────────────────────────────────────────┐
│                    Program.cs                            │
│              (Application Entry Point)                   │
└─────────────────────────┬───────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────┐
│              TrayApplicationContext.cs                   │
│         (System Tray & Event Management)                 │
│                                                          │
│  - NotifyIcon Management                                 │
│  - 10s Update Timer                                      │
│  - Context Menu (Refresh, About, Exit)                   │
└──────────┬──────────────────────────────┬───────────────┘
           │                              │
           ▼                              ▼
┌──────────────────────┐    ┌─────────────────────────────┐
│  NetworkAdapterManager│    │        SystemInfo.cs         │
│         .cs           │    │   (System Data Collection)   │
│                       │    │                              │
│ - GetAllEthernetAdapters() │ - Computer Name, Domain     │
│ - DNS Reachability Check   │ - OS Version, Uptime        │
│ - Gateway Ping             │ - CPU, RAM, Disk Info       │
└──────────┬───────────┘    └──────────────┬──────────────┘
           │                               │
           ▼                               │
┌──────────────────────┐                   │
│ NetworkAdapterInfo.cs│                   │
│    (Data Model)      │                   │
└──────────┬───────────┘                   │
           │                               │
           └───────────────┬───────────────┘
                           ▼
           ┌───────────────────────────────┐
           │    AdapterTooltipForm.cs      │
           │      (Visual Rendering)       │
           │                               │
           │  - Dark Theme UI              │
           │  - GDI+ Graphics              │
           │  - Fade Animations            │
           │  - Dynamic Height Calculation │
           └───────────────────────────────┘
```

---

## Dateistruktur

```
System Info - Server/
├── Program.cs                    # Anwendungs-Einstiegspunkt
├── TrayApplicationContext.cs     # System-Tray-Verwaltung
├── NetworkAdapterManager.cs      # Netzwerk-Adapter-Logik
├── NetworkAdapterInfo.cs         # Netzwerk-Datenmodell
├── SystemInfo.cs                 # System-Informationen (WMI)
├── AdapterTooltipForm.cs         # Tooltip-Visualisierung
├── AboutForm.cs                  # About-Dialog
├── SystemInfo.csproj         # Projektdatei
├── SystemInfo.sln            # Solution-Datei
├── app.manifest                  # Manifest (keine Admin-Rechte)
├── nuget.config                  # NuGet-Konfiguration
├── README.md                     # Benutzer-Dokumentation
├── DOCUMENTATION.md              # Diese Dokumentation
├── config/
│   └── about.xml                 # About-Dialog Konfiguration (extern anpassbar)
└── icons/
    ├── Appicon.ico               # Anwendungs-Icon
    └── CompanyLogo.png           # Firmenlogo (optional)
```

---

## Komponenten

### Program.cs
- Verhindert mehrfache Instanzen (Mutex)
- Aktiviert High-DPI und visuelle Stile
- Startet `TrayApplicationContext`

### TrayApplicationContext.cs
- Verwaltet das System-Tray-Icon
- **Update-Intervall:** 10 Sekunden
- **Tooltip-Verzögerung:** 800ms
- **Auto-Hide:** 15 Sekunden

### NetworkAdapterManager.cs
Zentrale Klasse für Netzwerk-Operationen:

```csharp
// Holt alle Ethernet-Adapter (ohne WLAN, Bluetooth, Virtual)
public List<NetworkAdapterInfo> GetAllEthernetAdapters()

// DNS-Erreichbarkeitsprüfung (TCP Port 53, 1s Timeout)
private bool CheckDnsReachability(string dnsServer)

// Gateway-Ping mit Latenz-Messung (ICMP, 1s Timeout)
private (bool isReachable, long latency) PingGateway(string gateway)
```

**Adapter-Filter:**
- Ethernet-Typ: `NetworkInterfaceType.Ethernet`
- Ausgeschlossen: VirtualBox, Bluetooth
- Angezeigt: Hyper-V, VMware (ab Version 2.0.26.3)

### NetworkAdapterInfo.cs
Datenmodell für Netzwerk-Adapter:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| Name | string | Adaptername (z.B. "Ethernet", "VLAN_10") |
| Description | string | Hardware-Beschreibung |
| Status | string | Connected/Disconnected/Disabled |
| IpAddresses | List<string> | IPv4-Adressen |
| SubnetMasks | List<string> | Subnetzmasken |
| GatewayAddresses | List<string> | Gateway-Adressen |
| DnsAddresses | List<string> | Alle DNS-Server |
| DnsReachable | List<bool> | Erreichbarkeitsstatus pro DNS |
| GatewayLatency | List<long> | Latenz in ms pro Gateway |
| LinkSpeed | long | Verbindungsgeschwindigkeit (bps) |

### SystemInfo.cs
Sammelt System-Informationen via WMI und Registry:

- Computername, Domain/Workgroup
- OS Caption, DisplayVersion, Build
- System-Uptime
- CPU (Name, Cores, Threads)
- RAM (Total, Used, Free)
- Festplatten (alle Laufwerke)

### AdapterTooltipForm.cs
Visuelle Darstellung mit GDI+:

- **Höhenberechnung:** Dynamisch basierend auf Adaptern, Festplatten und DNS-Einträgen
- **Farbkodierung:**
  - Blau: Verbunden
  - Grün: Status OK
  - Gelb: Warnung (Latenz 20-50ms, Disk 70-85%)
  - Rot: Fehler/Kritisch
  - Grau: Deaktiviert/Getrennt

---

## Netzwerk-Funktionalität

### DNS-Server-Anzeige
Ab Version 2.0.26.1 werden **alle konfigurierten DNS-Server** angezeigt:

```csharp
// AdapterTooltipForm.cs - DrawAdapterInfo()
for (int i = 0; i < info.DnsAddresses.Count; i++)
{
    bool isReachable = i < info.DnsReachable.Count && info.DnsReachable[i];
    string label = i == 0 ? "DNS" : "DNS " + (i + 1);
    DrawInfoLineWithStatus(g, label, info.DnsAddresses[i], yPos, isReachable);
    yPos += 18;
}
```

### DNS-Erreichbarkeitsprüfung
```csharp
// TCP-Verbindung zu Port 53 mit 1s Timeout
using (var client = new TcpClient())
{
    var result = client.BeginConnect(dnsServer, 53, null, null);
    var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
    return success;
}
```

### Gateway-Latenz-Messung
```csharp
// ICMP Ping mit 1s Timeout
using (var ping = new Ping())
{
    var reply = ping.Send(gateway, 1000);
    return (reply.Status == IPStatus.Success, reply.RoundtripTime);
}
```

---

## System-Informationen

### WMI-Abfragen

| Daten | WMI-Klasse | Eigenschaft |
|-------|------------|-------------|
| Computername | Win32_ComputerSystem | Name |
| Domain | Win32_ComputerSystem | Domain |
| OS Caption | Win32_OperatingSystem | Caption |
| Uptime | Win32_OperatingSystem | LastBootUpTime |
| CPU | Win32_Processor | Name, NumberOfCores |
| RAM | Win32_OperatingSystem | TotalVisibleMemorySize |
| Festplatten | Win32_LogicalDisk | Size, FreeSpace |

### Registry-Abfragen
```
HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion
  - DisplayVersion (z.B. "21H2")
  - CurrentBuild
  - UBR (Update Build Revision)
```

---

## Build & Deployment

### Voraussetzungen
- .NET 8.0 SDK
- Visual Studio 2022 oder VS Code

### Build-Befehle

```bash
# Debug Build
dotnet build -c Debug

# Release Build
dotnet build -c Release

# Self-Contained Publish (Windows x64)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### Publish-Ausgabe
```
bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\
├── SystemInfo.exe
├── config/
│   └── about.xml
└── icons/
    ├── Appicon.ico
    └── CompanyLogo.png
```

---

## Changelog

### Version 2.0.26.3 (März 2026)
- **Neu:** Hyper-V und VMware Netzwerk-Adapter werden wieder angezeigt
- **Neu:** About-Dialog Description und E-Mail über `config/about.xml` konfigurierbar
- **Verbessert:** Version wird aus Assembly-Metadaten gelesen

### Version 2.0.26.1 (Januar 2026)
- **Neu:** Alle konfigurierten DNS-Server werden angezeigt (nicht nur der primäre)
- **Neu:** Dynamische Höhenberechnung für mehrere DNS-Einträge
- **Verbessert:** Adapter-Filterung (WLAN, Bluetooth, Virtual ausgeschlossen)
- **Aktualisiert:** Jahr auf 2026

### Version 1.0 Beta
- Gateway-Ping mit Latenz-Messung
- DNS-Erreichbarkeitsprüfung (Port 53 TCP)
- Link-Speed-Anzeige
- RAM-Nutzungsanzeige
- Festplatten-Anzeige mit Fortschrittsbalken
- System-Uptime
- FQDN-Unterstützung für Domains
- About-Dialog
- Dark Theme UI

---

## Lizenz

**FREEWARE**

Diese Software wird kostenlos für private und kommerzielle Nutzung bereitgestellt.
Verwendung, Kopieren und Verteilung sind ohne Einschränkungen erlaubt.

DIE SOFTWARE WIRD "WIE SIE IST" BEREITGESTELLT, OHNE JEGLICHE GARANTIE.
DER AUTOR HAFTET NICHT FÜR SCHÄDEN, DIE DURCH DIE NUTZUNG ENTSTEHEN.

---

© 2026 Miloch
