==============================================================================
                _       ___ _     ____  
  ___ _ __ _ __(_)_ __ |_ _| |   / ___| 
 / _ \ '__| '__| | '_ \ | || |   \___ \ 
|  __/ |  | |  | | | | || || |___ ___) |
 \___|_|  |_|  |_|_| |_|___|_____|____/ 

 ErrinILS - Lightweight School Integrated Library System
 Version:  1.3.2
 License:  MIT Open Source
 Website:  https://rzsy.github.io/ErrinILS/
 GitHub:   https://github.com/RZSY/ErrinILS
==============================================================================

[1] ABOUT ERRINILS
------------------------------------------------------------------------------
ErrinILS is a zero-dependency school library management system (ILS) for
standard Windows computers. Rather than requiring dedicated Linux servers,
MySQL configuration, or web server setups like Koha or SLiMS, ErrinILS runs as
a standalone executable on any PC running Windows 7 or newer.

Key Specifications:
  * Zero external dependencies: Pure C# using base .NET Framework 4.0.
    No NuGet packages, no third-party DLLs, no database server required.
  * Low resource footprint: Runs smoothly on older classroom PCs (1 GB RAM).
  * Offline persistence: All library records reside in a single SQLite database
    (%APPDATA%\Errin\errin-data.db) with built-in backups and automatic migration.
  * High-DPI support: Native PerMonitorV2 awareness for clear text and
    proper layout scaling at 125%, 150%, and 200%.


[2] SYSTEM REQUIREMENTS & HARDWARE SPECIFICATIONS
------------------------------------------------------------------------------
Operating System:     Windows 7 SP1, Windows 8, 8.1, 10, or 11 (32 or 64-bit).
Runtime Engine:       Microsoft .NET Framework 4.0 or newer.
                        - Windows 8, 8.1, 10, and 11: Pre-installed by default.
                        - Windows 7: Requires ".NET Framework 4.8 Runtime"
                          (free one-time install from Microsoft).
Processor (CPU):      1.0 GHz Intel Pentium / AMD x86/x64 or faster.
Memory (RAM):         1 GB RAM minimum (ErrinILS consumes only ~25-30 MB RAM).
Disk Space:           ~10 MB for executable and application data.
Display:              1024 x 600 minimum (1366 x 768 or higher recommended).
Barcode Scanner:      Any standard USB keyboard-wedge barcode scanner.
Printer (Optional):   Standard desktop or label printer for barcodes, spine
                      labels, and patron cards.
Network (Optional):   100% offline for core circulation; Wi-Fi/LAN enabled for
                      wireless mobile QR auditing and 5-source ISBN lookup.


[3] INSTALLATION & QUICK START
------------------------------------------------------------------------------
Choose either the Windows installer or the portable standalone binary:

Option A: Standard Windows Installer (Recommended)
  1. Download ErrinILS-Setup-1.3.2.exe and double-click to launch.
  2. If Windows User Account Control prompts you, click "Yes".
  3. If a blue "Windows protected your PC" (SmartScreen) warning appears,
     click "More info", then click "Run anyway". (This is normal: ErrinILS
     is community-developed open-source software and is not code-signed).
  4. Follow the setup wizard, keep the default path, and click "Finish".
  5. Launch ErrinILS anytime from your Start Menu or Desktop shortcut.

Option B: Portable Standalone (No Installation Required)
  1. Download ErrinILS-Windows.zip or grab the standalone ErrinILS.exe.
  2. Extract or copy the file anywhere (e.g. C:\ErrinILS or a USB flash drive).
  3. Double-click ErrinILS.exe to run immediately.

Upgrading:
  Run the new installer over the old version or replace ErrinILS.exe. All
  library records and configuration remain completely untouched.

Uninstalling:
  Windows Settings > Apps (or Control Panel > Programs) > ErrinILS > Uninstall.
  Your database (%APPDATA%\Errin) is preserved to prevent accidental data loss.


[4] DEFAULT CREDENTIALS & SECURITY
------------------------------------------------------------------------------
ErrinILS protects administrative operations using salted PBKDF2 / SHA-256
cryptographic hashing:

  Default Administrator Password:   admin123

Protected Operations:
  * System Settings and network configuration.
  * Permanent deletion of book catalog and patron records.
  * Restoring database backups.

How to Change the Password:
  1. Open ErrinILS and navigate to the "Settings" tab.
  2. Enter the current password (admin123) to unlock settings.
  3. Enter your new password and click Save.

Emergency Password Reset:
  If you ever forget the administrator password:
  1. Close ErrinILS completely.
  2. Press Win+R, type:  %APPDATA%\Errin  and press Enter.
  3. Delete the file:  errin-config.json  (Do NOT delete errin-data.db).
  4. Restart ErrinILS. The password resets to default: admin123.


[5] DATA PERSISTENCE & BACKUPS
------------------------------------------------------------------------------
ErrinILS does not use complicated client-server database engines. Your entire library
resides in standard local files inside:

  %APPDATA%\Errin\
    ├── errin-data.db      Master SQLite database (items, patrons, loans,
    │                      holds, fines, weeding history, class history).
    └── errin-config.json  Security credentials, Z39.50 client preferences,
                           and authenticated paired device tokens.

Backing Up Your Library:
  * Backup Tab: In ErrinILS, open the Backup tab and click "Download backup"
    to save a timestamped .db SQLite backup or .json export. Keep a copy on a USB flash drive regularly.
  * Catalog Transfer: In Catalog, click "Transfer catalog" to export full MARC21
    records and spreadsheets (.zip / .csv) for external archives or migration.
  * Manual Backup: Simply copy the file %APPDATA%\Errin\errin-data.db to
    any safe backup location or cloud drive.


[6] FEATURE HIGHLIGHTS
------------------------------------------------------------------------------
* Barcode Circulation:
  Scan student IC/barcode followed by book barcodes. Automatic loan limit
  enforcement (default: 3 books), loan periods (default: 7 days), overdue
  flags, and hold priority queue alerts.

* 5-Source ISBN Cataloguing:
  Scan or type any 10-digit or 13-digit ISBN. ErrinILS queries 5 international
  bibliographic sources in priority order:
    1. Library of Congress (Z39.50 lx2.loc.gov:210)
    2. Open Library (HTTP JSON API)
    3. Google Books (HTTP JSON API)
    4. HathiTrust Digital Library (HTTP MARCXML)
    5. K10plus German Union Catalogue (SRU MARCXML)
  Automatically populates Title, Author, Publisher, Year, and Dewey Decimal
  Classification (DDC).

* Simplified View:
  Streamlined interface designed for students and young readers. Features
  oversized buttons, clear visual status banners, and a dedicated exit
  button back to librarian controls.

* Wireless Mobile Audits & QR Pairing:
  Built-in pure C# HTTP server (0.0.0.0:8080) hosting a responsive mobile web
  app. Scan an on-screen QR code with any phone or tablet camera to instantly
  pair using 256-bit cryptographic tokens. Perform shelf stocktaking and
  catalog audits directly in the book stacks.

* Malaysian School Localization:
  Full validation for Malaysian IC and MyKid numbers (YYMMDD-PB-###G) with
  birth month/day calendar checks. Fines calculation in Ringgit Malaysia
  (RM 0.10/day overdue) and Nilam reading reports.

* Native MARC21 & Z39.50 Engine:
  Zero-dependency parsers and writers for binary ISO 2709 (.mrc), MARCXML
  (.xml), and MRK mnemonic (.mrk). Full MARC-8 diacritic conversion and
  ASN.1 BER encoded Z39.50 v3 client and server.

* Collection Maintenance & Weeding:
  Track withdrawn and weeded titles with specific reasons (Damaged, Obsolete,
  Lost, Duplicate) while preserving historical circulation statistics.


[7] BARCODE SCANNER GUIDE & FAST CIRCULATION
------------------------------------------------------------------------------
ErrinILS works with any standard USB or wireless barcode scanner that operates
in "keyboard-wedge" mode (the factory default for 99% of barcode scanners,
where scanning a barcode acts like typing on a keyboard followed by Enter).

Circulation Desk Workflow:
  1. Click the Circulation tab to open the circulation desk.
  2. Scan the student's ID barcode or Malaysian IC number.
     - Student profile, active loans, and fine status appear immediately.
  3. Scan the book barcode(s).
     - Books are checked out instantly with on-screen confirmation.
  4. Quick Return: Scan any book barcode without selecting a patron to
     immediately check it back in.


[8] BARCODE SCANNER & KEYBOARD INTERACTION
------------------------------------------------------------------------------
ErrinILS works with standard USB and wireless barcode scanners operating in
keyboard-wedge mode (which type the scanned digits followed by Enter):

  * Enter Key: Automatically submits scanned barcodes, search queries,
    and dialog forms. No additional keyboard shortcuts are required.
  * Circulation: Scan patron card, then scan book barcodes to check out.
  * Simplified View: Students scan their card and books directly. Click
    "Exit Simplified View" at the bottom to return to the dashboard.


[9] COMPILATION & BUILDING FROM SOURCE
------------------------------------------------------------------------------
ErrinILS requires no Visual Studio or NuGet packages. Build directly using
the built-in Microsoft .NET Framework C# compiler (csc.exe):

Native Windows Build:
  & "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
      /target:winexe /optimize+ /debug- `
      /win32icon:ErrinILS-source\icon.ico `
      /r:System.Drawing.dll,System.Windows.Forms.dll `
      /out:dist\ErrinILS.exe `
      ErrinILS-source\src\*.cs

Linux / macOS Build (Using Mono):
  cd ErrinILS-source && chmod +x build.sh && ./build.sh


[10] REPOSITORY DIRECTORY LAYOUT
------------------------------------------------------------------------------
  d:/errin/
  ├── AGENTS.md            Contributor architectural specification
  ├── README.md            GitHub markdown documentation
  ├── README.txt           Plain-text user & administrator guide
  ├── LICENSE              MIT Open Source License
  ├── dist/                Compiled installer and portable distribution
  ├── docs/                Documentation website, HTML guide, and assets
  └── ErrinILS-source/     C# application source tree
      ├── src/             Subsystems (Lib, Marc, Z3950, Lookup, Web, Qr, Ui)
      ├── icon.ico         Multi-resolution Win32 application icon
      ├── installer.nsi    NSIS Windows installer definition script
      └── build.sh         Cross-platform Mono build script


[11] DOCUMENTATION, USER GUIDE & SUPPORT
------------------------------------------------------------------------------
Complete 19-chapter User Manual:
  * Online HTML:   https://rzsy.github.io/ErrinILS/guide.html
  * Offline PDF:   docs\assets\ErrinILS-User-Guide.pdf
  * GitHub Issues: https://github.com/RZSY/ErrinILS/issues

ErrinILS is open-source software maintained under the MIT License.
Free to use, deploy, and customize in all schools.
==============================================================================
