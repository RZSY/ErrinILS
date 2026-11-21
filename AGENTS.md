# AGENTS.md - ErrinILS Developer & Agent Guide

Welcome to **ErrinILS** (School Integrated Library System). This guide provides AI agents and developers with essential context, architectural rules, build/test commands, and conventions for working effectively in this repository.

---

## 1. Project Overview

- **Name**: ErrinILS
- **Description**: Lightweight, zero-dependency school library system (ILS) with cataloguing, circulation, patron management, copy-cataloguing (ISBN lookup via Z39.50/APIs), and Simplified View.
- **Target OS**: Windows 7, 8, 8.1, 10, and 11 (32-bit and 64-bit).
- **Runtime**: Microsoft .NET Framework 4.0 or newer (built into Windows 8+; Windows 7 requires .NET 4.8).
- **Core Philosophy**:
  - **Zero external dependencies**: Pure C# using only base .NET Framework 4.0 assemblies (`mscorlib`, `System`, `System.Core`, `System.Drawing`, `System.Windows.Forms`). No NuGet packages or third-party DLLs.
  - **Single standalone binary**: `ErrinILS.exe` runs directly without an installation dependency chain.
  - **Single SQLite database**: Data persists in `%APPDATA%\Errin\errin-data.db`. High-performance ACID storage with WAL journaling, relational tables, and automatic backward-compatible migration from legacy JSON databases (`errin-data.json`).
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
├── ErrinILS.sln                  # Visual Studio 2026 solution
├── ErrinILS-Windows/             # Distribution artifacts
│   └── ErrinILS/
│       ├── ErrinILS.exe          # Pre-compiled standalone executable
│       └── README.txt            # Portable copy manual
└── ErrinILS-source/              # Source tree & build tools
    ├── ErrinILS.csproj           # WinForms main application project
    ├── SelfTest.csproj           # Automated test project
    ├── build.bat                 # Windows batch build script (debug/release/selftest)
    ├── icon.ico                  # Application icon (multi-resolution Win32 icon)
    ├── icon.png                  # High-resolution PNG logo
    ├── build.sh                  # Linux / Mono build script
    ├── installer.nsi             # NSIS (Nullsoft) Windows installer script
    ├── README.txt                # Source-level notes
    ├── src/                      # C# application source files
    │   ├── AssemblyInfo.cs       # Assembly metadata, product info, version
    │   ├── Auth.cs               # Password hashing (PBKDF2/SHA256), salt, auth verification
    │   ├── CloudSync.cs          # Zero-dependency cloud sync manager for Google Drive (users) & GitHub (catalog)
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
    │   ├── Simple.cs             # Simplified circulation view (Simplified View)
    │   ├── Sqlite.cs             # Zero-dependency SQLite interface and high-performance persistence store
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

### 3.1 Data Model & Persistence (`Models.cs`, `Sqlite.cs`, `Json.cs`)
- All data models (`Item`, `Patron`, `Loan`, `Hold`, `Fine`, `Settings`, `Db`) are plain C# objects.
- Primary storage engine is SQLite (`Sqlite.cs`), persisting data to `%APPDATA%\Errin\errin-data.db` with relational tables (`items`, `patrons`, `loans`, `holds`, `fines`, `settings`, `class_history`, `archived_patrons`), indexed lookups, WAL journal mode, and atomic transactions.
- Zero-dependency native SQLite loading dynamically binds to Windows' built-in `winsqlite3.dll` (Windows 10/11) or `sqlite3.dll` without NuGet dependencies.
- Automatic Migration: On first launch without `errin-data.db`, existing `errin-data.json` files are automatically parsed and migrated into the SQLite database, preserving a `.migrated` copy.
- `Db.ToJson()` / `Db.FromJson()` preserves exact schema compatibility with previous Electron versions of Errin for Web API responses and JSON export/import.
- Data directory default:
  - Windows: `%APPDATA%\Errin\errin-data.db`
- Backup / Restore: Supports native SQLite database backups (`.db`) and JSON exports (`.json`).
- **Database Optimization (`SqliteStore.Optimize`)**: Built-in "Optimize Database" utility (accessible from Backup and Settings) executing `PRAGMA integrity_check;`, `PRAGMA optimize;`, and `VACUUM;` to reclaim unused disk space, defragment the SQLite database, and verify file integrity.
- Configuration: `%APPDATA%\Errin\errin-config.json` stores administrator password hash, Z39.50 settings, and paired device tokens.
- **Admin Password**: Protects Settings, record deletion, and database restoration. Stored as salted PBKDF2/SHA-256 (`Auth.cs`). Factory default password is `admin123` (SHA-256 hash `f16959ae2d027f8562807b5d1dbbef305556f3fd4caaac6ac82679b33397ad72`). Deleting `errin-config.json` resets the password to `admin123`.

### 3.2 Circulation & Business Rules (`Lib.cs`)
- **Loan Duration**: Default 7 days (`Lib.LOAN`).
- **Fines**: Calculated per overdue day based on `Set.rate` (default RM 0.10/day).
- **Blocking**: Patrons are blocked from borrowing if overdue items exist or unpaid fines exceed `Set.block` (default RM 5.00).
- **Holds**: Held items cannot be renewed or checked out to other patrons.
- **Weeding / Collection Maintenance**: Items can be marked as weeded/withdrawn with reason (condition/damage, obsolete, lost, duplicate) and date. Weeded copies are blocked from checkout and hold requests, while preserving full bibliographic and circulation history. Can be restored to active status at any time or permanently deleted.
- **Data Retention & Archive**: `Lib.Purge()` safely deactivates student records with no activity for 6 years (`Lib.RET_YEARS`), moving them into a password-protected Archive section under Reports. Preserves student names, registration dates, borrowing history, and fines—records are never deleted permanently. Once deactivated, students are automatically excluded from leaderboards (Reading Champions) and active circulation. Archived students can be restored to active status at any time.
- **Academic Class Year Archiving**: Captures snapshots of class rosters and borrowing counts on December 31st (and anytime via manual snapshot), accessible under the Previous Years tab in Reports to preserve historical class cohorts.
- **Batch Student Registration**: Imports students from Excel (`.xlsx`) or delimited files (`.csv`, `.tsv`, `.txt`) using a zero-dependency OpenXML parser (`ExcelReader`). Primary key is IC number: duplicate IC numbers update existing student details (name, class, contact) rather than creating duplicates.
- **Malaysian IC / MyKid Validation**:
  - `Lib.NormIc(string)`: Normalizes 12-digit strings into `YYMMDD-PB-###G`.
  - `Lib.IcOk(string)`: Validates birth month (01–12) and day valid for the given month.

### 3.3 MARC & Cataloguing Protocols (`Marc.cs`, `Z3950.cs`, `Lookup.cs`)
- **MARC21**: Full support for reading and writing raw binary ISO 2709 (`.mrc`), MARCXML (`.xml`), and human-readable MARC mnemonic (`.mrk`). Handles MARC-8 combining diacritics and UTF-8.
- **Z39.50**: Native ASN.1 Basic Encoding Rules (BER) encoder/decoder (`Ber`) and Z39.50 v3 client/server (`Z`). Supports Init, Search (type-1 query, Use attributes for ISBN/Title/Author/Subject), and Present requests.
- **Copy Cataloguing Chain**: `Lookup.cs` queries 31 free sources in priority order, including all Malaysian national union, state public libraries, academic union, top universities, and heritage institutes (from `marc-retriever`):
  1. Pustaka Terbuka Union (Local cloned sibling repositories & live Pustaka Terbuka Union OPAC alias shards)
  2. Katalog Induk Kebangsaan (KIK - National Union Catalog)
  3. Perpustakaan Negara Malaysia (PNM - Clarivate Vega Discover API)
  4. Perbadanan Perpustakaan Awam Selangor (PPAS - Clarivate Vega Discover API)
  5. Perbadanan Perpustakaan Awam Pulau Pinang (PPAP - Koha OPAC)
  6. Perbadanan Perpustakaan Awam Negeri Perak (PPANPK - Koha OPAC)
  7. Perbadanan Perpustakaan Awam Negeri Kedah (PPANK - Koha OPAC)
  8. Perbadanan Perpustakaan Awam Negeri Perlis (PPANPS - Koha OPAC)
  9. Perbadanan Perpustakaan Awam Negeri Johor (PPANJ)
  10. Perbadanan Perpustakaan Awam Kelantan (PPAK)
  11. Perbadanan Perpustakaan Awam Terengganu (PPAT)
  12. Perbadanan Perpustakaan Awam Pahang (PPAPH)
  13. Perbadanan Perpustakaan Awam Negeri Sembilan (PPANS)
  14. Perbadanan Perpustakaan Awam Melaka (PERPUSTAM)
  15. Pustaka Negeri Sarawak
  16. Perpustakaan Negeri Sabah (PNS)
  17. Perpustakaan Kuala Lumpur (PKL)
  18. Perpustakaan Awam Putrajaya (PPJ)
  19. Perpustakaan Awam Perbadanan Labuan (PPL)
  20. u-Pustaka (National Information Hub & Consortium)
  21. MALCat (Malaysian Academic Library Union Catalog - PERPUN / JKPUA)
  22. Universiti Malaya (UM - Pendeta Discovery)
  23. Universiti Kebangsaan Malaysia (UKM - Tun Seri Lanang Library)
  24. Universiti Teknologi MARA (UiTM - PTAR Network)
  25. Universiti Pendidikan Sultan Idris (UPSI - Education & Pedagogy)
  26. Dewan Bahasa dan Pustaka (DBP - Language & Literature Archives)
  27. Library of Congress (Z39.50 `lx2.loc.gov:210`)
  28. Open Library (HTTP JSON API)
  29. Google Books (HTTP JSON API)
  30. HathiTrust (HTTP JSON / MARCXML)
  31. K10plus German union catalogue (SRU MARCXML)
- **Batch ISBN Lookup**: High-throughput multi-ISBN copy cataloguing designed for book donations and bulk cataloguing (`BatchLookupDialog`). Supports pasting ISBN lists, scanning barcodes continuously, or loading files, with live multithreaded lookups and bulk catalog additions.
- **Item Circulation History**: Full historical borrowing lens for any item (`ItemHistoryDialog`), detailing which students borrowed it, loan dates, due dates, return timestamps, and overdue metrics.

### 3.5 Remote Access & OPAC (`Web.cs`, `Qr.cs`, `Dialogs.cs`)
- **Device Pairing Model**: Zero-password access control using temporary 6-digit pairing codes (5-minute expiry, 3-attempt lockout) and persistent 32-byte cryptographically secure device tokens (`X-Errin-Token`).
- **QR Code Generation (`Qr.cs`)**: Pure C# zero-dependency ISO/IEC 18004 QR code generator supporting Versions 1 to 10 with Error Correction Level M, rendering sharp `Bitmap`, `ToPng()`, and `ToSvg()` representations. Used in `PairNewDeviceDialog` for instant mobile connection via camera scanning (auto-filling pairing code), in Settings for sharing remote access links, and exposed via HTTP at `/api/qr` and `/qr.png`.
- **Online Public Access Catalog (OPAC)**: Public-facing catalog served at `/opac` using the same pure C# HTTP server (`Web.cs`). Matches desktop `MainForm` visual theme (charcoal `#232427`, yellow accents `#f6c61b`, royal blue logo, left-border card styling). Enables searching by title, author, call number, and dedicated **Subject Heading** search, with multi-token keyword filters and subject topic exploration.
- **OPAC Patron Login**: Optional student login (disabled by default, toggled in Settings `db.Set.opacLogin`). When enabled, students can sign in using their Student ID or Malaysian IC (`no_pengenalan`) to check current loans, due dates, fines, reading history, and place holds on borrowed copies.
- **Multi-Network / Multi-Wi-Fi Support**: Pure C# HTTP server (`TcpListener`) binding to `0.0.0.0` (all interfaces: Ethernet, Wi-Fi 2.4/5GHz, VLANs, VPNs, and Tailscale mesh). Allows devices on different school Wi-Fi SSIDs/APs, subnets, or remote networks (via Tailscale or Cloudflare Tunnel) to connect seamlessly.
- **Single Page App (SPA)**: Embedded mobile-friendly web replica covering Dashboard, Circulation, Catalog (with Subject Heading search), Students, Fines, and Reports with instant barcode scanner support.

### 3.6 Reports & Circulation Analytics (`MainForm.cs`, `Web.cs`)
- **Executive Summary & Metrics**: Active copies, active loans, overdue items with percentages, registered students, active reader participation rate, fines assessed/collected/unpaid, and weeded items.
- **Circulation Trends & Rankings**: Checkouts per month over the last 6 months, top 10/25 borrowed titles, and Reading Champions (Top student reader leaderboard by books read, filtering for active students only).
- **Collection Classification**: Breakdown of cataloged books by Dewey Decimal Classification (DDC) 100s and fiction/subject categories.
- **Dormant & Weeding Analysis**: Tracks titles with zero circulation (dormant collection) and categorizes weeded items by withdrawal reason (condition/damage, obsolete, lost, duplicate).
- **Reports Sub-views**:
  - **Analytics**: Real-time executive metrics, circulation trends, reader leaderboard, and Dewey breakdown.
  - **Previous Years (Classes)**: Historical class rosters and borrowing stats captured annually on December 31st or on demand.
  - **Inactive Archive**: Secure, password-protected archive housing student borrowing records preserved beyond the 5-year retention limit.
- **Export Full Report (CSV)**: One-click export of an executive summary spreadsheet containing all analytics, overdue notices, unpaid fines, and reading champions.

### 3.4 User Interface & High-DPI Sharpness (`MainForm.cs`, `Simple.cs`, `Ui.cs`, `Dialogs.cs`)
- Standard WinForms application with custom styling and flat controls in `Ui.cs`.
- **High-DPI Sharpness**: Native PerMonitorV2 awareness via `Program.cs` and `app.manifest`. Prevents blurry DWM bitmap scaling on modern displays (125%, 150%, 200%), rendering razor-sharp ClearType text and proportional layouts without extra GPU/CPU consumption.
- **Main View**: Tabbed librarian interface with Circulation, Catalog (with Active/Weeded filters and Weeding dialog), Patrons, Overdue Notices, Inventory, Reports, and System Settings.
- **Grid Paging & Low-Memory Optimization (`PagerBar`)**: Slices records on Catalog (`catGrid`) and Students (`patGrid`) with customizable page sizes (50, 100, 250, Show All), Previous/Next controls, page selector, and slice metrics. Keeps UI responsive and RAM usage near zero even with 10,000+ items on 1–2 GB RAM school hardware.
- **Simplified View (`Simple.cs`)**: Child-friendly circulation kiosk tailored for young students with large colorful touch/mouse targets, cheerful emoji badges, high-contrast friendly banner boxes, barcode scanner focus, and friendly error dialogs (`Lib.Friendly(...)`).

### 3.7 Cloud Sync & Multi-PC Operations (`CloudSync.cs`, `MainForm.cs`)
- **Dual Cloud Architecture**:
  - **User Records via Google Drive (Zero-Dependency Direct Cloud)**: Student records (Patrons, Loans, Holds, Fines, Class History, Archived Patrons, Settings) contain sensitive personal data (MyKid/IC, addresses, contact details, loan history). They are automatically saved and synced privately to Google Drive without requiring any 3rd party desktop application, using direct HTTP calls to the Google Drive v3 REST API (supporting Service Account key or OAuth token). Configurable for any school Google account (defaulting to `tamanria.sk@gmail.com`). Also falls back to local Google Drive folder if available.
  - **Books & Catalog Data via GitHub (`pustaka-terbuka/sk-taman-ria`)**: Bibliographic records are public domain/open data. The repository strictly conforms to the **Pustaka Terbuka Data Provider Standard** (PPAPP model) enabling central indexing by `pustaka-terbuka/opac`:
    - `data/sources.json` & `data/orgs.json`: Source code (`sk-taman-ria`), organization prefix (`SKTR`), institution metadata.
    - `data/aliases.jsonl`: Canonical lexicographically sorted alias index (ISBN, 035, and fallback keys -> Crockford Base32 record IDs).
    - `data/bib/<shard>/<id>.xml`: Partitioned single-record MARCXML files with `manifest.json` SHA-256 hashes.
    - `data/holdings/SKTR.csv`: Institutional holdings table.
    - `harvest/inbox/sk-taman-ria/<run-id>/`: Automated intake batches with `manifest.json`, `partition_state.json`, `provenance.jsonl`, and `records/batch_0.xml`.
    - `tools/` & `tests/`: Zero-dependency and Node.js validation (`validate-inbox.js`, `merge.js`).
    - Convenience root exports: `catalog.json`, `holdings.csv`, `catalog.mrc` (binary MARC 21), `catalog.mrk` (MarcEdit mnemonic), `catalog.xml` (MARCXML), and `README.md`.
    - **Dual Push Mode (Git CLI or Direct HTTPS REST API)**: If Git for Windows is installed, ErrinILS uses standard `git.exe` commands. If Git is NOT installed (common on school netbooks), ErrinILS can still push commits, file trees, and blobs directly to GitHub over HTTPS using the GitHub Git Database REST API (`PushViaApi`), requiring only a GitHub Personal Access Token (PAT).
- **Multi-PC Parity**: Installing ErrinILS on any other PC connects to Google Drive and pulls `pustaka-terbuka/sk-taman-ria`. Both PCs retrieve identical student and catalog records, allowing seamless multi-station circulation and cataloging.
- **Auto-Sync & Offline Resilience**: Edits immediately trigger non-blocking background Google Drive updates. Catalog updates can be synchronized with one click ("Sync with GitHub") or fully unified in Settings ("Sync Everything Now"). Full functionality remains offline if the internet or cloud drives are temporarily unavailable.

---

## 4. Build, Test, and Packaging Instructions

### 4.1 Native Windows Build (Using .NET Framework `csc.exe`)

The codebase targets .NET Framework 4.0 and C# 5 syntax. It can be built directly with Microsoft's built-in C# compiler without requiring Visual Studio:

**Compiler Path**:
```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

#### Visual Studio 2026 Enterprise / MSBuild:
Open [`ErrinILS.sln`](file:///d:/GitHub/erinils/ErrinILS.sln) directly in Microsoft Visual Studio 2026 Enterprise.
- Press **`F5`** to build and start debugging `ErrinILS`.
- Set configuration to `Debug` (outputs to `bin\Debug\ErrinILS.exe`) or `Release` (outputs to `dist\ErrinILS.exe`).
- Right-click `SelfTest` project -> **Debug** -> **Start New Instance** to run the test suite.
- Or build from command line:
  ```cmd
  msbuild ErrinILS.sln /p:Configuration=Debug
  ```

#### Running via F5 in Antigravity IDE:
Pressing **`F5`** in Antigravity IDE executes `run.ps1` via `.vscode/launch.json`, automatically compiling `bin\Debug\ErrinILS.exe` and launching the application without licensing blocks.

*(Note: Microsoft's proprietary debugger `vsdbg` restricts line-by-line debugging to official Microsoft Visual Studio and Microsoft VS Code distributions per https://aka.ms/VSCode-DotNet-DbgLicense. For full interactive debugging with breakpoints, call stacks, and diagnostics, use Microsoft Visual Studio 2026 Enterprise via `ErrinILS.sln`).*

You can also run builds directly using the Windows batch script:
```cmd
ErrinILS-source\build.bat debug     # Builds bin\Debug\ErrinILS.exe with debug symbols & PDB
ErrinILS-source\build.bat release   # Builds dist\ErrinILS.exe (optimized release)
ErrinILS-source\build.bat selftest  # Builds bin\Debug\selftest.exe
run.ps1 -Target app                 # Builds and launches ErrinILS
run.ps1 -Target selftest            # Builds and executes test suite
```

#### Manual PowerShell Build (Release):
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
    ErrinILS-source\src\Sqlite.cs `
    ErrinILS-source\src\Marc.cs `
    ErrinILS-source\src\Z3950.cs `
    ErrinILS-source\src\Lib.cs `
    ErrinILS-source\src\Lookup.cs `
    ErrinILS-source\src\Zip.cs `
    ErrinILS-source\src\Auth.cs `
    ErrinILS-source\src\CloudSync.cs `
    ErrinILS-source\src\NetHelper.cs `
    ErrinILS-source\src\Qr.cs `
    ErrinILS-source\src\CertHelper.cs `
    ErrinILS-source\src\Updater.cs `
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
    ErrinILS-source\src\Sqlite.cs `
    ErrinILS-source\src\Marc.cs `
    ErrinILS-source\src\Z3950.cs `
    ErrinILS-source\src\Lib.cs `
    ErrinILS-source\src\Lookup.cs `
    ErrinILS-source\src\Zip.cs `
    ErrinILS-source\src\Auth.cs `
    ErrinILS-source\src\CloudSync.cs `
    ErrinILS-source\src\NetHelper.cs `
    ErrinILS-source\src\Qr.cs `
    ErrinILS-source\src\CertHelper.cs `
    ErrinILS-source\src\Updater.cs `
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
makensis -V2 -DVERSION=1.3.3 ErrinILS-source\installer.nsi
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
