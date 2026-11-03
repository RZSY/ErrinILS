# AGENTS.md - ErrinILS Developer & Agent Guide

Welcome to **ErrinILS** (School Integrated Library System). This guide provides AI agents and developers with essential context, architectural rules, build/test commands, and conventions for working effectively in this repository.

---

## 1. Project Overview

- **Name**: ErrinILS
- **Description**: Lightweight, zero-dependency school library system (ILS) with cataloguing, circulation, patron management, copy-cataloguing (ISBN lookup via Z39.50/APIs), and student self-checkout kiosk mode.
- **Target OS**: Windows 7, 8, 8.1, 10, and 11 (32-bit and 64-bit).
- **Runtime**: Microsoft .NET Framework 4.0 or newer (built into Windows 8+; Windows 7 requires .NET 4.8).
- **Core Philosophy**:
  - **Zero external dependencies**: Pure C# using only base .NET Framework 4.0 assemblies (`mscorlib`, `System`, `System.Core`, `System.Drawing`, `System.Windows.Forms`). No NuGet packages or third-party DLLs.
  - **Single standalone binary**: `ErrinILS.exe` runs directly without an installation dependency chain.
  - **Single JSON database**: Data persists in `%APPDATA%\Errin\errin-data.json`. No separate database server required.
  - **Low-resource footprint**: Optimized for low-end school PCs (1 GB RAM, ~10 MB disk).
  - **Localized for schools**: Supports Malaysian IC / MyKid student identification numbers (`YYMMDD-PB-###G`), Ringgit Malaysia (RM) fines calculation, and child-friendly UI phrasing.

---

## 2. Repository Layout

```text
d:/errin/
├── AGENTS.md                     # This guide for agents and contributors
├── README.md                     # User-facing markdown readme
├── README.txt                    # Plain text user manual and installation guide
├── LICENSE                       # MIT License
├── ErrinILS-Windows/             # Distribution artifacts
│   └── ErrinILS/
│       ├── ErrinILS.exe          # Pre-compiled standalone executable
│       └── README.txt            # Portable copy manual
└── ErrinILS-source/              # Source tree & build tools
    ├── icon.ico                  # Application icon (multi-resolution Win32 icon)
    ├── icon.png                  # High-resolution PNG logo
    ├── build.sh                  # Linux / Mono build script
    ├── installer.nsi             # NSIS (Nullsoft) Windows installer script
    ├── README.txt                # Source-level notes
    ├── src/                      # C# application source files
    │   ├── AssemblyInfo.cs       # Assembly metadata, product info, version
    │   ├── Auth.cs               # Password hashing (PBKDF2/SHA256), salt, auth verification
    │   ├── Dialogs.cs            # WinForms dialogs (Item edit, Patron edit, Barcode printing)
    │   ├── Json.cs               # Zero-dependency JSON parser and serializer
    │   ├── Lib.cs                # Core circulation rules, loan logic, fines, IC validation
    │   ├── Lookup.cs             # Multi-source ISBN copy-cataloguing (LOC, OpenLib, Google, Hathi, K10plus)
    │   ├── MainForm.cs           # Primary WinForms interface (Circulation, Catalog, Patrons, Reports)
    │   ├── Marc.cs               # MARC21 parser & generator (ISO 2709 .mrc, MARCXML, MRK, MARC-8)
    │   ├── NetHelper.cs          # Network interface discovery, hotspot IP filtering, firewall integration
    │   ├── Qr.cs                 # Zero-dependency QR code generator (ISO/IEC 18004, Levels 1-10, EC-M)
    │   ├── Models.cs             # Data models (Item, Patron, Loan, Hold, Fine, Db, Settings)
    │   ├── Program.cs            # Single-instance mutex & application entry point
    │   ├── SelfTest.cs           # Automated self-tests (MARC, Z39.50, circulation rules, JSON)
    │   ├── Simple.cs             # Simplified student self-checkout / checkin kiosk mode
    │   ├── Ui.cs                 # UI controls, styles, layout, and rendering helpers
    │   ├── Web.cs                # Remote access HTTP server, device pairing, and web SPA
    │   ├── Z3950.cs              # ASN.1 BER encoding/decoding and Z39.50 v3 client & server
    │   └── Zip.cs                # Pure C# ZIP archive writer (DEFLATE) for backups
    └── tools/                    # Diagnostics & interop tools
        ├── ZClient.cs            # Standalone CLI Z39.50 client tool
        ├── ZServer.cs            # Standalone CLI Z39.50 server test stub
        └── yaz-interop.sh        # Bash script to verify Z39.50 interop with IndexData YAZ
```

---

## 3. Architecture & Subsystems

### 3.1 Data Model & Persistence (`Models.cs`, `Json.cs`)
- All data models (`Item`, `Patron`, `Loan`, `Hold`, `Fine`, `Settings`, `Db`) are plain C# objects.
- `Db.ToJson()` / `Db.FromJson()` preserves exact schema compatibility with previous Electron versions of Errin.
- Data directory default:
  - Windows: `%APPDATA%\Errin\errin-data.json`
- Backup / Restore: Handled via `Zip.cs` generating standard ZIP archives containing JSON records and catalog data.

### 3.2 Circulation & Business Rules (`Lib.cs`)
- **Loan Duration**: Default 7 days (`Lib.LOAN`).
- **Fines**: Calculated per overdue day based on `Set.rate` (default RM 0.10/day).
- **Blocking**: Patrons are blocked from borrowing if overdue items exist or unpaid fines exceed `Set.block` (default RM 5.00).
- **Holds**: Held items cannot be renewed or checked out to other patrons.
- **Weeding / Collection Maintenance**: Items can be marked as weeded/withdrawn with reason (condition/damage, obsolete, lost, duplicate) and date. Weeded copies are blocked from checkout and hold requests, while preserving full bibliographic and circulation history. Can be restored to active status at any time or permanently deleted.
- **Data Retention**: `Lib.Purge()` clears patron activity older than 5 years (`Lib.RET_YEARS`).
- **Malaysian IC / MyKid Validation**:
  - `Lib.NormIc(string)`: Normalizes 12-digit strings into `YYMMDD-PB-###G`.
  - `Lib.IcOk(string)`: Validates birth month (01–12) and day valid for the given month.

### 3.3 MARC & Cataloguing Protocols (`Marc.cs`, `Z3950.cs`, `Lookup.cs`)
- **MARC21**: Full support for reading and writing raw binary ISO 2709 (`.mrc`), MARCXML (`.xml`), and human-readable MARC mnemonic (`.mrk`). Handles MARC-8 combining diacritics and UTF-8.
- **Z39.50**: Native ASN.1 Basic Encoding Rules (BER) encoder/decoder (`Ber`) and Z39.50 v3 client/server (`Z`). Supports Init, Search (type-1 query, Use attributes for ISBN/Title/Author/Subject), and Present requests.
- **Copy Cataloguing Chain**: `Lookup.cs` queries 5 free sources in priority order:
  1. Library of Congress (Z39.50 `lx2.loc.gov:210`)
  2. Open Library (HTTP JSON API)
  3. Google Books (HTTP JSON API)
  4. HathiTrust (HTTP JSON / MARCXML)
  5. K10plus German union catalogue (SRU MARCXML)

### 3.5 Remote Access, QR Code Pairing & Device Pairing (`Web.cs`, `Qr.cs`)
- **Device Pairing Model**: Zero-password access control using temporary 6-digit pairing codes (5-minute expiry, 3-attempt lockout) and persistent 32-byte cryptographically secure device tokens (`X-Errin-Token`).
- **QR Code Generation (`Qr.cs`)**: Pure C# zero-dependency ISO/IEC 18004 QR code generator supporting Versions 1 to 10 with Error Correction Level M, rendering sharp `Bitmap`, `ToPng()`, and `ToSvg()` representations. Used in `PairNewDeviceDialog` for instant mobile connection via camera scanning (auto-filling pairing code), in Settings for sharing remote access links, and exposed via HTTP at `/api/qr` and `/qr.png`.
- **Multi-Network / Multi-Wi-Fi Support**: Pure C# HTTP server (`TcpListener`) binding to `0.0.0.0` (all interfaces: Ethernet, Wi-Fi 2.4/5GHz, VLANs, VPNs, and Tailscale mesh). Allows devices on different school Wi-Fi SSIDs/APs, subnets, or remote networks (via Tailscale or Cloudflare Tunnel) to connect seamlessly.
- **Single Page App (SPA)**: Embedded mobile-friendly web replica covering Dashboard, Circulation, Catalog, Students, Fines, and Reports with instant barcode scanner support.

### 3.6 Reports & Circulation Analytics (`MainForm.cs`, `Web.cs`)
- **Executive Summary & Metrics**: Active copies, active loans, overdue items with percentages, registered students, active reader participation rate, fines assessed/collected/unpaid, and weeded items.
- **Circulation Trends & Rankings**: Checkouts per month over the last 6 months, top 10/25 borrowed titles, and Reading Champions (Top student reader leaderboard by books read).
- **Collection Classification**: Breakdown of cataloged books by Dewey Decimal Classification (DDC) 100s and fiction/subject categories.
- **Dormant & Weeding Analysis**: Tracks titles with zero circulation (dormant collection) and categorizes weeded items by withdrawal reason (condition/damage, obsolete, lost, duplicate).
- **Export Full Report (CSV)**: One-click export of an executive summary spreadsheet containing all analytics, overdue notices, unpaid fines, and reading champions.

### 3.4 User Interface & High-DPI Sharpness (`MainForm.cs`, `Simple.cs`, `Ui.cs`, `Dialogs.cs`)
- Standard WinForms application with custom styling and flat controls in `Ui.cs`.
- **High-DPI Sharpness**: Native PerMonitorV2 awareness via `Program.cs` and `app.manifest`. Prevents blurry DWM bitmap scaling on modern displays (125%, 150%, 200%), rendering razor-sharp ClearType text and proportional layouts without extra GPU/CPU consumption.
- **Main View**: Tabbed librarian interface with Circulation, Catalog (with Active/Weeded filters and Weeding dialog), Patrons, Overdue Notices, Inventory, Reports, and System Settings.
- **Simplified View (`Simple.cs`)**: Kiosk view tailored for young children with large touch/mouse targets, barcode scanner focus, and friendly error dialogs (`Lib.Friendly(...)`).

---

## 4. Build, Test, and Packaging Instructions

### 4.1 Native Windows Build (Using .NET Framework `csc.exe`)

The codebase targets .NET Framework 4.0 and C# 5 syntax. It can be built directly with Microsoft's built-in C# compiler without requiring Visual Studio:

**Compiler Path**:
```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

#### Build Standalone Application:
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /target:winexe `
    /optimize+ `
    /debug- `
    /win32icon:ErrinILS-source\icon.ico `
    /r:System.Drawing.dll,System.Windows.Forms.dll `
    /out:dist\ErrinILS.exe `
    ErrinILS-source\src\Json.cs `
    ErrinILS-source\src\Models.cs `
    ErrinILS-source\src\Marc.cs `
    ErrinILS-source\src\Z3950.cs `
    ErrinILS-source\src\Lib.cs `
    ErrinILS-source\src\Lookup.cs `
    ErrinILS-source\src\Zip.cs `
    ErrinILS-source\src\Auth.cs `
    ErrinILS-source\src\NetHelper.cs `
    ErrinILS-source\src\Qr.cs `
    ErrinILS-source\src\AssemblyInfo.cs `
    ErrinILS-source\src\Ui.cs `
    ErrinILS-source\src\Simple.cs `
    ErrinILS-source\src\Web.cs `
    ErrinILS-source\src\MainForm.cs `
    ErrinILS-source\src\Dialogs.cs `
    ErrinILS-source\src\Program.cs
```

#### Build and Run Self-Tests:
```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /target:exe `
    /r:System.Drawing.dll `
    /out:selftest.exe `
    ErrinILS-source\src\Json.cs `
    ErrinILS-source\src\Models.cs `
    ErrinILS-source\src\Marc.cs `
    ErrinILS-source\src\Z3950.cs `
    ErrinILS-source\src\Lib.cs `
    ErrinILS-source\src\Lookup.cs `
    ErrinILS-source\src\Zip.cs `
    ErrinILS-source\src\Auth.cs `
    ErrinILS-source\src\NetHelper.cs `
    ErrinILS-source\src\Qr.cs `
    ErrinILS-source\src\Web.cs `
    ErrinILS-source\src\SelfTest.cs

.\selftest.exe
```
*(Note: If running on Windows, ensure the test environment can write temporary files or verify the path in `SelfTest.cs` line 73).*

### 4.2 Linux / macOS Build (Using Mono)
```bash
cd ErrinILS-source
chmod +x build.sh
./build.sh
```
Requires `mono-mcs`, `mono-devel`, and `nsis`.

### 4.3 Building NSIS Installer
```cmd
makensis -V2 -DVERSION=1.2.3 ErrinILS-source\installer.nsi
```

---

## 5. Development Guidelines & Constraints for AI Agents

1. **Strict Zero-Dependency Rule**:
   - Never add NuGet package references or third-party assemblies.
   - All protocol, parser, or encryption needs must be implemented using existing standard .NET 4.0 libraries (`mscorlib`, `System`, `System.Core`, `System.Drawing`, `System.Windows.Forms`).

2. **C# 5 Language Compatibility**:
   - The primary compiler is .NET Framework 4.0 / C# 5 (`csc.exe` v4.8).
   - **Do not** use C# 6+ language features:
     - No string interpolation (`$"Hello {name}"` -> use `string.Format` or concatenation).
     - No null-conditional operator (`obj?.Prop` -> use explicit null checks).
     - No expression-bodied members (`int Prop => 42;` -> use full getters).
     - No `nameof(...)` operator.
     - No auto-property initializers (`public int X { get; set; } = 1;`).

3. **Data Schema Backward Compatibility**:
   - Do not rename or alter existing JSON fields in `errin-data.json` without fallback logic in `Db.FromJson()`. Schools preserve data across upgrades.

4. **Self-Contained File Structure**:
   - Keep business logic in `Lib.cs`, protocols in `Marc.cs` / `Z3950.cs` / `Lookup.cs`, and UI in `Ui.cs` / `MainForm.cs` / `Dialogs.cs` / `Simple.cs`.
   - Ensure `SelfTest.cs` passes whenever modifying core logic or data schemas.

5. **Cross-Platform Building**:
   - Ensure file path handling in scripts and tests works cleanly across both Windows and Mono on Linux.
