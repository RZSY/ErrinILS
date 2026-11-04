# ErrinILS School Library System
## User Manual and Operating Guidelines
**Installation, daily circulation, cataloguing, students, fines, weeding, remote access, reports, backup and administration**  
**Software Version 1.3.1 (Windows Edition)**  
*For Windows 7, 8, 8.1, 10 and 11 (32-bit and 64-bit)*  
*Official Repository: https://github.com/RZSY/ErrinILS*  
*Online Portal: https://rzsy.github.io/ErrinILS/*

---

## Contents

1. [About ErrinILS](#1-about-errinils)
   - 1.1 What ErrinILS is
   - 1.2 Core Philosophy & Zero-Dependency Architecture
   - 1.3 What you can do with it
   - 1.4 Which chapters you need
   - 1.5 Conventions used in this manual
2. [System Requirements & Hardware Guidelines](#2-system-requirements--hardware-guidelines)
   - 2.1 System Requirements Matrix
   - 2.2 Display Resolution & High-DPI Sharpness
   - 2.3 Barcode Scanners & Wedge Input
   - 2.4 Label Printers & Paper Sheets
3. [Installing, Updating and Starting ErrinILS](#3-installing-updating-and-starting-errinils)
   - 3.1 Installing with the Setup program
   - 3.2 Running without installing (portable copy & USB drives)
   - 3.3 Upgrading to a newer version
   - 3.4 Uninstalling
   - 3.5 Where your data is stored (`%APPDATA%\Errin`)
   - 3.6 Single-instance execution mutex
4. [Getting Started & Initial Configuration](#4-getting-started--initial-configuration)
   - 4.1 The first time you run it (Sample data)
   - 4.2 A tour of the window
   - 4.3 Password-protected actions
   - 4.4 Using a barcode scanner
   - 4.5 Saving your work (Automatic persistence)
   - 4.6 If something goes wrong (Error resilience)
5. [Library Rules and Numbering at a Glance](#5-library-rules-and-numbering-at-a-glance)
   - 5.1 The rules ErrinILS applies
   - 5.2 How due dates and fines work
   - 5.3 When a student cannot borrow (Prioritized refusal checks)
   - 5.4 Identifiers used in the catalogue
6. [The Dashboard & Daily Overview](#6-the-dashboard--daily-overview)
   - 6.1 What the Dashboard shows
   - 6.2 Overdue items table & morning checklist
   - 6.3 Top borrowers summary
7. [Circulation: Check Out, Return, Renew and Holds](#7-circulation-check-out-return-renew-and-holds)
   - 7.1 Page layout
   - 7.2 Checking out a book
   - 7.3 Returning a book
   - 7.4 Renewing a book
   - 7.5 Placing a hold (reservations)
   - 7.6 The Current loans list
   - 7.7 The Holds queue
   - 7.8 Complete circulation error messages reference
8. [The Simplified View](#8-the-simplified-view)
   - 8.1 Purpose & design for young students
   - 8.2 Opening and leaving Simplified View
   - 8.3 Checking out books independently
   - 8.4 Returning books independently
   - 8.5 The color banner & transaction log
   - 8.6 Student-friendly messages reference
9. [The Catalog & Copy-Cataloguing](#9-the-catalog--copy-cataloguing)
   - 9.1 The Catalog page layout
   - 9.2 Searching the catalogue
   - 9.3 Adding a record by hand
   - 9.4 5-Source ISBN Copy-Cataloguing (LOC, OpenLib, Google, Hathi, K10plus)
   - 9.5 Field reference (MARC 21 mapping)
   - 9.6 Editing a catalogue record
   - 9.7 Viewing MARC records
   - 9.8 Deleting a record
   - 9.9 Importing MARC records (.mrc, .xml, .mrk, .txt)
   - 9.10 Searching other libraries (Z39.50)
   - 9.11 Printing barcode labels
   - 9.12 Printing spine labels
10. [Students & Patron Management](#10-students--patron-management)
    - 10.1 The Students page
    - 10.2 Registering a student
    - 10.3 Malaysian IC / MyKid validation rules
    - 10.4 Finding a student
    - 10.5 The student account & borrowing history
    - 10.6 Printing library cards
    - 10.7 Printing cards for a whole class
    - 10.8 Deleting a student
    - 10.9 Automated 5-year data retention and purge rule
11. [Fines Management](#11-fines-management)
    - 11.1 How fines arise
    - 11.2 The Fine Tracker page
    - 11.3 Recording payment or waiving a fine
    - 11.4 Handling lost or unreturned books
12. [Collection Weeding & Maintenance](#12-collection-weeding--maintenance)
    - 12.1 Purpose of weeding
    - 12.2 Withdrawal reasons
    - 12.3 Step-by-step weeding workflow
    - 12.4 Filtering active vs weeded copies
    - 12.5 Restoring weeded items to active circulation
    - 12.6 Permanent deletion vs weeding
13. [Remote Access, QR Code Pairing & Mobile Web App](#13-remote-access-qr-code-pairing--mobile-web-app)
    - 13.1 Architecture & network connectivity
    - 13.2 Enabling the remote web server
    - 13.3 Instant QR Code & 6-digit PIN pairing
    - 13.4 Using phone camera as a wireless barcode scanner
    - 13.5 Managing paired devices & token security
    - 13.6 Network interfaces: Multi-Wi-Fi, hotspots, and Tailscale
14. [Reports, Reading Champions & Analytics](#14-reports-reading-champions--analytics)
    - 14.1 Executive metrics & library participation
    - 14.2 Reading Champions leaderboard (Nilam awards)
    - 14.3 Circulation trajectory (6-month loan trends)
    - 14.4 Collection classification (Dewey Decimal 100s)
    - 14.5 Dormant collection audit (Zero-circulation tracking)
    - 14.6 One-Click Full Report export (CSV spreadsheet)
15. [Backup, Restore and Disaster Recovery](#15-backup-restore-and-disaster-recovery)
    - 15.1 Why back up & disaster scenarios
    - 15.2 Making a backup (JSON & built-in ZIP archive)
    - 15.3 Restoring from a backup
    - 15.4 Moving to a new computer
    - 15.5 Recommended backup habits (3-2-1 strategy)
16. [Settings and Administration](#16-settings-and-administration)
    - 16.1 Opening Settings
    - 16.2 Configuring library policies
    - 16.3 Changing the administrator password
    - 16.4 Data retention settings
    - 16.5 Exporting the catalogue to another system
    - 16.6 The Z39.50 server (Optional network sharing)
    - 16.7 The local database path
17. [Recommended Operational Routines](#17-recommended-operational-routines)
    - 17.1 Every day the library is open
    - 17.2 Every week
    - 17.3 Every term
    - 17.4 Every year
18. [Troubleshooting Guide](#18-troubleshooting-guide)
    - 18.1 Comprehensive problem & resolution table
    - 18.2 Resetting a forgotten administrator password
19. [Frequently Asked Questions (FAQ)](#19-frequently-asked-questions-faq)
- [Appendix A. Quick Task Reference Guide](#appendix-a-quick-task-reference-guide)
- [Appendix B. Glossary of Terms](#appendix-b-glossary-of-terms)
- [Appendix C. Files and Folders Directory Reference](#appendix-c-files-and-folders-directory-reference)

---

## 1. About ErrinILS

### 1.1 What ErrinILS is
**ErrinILS** is an integrated library system (ILS) engineered specifically for primary, secondary, and vernacular school libraries. It is a lightweight desktop program for Windows. You install it on the library computer, and from then on it maintains complete, synchronized records of three essential pillars:
1. The **catalogue** of books, learning materials, and physical items in the library.
2. The **students, teachers, and patrons** registered to borrow them.
3. The **active loans, historical borrowings, holds (reservations), and fines** connecting items and patrons.

Everyday library operation does not require an active internet connection. All data lives on the library computer itself and is saved to disk automatically every time you make a change. The internet is used only for two optional tasks: looking up book bibliographic records by ISBN from international databases, and searching other libraries' catalogues over Z39.50.

### 1.2 Core Philosophy & Zero-Dependency Architecture
Traditional enterprise library software (such as Koha, SLiMS, or commercial web suites) requires heavy stacks: Linux servers, Apache/Nginx web servers, MySQL or PostgreSQL databases, PHP or Python runtimes, and continuous IT support. When a school computer loses internet access or is managed by a teacher-librarian with limited IT background, those systems quickly break down.

ErrinILS is built on a **strict zero-external-dependency philosophy**:
- **Pure .NET Framework 4.0**: Built using only the native Microsoft assemblies included with Windows (`mscorlib`, `System`, `System.Drawing`, `System.Windows.Forms`). No external NuGet packages, no C++ redistributables, and no third-party DLLs.
- **Single Standalone Executable**: Compiles into a single binary (`ErrinILS.exe`) of ~200 KB.
- **Single JSON Database**: All library data persists in human-readable JSON format (`%APPDATA%\Errin\errin-data.json`). No database server installation, configuration, or daemon management is required.
- **Ultra-Low Resource Footprint**: Operates with fluid speed on budget classroom PCs with as little as 1 GB RAM and ~10 MB disk space.
- **School Localization**: Built-in support for Malaysian National Registration IC and MyKid formats (`YYMMDD-PB-###G`), Ringgit Malaysia (RM) fine calculations, Nilam reading awards tracking, and child-friendly terminology.

### 1.3 What you can do with it
- **Circulation Desk**: Check out, return, renew, and place holds on books with sub-second barcode scanner response. The system automatically enforces loan limits, loan durations, overdue blocks, and reserve queues.
- **MARC 21 Cataloguing**: Full bibliographic cataloguing complying with international MARC 21, ISO 2709, MARCXML, and MarcEdit MRK standards.
- **5-Source Copy-Cataloguing**: Scan an ISBN to auto-fill title, author, publisher, publication year, and Dewey Decimal Classification (DDC) from the Library of Congress (Z39.50), Open Library, Google Books, HathiTrust, and K10plus.
- **Students & Patrons**: Register students with automatic Malaysian IC / MyKid validation, manage class allocations, and track complete reading histories.
- **Printing**: Print 1D Code 128 book barcode labels (70 x 30 mm), spine call-number labels (25 x 38 mm), and scannable plastic-size student library cards (85.6 x 54 mm).
- **Simplified View**: An uncluttered, high-contrast, child-friendly circulation desk designed for elementary and high school students.
- **Weeding & Collection Maintenance**: Mark damaged, obsolete, lost, or duplicate books as weeded to remove them from borrowing while permanently preserving historical circulation metrics.
- **Remote Access & QR Wireless Pairing**: Connect smartphones and classroom tablets to the built-in HTTP server using a zero-password 6-digit PIN and scannable QR code. Use your phone camera as a wireless shelf scanner.
- **Reports & Reading Champions**: Track Nilam Reading Champions leaderboards (top student readers), 6-month borrowing trajectories, Dewey Decimal 100s breakdown, dormant inventory audits, and 1-click CSV spreadsheet exports.
- **Backup & Disaster Recovery**: One-click JSON and pure C# DEFLATE ZIP archive backups for safe USB drive archiving.
- **Z39.50 Server (Optional)**: Built-in ASN.1 BER server allowing other institutions to search your collection over the local network.

### 1.4 Which chapters you need

| If you are… | Read… |
|---|---|
| **The Teacher-Librarian or Library Administrator** | Everything. Start with Chapters 3, 4, and 5, followed by Chapters 6 through 16. |
| **A Student Helper or Library Prefect** | Chapter 4.4 (barcode scanners), Chapter 7 (Circulation Desk), and Chapter 8 (The Simplified View). |
| **The School IT Coordinator or Technician** | Chapter 2 (System Requirements), Chapter 3 (Installation & Data Storage), Chapter 13 (Remote QR Access & Networking), Chapter 15 (Backups), and Chapter 18 (Troubleshooting). |

### 1.5 Conventions used in this manual
- **Bold text** represents buttons, tabs, input boxes, and menu options exactly as they appear on screen (e.g., **Check out**, **Add Record**, **Settings**).
- **"Tab"** refers to one of the main navigation buttons across the top toolbar (**Dashboard**, **Circulation**, **Catalog**, **Students**, **Fines**, **Reports**, **Backup**, **Settings**).
- **Monetary amounts** are expressed in Malaysian Ringgit (**RM**).
- **Dates** are formatted in ISO standard `YYYY-MM-DD` (e.g., `2026-10-09`), except in the Simplified View where they are presented in natural English (e.g., `9 Oct 2026`).
- **Callout boxes** highlight critical guidance:
  - **Tip**: Shortcuts and operational best practices.
  - **Note**: Technical context and background details.
  - **Caution**: Actions that require care or affect data integrity.
  - **Important**: Must-know rules and security warnings.

---

## 2. System Requirements & Hardware Guidelines

### 2.1 System Requirements Matrix
ErrinILS is designed to run efficiently on low-resource hardware:

| Item | Minimum Requirement | Recommended Specification |
|---|---|---|
| **Operating System** | Windows 7 SP1 (32-bit or 64-bit) | Windows 10 or Windows 11 (64-bit) |
| **Required Runtime** | Microsoft .NET Framework 4.0 | Microsoft .NET Framework 4.8 (pre-installed on Win 10/11) |
| **Processor (CPU)** | 1.0 GHz x86/x64 processor | 1.8 GHz dual-core or faster |
| **System Memory (RAM)** | 1 GB RAM | 2 GB RAM or more |
| **Hard Disk Space** | 10 MB for binary; ~50 MB for database & backups | 100 MB free storage |
| **Display Resolution** | 1024 × 600 (minimum window size: 900 × 600) | 1366 × 768 or 1920 × 1080 |
| **Internet Access** | Optional (100% offline circulation) | Needed only for ISBN lookup & remote Z39.50 search |
| **Barcode Scanner** | Optional (keyboard input supported) | USB 1D/2D Handheld Laser or CCD Barcode Scanner |
| **Printer** | Optional | Any standard A4 laser/inkjet printer or direct thermal printer |

> [!NOTE]
> Windows 8, 8.1, 10, and 11 include .NET Framework 4.5+ natively; ErrinILS will run immediately without extra downloads. On older **Windows 7** machines, install the free Microsoft .NET Framework 4.8 runtime once before running Setup.

### 2.2 Display Resolution & High-DPI Sharpness
On high-resolution modern laptops (1080p, 2K, and 4K displays), Windows applies display scaling (125%, 150%, or 200%). Many older applications become blurry due to Desktop Window Manager (DWM) bitmap stretching.

ErrinILS incorporates native **PerMonitorV2 High-DPI awareness** in its core manifest. All fonts, tables, barcode graphics, and flat controls scale crisply using sub-pixel ClearType font rendering with zero GPU overhead.

### 2.3 Barcode Scanners & Wedge Input
ErrinILS is engineered to work out of the box with any **USB Keyboard Wedge Barcode Scanner**. These scanners emulate rapid keyboard typing followed by an Enter keypress (`\r\n` carriage return).
- Ensure your scanner is programmed in **USB HID Keyboard Mode** (the factory default for 99% of barcode scanners).
- When you open the **Circulation** tab or **Simplified View**, the cursor is automatically focused on the active scan box. Simply scan the barcode and ErrinILS processes the transaction instantly.

### 2.4 Label Printers & Paper Sheets
- **Book Barcode Labels (70 × 30 mm)**: Formatted for standard 3-column sticker sheets (e.g., 24 or 30 labels per A4 page) or single continuous thermal label rolls.
- **Spine Labels (25 × 38 mm / 1 × 1.5 inch)**: Formatted for compact book spines displaying multi-line Dewey call numbers in bold, high-contrast type.
- **Student Library Cards (85.6 × 54 mm)**: Standard CR80 credit card dimensions, printed several per page on heavy cardstock (160–240 gsm).

---

## 3. Installing, Updating and Starting ErrinILS

### 3.1 Installing with the Setup program
1. Download `ErrinILS-Setup-1.3.1.exe` from the official release page: `https://github.com/RZSY/ErrinILS/releases/tag/v1.3.1`.
2. Double-click `ErrinILS-Setup-1.3.1.exe`.
3. If Windows User Account Control (UAC) prompts *"Do you want to allow this app to make changes?"*, click **Yes**.
4. If a blue **Windows protected your PC** (SmartScreen) notification appears, click **More info**, then click **Run anyway**. *(This appears on open-source releases that do not have costly commercial signing certificates).*
5. The setup wizard opens. Click **Next**, keep the default destination directory (`C:\Program Files (x86)\ErrinILS` or `C:\Program Files\ErrinILS`), click **Install**, then click **Finish**.
6. The installer automatically generates Start Menu shortcuts and a Desktop icon.

### 3.2 Running without installing (portable copy & USB drives)
If you do not have Windows administrator rights or wish to operate ErrinILS entirely from a USB thumb drive:
1. Download `ErrinILS-Windows.zip`.
2. Extract the archive into any folder (e.g. `E:\ErrinILS\` on your USB drive or a folder on the Desktop).
3. Double-click `ErrinILS.exe`.
4. The portable version functions identically to the installed version. It reads and writes data to `%APPDATA%\Errin\` on the host computer.

### 3.3 Upgrading to a newer version
Upgrading is 100% non-destructive:
1. Close any running copies of ErrinILS.
2. Run the new version's installer (or replace `ErrinILS.exe` with the new binary).
3. All catalogued books, registered students, active loans, and fine histories are preserved intact in `%APPDATA%\Errin\errin-data.json`.
4. *(Best Practice: Always download a backup ZIP from the Backup tab before any major software upgrade).*

### 3.4 Uninstalling
- On **Windows 10 / 11**: Open **Settings → Apps → Installed apps**, select **ErrinILS**, and click **Uninstall**.
- On **Windows 7 / 8**: Open **Control Panel → Programs and Features**, select **ErrinILS**, and click **Uninstall**.
- Uninstalling removes application binaries and shortcuts but leaves your library database (`%APPDATA%\Errin\errin-data.json`) intact to prevent accidental data loss.

### 3.5 Where your data is stored
All application data lives in your local Windows user profile directory:
```text
%APPDATA%\Errin\
```
*(To open this folder directly, press `Win + R`, paste `%APPDATA%\Errin`, and press Enter).*

| File | Purpose | Contents |
|---|---|---|
| `errin-data.json` | **Main Library Database** | Entire collection catalogue, registered students, active and historical loans, holds, assessed fines, and circulation rules in plain-text JSON. |
| `errin-data.bak` | **Automatic Rollback Backup** | The previous snapshot of the data file, written automatically before each save. If `errin-data.json` ever suffers power-cut corruption, the system automatically recovers from this file. |
| `errin-config.json` | **Security & Network Settings** | The administrator password (PBKDF2/SHA-256 salted hash), Z39.50 server configuration, and remote web server paired device tokens. |

> [!IMPORTANT]
> The database file `errin-data.json` contains real student names, Malaysian IC numbers, and class allocations. Protect the physical computer with a Windows login password and restrict USB drive backups to authorized staff.

### 3.6 Single-instance execution mutex
ErrinILS employs a named Windows kernel mutex (`ErrinILS_SingleInstance_Mutex`). Only one copy of the program can run on a computer at a time. If someone attempts to launch ErrinILS while it is already open, the system brings the existing window to the front instead of creating conflicting database locks.

---

## 4. Getting Started & Initial Configuration

### 4.1 The first time you run it
When ErrinILS launches and detects no database, it automatically generates a starter database with five sample books and one sample student:
- **Sample Books**: *Charlotte's Web* (`B1001`), *A Brief History of Time* (`B1002`), *Hatchet* (`B1003`), *The Hobbit* (`B1004`), and *Atlas of the World* (`B1005`).
- **Sample Student**: Aisha Rahman (`120315-10-0001`, Class 7A).

Use these sample records to test your barcode scanner and practice checkouts. When ready for production, delete the sample student (**Students → Delete**) and edit or delete the sample books (**Catalog → Delete**). Deleting records requires the administrator password.

### 4.2 A tour of the window
- **Top Header**: ErrinILS icon and title, followed by 8 navigation tabs: **Dashboard**, **Circulation**, **Catalog**, **Students**, **Fines**, **Reports**, **Backup**, and **Settings**. The active tab is highlighted in Errin Gold.
- **Main Working Area**: The primary screen corresponding to the selected tab.
- **Bottom Status Bar**: Displays persistence health: *"Saved to this computer (YYYY-MM-DD HH:MM:SS)"*. If the bar turns red and displays *"NOT saving"*, the disk is full or write-protected.
- **Notification Toast**: Action results appear as an alert toast near the bottom of the window (e.g. *"Checked out. Due 2026-10-16"*), automatically fading after 3 seconds.

### 4.3 Password-protected actions
To prevent accidental changes or student tampering, the administrator password is required for:
1. Opening the **Settings** tab.
2. Deleting a catalogue item or patron record.
3. Restoring a database from a backup file.

> [!IMPORTANT]
> ErrinILS ships with a default administrator password (`admin123`). Change this password immediately upon your first launch in **Settings → Admin Password**.

### 4.4 Using a barcode scanner
1. Connect your USB barcode scanner to any USB port.
2. Ensure the cursor is flashing in the input box where the scan should go (e.g. the **Barcode** box in Circulation).
3. Pull the trigger on the barcode. The barcode text is entered and the trailing Enter key automatically submits the transaction.
4. *Scanner Verification Test*: Open Notepad and scan any barcode. If the number appears and the cursor jumps to a new line, your scanner is configured correctly.

### 4.5 Saving your work (Automatic persistence)
ErrinILS features zero-click automatic persistence. Every circulation event, newly catalogued item, fine payment, or patron update is written to `errin-data.json` the microsecond it occurs. There is no manual "Save" button for routine operations.

### 4.6 If something goes wrong (Error resilience)
If an unhandled system error occurs, ErrinILS displays a notification: *"Something went wrong: [details]. Your data is safe."* Click **OK** to resume. If the problem persists, export a backup from the **Backup** tab, restart the software, and consult Chapter 18.

---

## 5. Library Rules and Numbering at a Glance

### 5.1 The rules ErrinILS applies

| Rule | Default Value | Configurable? | Location in Settings |
|---|---|---|---|
| **Loan Period** | 7 calendar days | Fixed in v1.3.1 | Core Circulation Engine |
| **Max Renewals per Loan** | 2 renewals (+14 days max) | Fixed in v1.3.1 | Core Circulation Engine |
| **Max Books per Student** | 3 books | Yes | **Settings → Max items per student** |
| **Overdue Fine Rate** | RM 0.10 / day late | Yes | **Settings → Fine per day late** |
| **Borrowing Block Threshold** | RM 5.00 unpaid fines | Yes | **Settings → Block checkout at unpaid fines of** |
| **Data Retention Purge** | 5 years after last activity | Automatic | **Tools → Purge Inactive Data** |
| **Student ID Standard** | Malaysian 12-digit IC/MyKid | Yes (Strict) | **Students → Register Student** |

### 5.2 How due dates and fines work
- **Due Date Calculation**: A book checked out on Monday 1 September is due on Monday 8 September (day of checkout + 7 days).
- **Grace Period**: On the due date itself, the book is considered on time. A book is overdue only when the calendar day advances past the due date.
- **Accrual vs. Settled Fines**: While an overdue book remains in the student's possession, the fine accrues in real time on the Dashboard (*"Fine on item"*), but is not officially recorded in the ledger. When the book is returned at the circulation desk, the fine is finalized and recorded in the **Fines** ledger.

### 5.3 When a student cannot borrow (Prioritized refusal checks)
When a checkout is attempted, ErrinILS evaluates the patron and copy against 6 criteria in strict priority order:
1. **Patron or Item Non-Existent**: The IC number or book barcode does not exist in the database.
2. **Item Already Checked Out**: The copy is currently on loan to another reader.
3. **Unpaid Fines Exceed Block Limit**: The student's recorded, unsettled fines equal or exceed the block threshold (default: `RM 5.00`).
4. **Student Has Overdue Items**: The student currently holds one or more books past their due date.
5. **Loan Quota Reached**: The student already holds the maximum permitted number of books (default: `3`).
6. **Active Hold by Another Reader**: Another student is waiting first in the hold queue for this specific title.

> [!CAUTION]
> Never configure **Block checkout at unpaid fines of** to `0.00`. Because the rule blocks checkout when unpaid fines reach or exceed the threshold, a value of zero would block every student at all times. To disable fine blocking, enter a large number such as `999.00`.

### 5.4 Identifiers used in the catalogue

| Identifier | Typical Format | MARC Field | Purpose |
|---|---|---|---|
| **Barcode** | `B1001`, `B1002`, `9780141325286` | `852 $p` | Unique identifier on the physical book label. Scanned at the desk. |
| **Control Number** | `ER00000001`, `ER00000002` | `001` | Unique bibliographic master record number. |
| **Acquisition Number** | `ACQ-000001`, `ACQ-000002` | `541 $e` | Sequential tracking number recording accession into the collection. |
| **Call Number** | `F DAH`, `823.914 ROW`, `523.1 HAW` | `082 $a` | Physical shelf location. Printed on spine labels. |
| **Student ID** | `080415-10-5421` | Custom | Malaysian IC / MyKid 12-digit identification number. |

---

## 6. The Dashboard & Daily Overview

### 6.1 What the Dashboard shows
The Dashboard provides an immediate executive snapshot of library activity:
- **Simplified View Card**: One-click launcher for the Simplified View.
- **Key Metric Tiles**:
  - **Titles in Catalog**: Total active catalogued copies.
  - **Registered Students**: Total active student patron accounts.
  - **Items on Loan Now**: Total physical copies currently checked out.
  - **Checkouts, All Time**: Cumulative borrowing transactions since installation.
  - **Overdue Items**: Total items past their return date.
  - **Unpaid Fines**: Cumulative monetary balance owed in Ringgit Malaysia (RM).
  - **Active Holds**: Reservations currently queued or ready for collection.

### 6.2 Overdue items table & morning checklist
The Overdue Items table lists every delinquent loan:
- **Columns**: Student Name, Class, Book Title, Due Date, Days Late, Accrued Fine, and Total Owed (accrued fines + past unsettled fines).
- **Morning Routine**: Check this table at the beginning of each school day to generate reminder slips for class teachers. When zero items are overdue, a celebration banner appears.

### 6.3 Top borrowers summary
Three distinct leaderboards recognize your most enthusiastic readers:
- **Top Borrowers This Month**
- **Top Borrowers This Year**
- **Top Borrowers All-Time**  
*(Use these rankings to award school Nilam certificates and reading prizes).*

---

## 7. Circulation: Check Out, Return, Renew and Holds

### 7.1 Page layout
The **Circulation** tab is organized into ergonomic operational cards:
1. **Check Out Card**: Student IC input and Book Barcode input with **Check Out** button.
2. **Return / Renew Card**: Universal Book Barcode input with **Return** and **Renew** buttons.
3. **Place Hold Card**: Student IC input and Book Barcode input with **Place Hold** button.
4. **Current Loans List**: Real-time table of all outstanding loans sorted by earliest due date.
5. **Holds Queue**: List of all active reservations and their pickup status.

### 7.2 Checking out a book
1. In the **Check out** card, scan the student's library card barcode into **IC number** (or type the 12 digits).
2. The cursor jumps to the **Item barcode** box. Scan the barcode on the inside or back cover of the book.
3. The checkout processes automatically upon scanning. A confirmation toast appears: *"Checked out. Due [YYYY-MM-DD]"*.
4. The loan immediately appears at the top of the **Current loans** table.

### 7.3 Returning a book
1. Place the cursor in the **Item barcode** box in the **Return / Renew** card.
2. Scan the book barcode. The student does not need to be present—the book identifies the loan.
3. ErrinILS closes the loan immediately:
   - If returned on time: Toast confirms *"Returned. Thank you!"*.
   - If returned late: Toast confirms *"Returned late [N] days. Fine: RM [X.XX]"*.
   - If a hold was waiting: An alert banner flashes: *"Hold ready for [Student Name] ([Class])"*. Place the book on the reservation shelf.

### 7.4 Renewing a book
1. Scan the book into the **Return / Renew** box and click **Renew** (or select the loan in the **Current loans** list and click **Renew selected**).
2. The loan is extended for another 7 days from today.
3. *Refusal Conditions*: Renewals are blocked if the book is already overdue, if it has already been renewed 2 times, or if another student has placed a hold on it.

### 7.5 Placing a hold (reservations)
1. In the **Place hold** card, enter the student's IC number and the book barcode.
2. Click **Place hold**. The hold is queued with status **Waiting**.
3. *Rules*: Holds can only be placed on books currently on loan. A student cannot place duplicate holds on the same title.
4. When the book is returned at the desk, the first queued hold automatically converts to **Ready for pickup**.

### 7.6 The Current loans list
Columns: **Student**, **Title**, **Barcode**, **Due Date**, and **Renewals Count**. Overdue items are flagged with `(overdue)` in bold red text.

### 7.7 The Holds queue
Columns: **Title**, **Reserving Student**, **Date Placed**, and **Status** (`Waiting` or `Ready for pickup`). Select any row and click **Cancel hold** to remove uncollected reservations.

### 7.8 Complete circulation error messages reference

| Error Message | Cause | Resolution |
|---|---|---|
| `Student not found` | The scanned IC is not registered. | Register the student in the **Students** tab. |
| `Item not found` | Barcode not recognized in catalogue. | Verify barcode label or add book in **Catalog**. |
| `Item is already checked out` | Copy is already out to another student. | Check in the previous loan first or place a hold. |
| `Blocked: unpaid fines RM x.xx` | Student fines meet/exceed block threshold. | Settle or waive fines in the **Fines** tab. |
| `Blocked: student has overdue items` | Student holds an overdue book. | Student must return late books before borrowing new ones. |
| `Loan limit reached` | Student already has 3 books checked out. | Return a book before borrowing another. |
| `Reserved for another student` | Book is on hold for a different reader. | Issue the book to the reserving patron. |
| `Item is not on loan` | Scanning an already-returned book. | Return book to shelf; loan is already closed. |
| `Overdue items cannot be renewed` | Loan is past its due date. | Return the book and assess fine. |
| `Renewal limit (2) reached` | Book was already renewed twice. | Return the book to the library. |
| `Cannot renew: item has a hold` | Another reader is waiting for this title. | Return the book for the next reader. |

---

## 8. The Simplified View

### 8.1 Purpose & design for young students
The **Simplified View** transforms ErrinILS into an uncluttered, high-contrast, child-friendly circulation screen. Designed specifically for primary and secondary students:
- Large oversized touch and click buttons.
- Clear visual feedback and status banners.
- Hidden administrative toolbars and settings to prevent tampering.

### 8.2 Opening and leaving Simplified View
- **To Launch**: On the Dashboard, click **Open Simplified View**.
- **To Exit**: Click **Exit Simplified View** at the bottom-right corner to return to the full administrative desk.

### 8.3 Checking out books independently
1. The screen displays: *"Ready. Scan a library card."*
2. The student scans their school card barcode.
3. The top banner illuminates in vibrant green: *"Hello, [Student Name]!"*
4. The student scans their book barcode.
5. ErrinILS confirms on screen: *"Checked out: [Book Title]. Due back [Date]!"*
6. When finished, clicking **Next Student** clears the session for the next child.

### 8.4 Returning books independently
1. In the **Return** box, the student scans the book barcode and presses **Return**.
2. The banner turns green: *"Returned: [Book Title]. Thank you!"*
3. If late, the banner turns yellow: *"This book was late. A fine has been recorded. Please see the librarian."*

### 8.5 The color banner & transaction log

| Banner Color | Meaning | Student Experience |
|---|---|---|
| **Green** | Transaction Successful | Book checked out or returned cleanly. |
| **Yellow** | Warning / Notice | Late return fine assessed or book was already returned. |
| **Red** | Blocked Action | Student has overdue books, unpaid fines, or invalid card. |
| **Slate / Grey** | Idle / Waiting | Awaiting student card or book barcode scan. |

The **Done so far** table displays the last 8 transactions with timestamp, student name, title, and outcome.

### 8.6 Student-friendly messages reference

| System Condition | Child-Friendly Message |
|---|---|
| Card not in database | *"I can't find that card. Please ask the librarian for help."* |
| Barcode not in database | *"I don't know this book. Please ask the librarian."* |
| Book already borrowed | *"Someone else has this book right now."* |
| Overdue item held | *"You have a late book. Please bring it back first!"* |
| Quota of 3 books reached | *"You have borrowed the most books allowed. Bring one back first!"* |
| Reserved for classmate | *"This book is saved for a friend."* |
| Unpaid fine block | *"Please see the librarian at the desk before borrowing."* |

---

## 9. The Catalog & Copy-Cataloguing

### 9.1 The Catalog page layout
The **Catalog** tab manages every bibliographic record and physical copy in the collection:
- **Top Actions**: **+ Add Record**, **Import MARC**, **Z39.50 Search**, **Print Labels**, and **Print Spine Labels**.
- **View Filter**: Toggle between **Active Collection** and **Weeded Items**.
- **Search Bar**: Real-time multi-term search across titles, authors, ISBNs, barcodes, and Dewey numbers.
- **Bottom Actions**: **Edit**, **MARC**, **Label**, **Spine Label**, **Weed Copy**, and **Delete**.

### 9.2 Searching the catalogue
Type any search keyword into the **Search** box. Searching `Rowling`, `823.914`, `B1042`, or `0545010221` immediately filters matching records with sub-second speed.

### 9.3 Adding a record by hand
1. Click **+ Add record**. ErrinILS auto-generates the next sequential Barcode, Control Number, and Acquisition Number.
2. Fill in **Title** (required), **Author**, **ISBN**, **Publisher**, **Year**, and **Call Number**.
3. Click **Save**. The record is immediately active.

### 9.4 5-Source ISBN Copy-Cataloguing
Save 95% of cataloguing time with automated multi-source copy-cataloguing:
1. Click **+ Add record**.
2. Scan the book's 10-digit or 13-digit ISBN barcode into the **Copy cataloguing** box.
3. Click **Look up** (or press <kbd>Enter</kbd>).
4. ErrinILS queries 5 international bibliographic archives simultaneously in priority sequence:
   - **1. Library of Congress (LOC)**: Native ASN.1 BER over Z39.50 v3 (`lx2.loc.gov:210`).
   - **2. Open Library**: Global community database via JSON REST API.
   - **3. Google Books API**: Massive commercial database with summaries and publication dates.
   - **4. HathiTrust Digital Library**: Academic union catalogue via MARCXML retrieval.
   - **5. K10plus German Union Catalogue**: SRU MARCXML gateway for international acquisitions.
5. The title, subtitle, author, publisher, publication date, pages, subjects, and Dewey Decimal classification are auto-populated.
6. Verify the shelf **Call number** and click **Save**.

### 9.5 Field reference (MARC 21 mapping)

| Field Box | MARC Tag | Description | Notes |
|---|---|---|---|
| **Title** | `245 $a` | Primary title of the book | Required |
| **Subtitle** | `245 $b` | Remainder of title / subtitle | Optional |
| **Author** | `100 $a` | Primary author (Last, First) | Recommended |
| **ISBN** | `020 $a` | 10 or 13-digit ISBN | Auto-lookup key |
| **Publisher** | `260 $b` | Publishing house | Auto-populated |
| **Publication Place**| `260 $a` | City/Country of publication | Auto-populated |
| **Year** | `260 $c` | 4-digit publication year | Auto-populated |
| **Pages** | `300 $a` | Physical description / pagination | Auto-populated |
| **Subject** | `650 $a` | Topical subject headings | Semicolon-separated |
| **Call Number** | `082 $a` | Dewey classification & author mark | Printed on spine |
| **Barcode** | `852 $p` | Unique physical barcode sticker | Required & unique |
| **Control Number** | `001` | Bibliographic master identifier | Auto-assigned |
| **Acquisition No** | `541 $e` | Accession sequence tracker | Auto-assigned |

### 9.6 Editing a catalogue record
Select any row and click **Edit** (or double-click the row). Modify any field and click **Save**. Editing records does not alter existing borrowing histories.

### 9.7 Viewing MARC records
Select any book and click **MARC**. ErrinILS displays the raw MARC 21 record line-by-line with tags, indicators, and subfield delimiters (e.g. `245 10 $a Charlie and the chocolate factory / $c by Roald Dahl`).

### 9.8 Deleting a record
Select an item and click **Delete**. A book currently on loan cannot be deleted (*"Item is on loan"*). Deleting requires the administrator password. Past loans of deleted books remain in student histories marked as `(deleted)`.

### 9.9 Importing MARC records
To import books from another school library or book supplier:
1. Click **Import MARC** (or drag and drop a MARC file into the window).
2. Supported formats: `.mrc` (binary ISO 2709), `.xml` (MARCXML), `.mrk` and `.txt` (MarcEdit format).
3. The import preview reports total records, new records, duplicates, and damaged entries.
4. Select duplicate handling: **Skip duplicates** or **Add as another copy (new barcode)**.
5. Click **Import**. Hundreds of records import in seconds.

### 9.10 Searching other libraries (Z39.50)
1. Click **Z39.50 search**.
2. Select target library (default: **Library of Congress** `lx2.loc.gov:210`, database `LCDB`).
3. Search by **ISBN**, **Title**, **Author**, or **Keyword**.
4. Select any result and click **Add selected to catalogue**. A new accession record is created immediately.

### 9.11 Printing barcode labels
- **Individual Book**: Select the book and click **Label**. Set copies (1–50) and click **Print**.
- **Batch Printing**: Filter the list by search keyword, click **Print labels** at the top, and confirm.
- Labels are formatted at **70 × 30 mm** featuring call number, book title, and crisp Code 128 barcode.

### 9.12 Printing spine labels
- Formatted at **25 × 38 mm (1 × 1.5 in)** displaying the Dewey Decimal call number split across lines in large, bold font.
- Click **Spine label** for one book, or **Print spine labels** for batch printing.

---

## 10. Students & Patron Management

### 10.1 The Students page
Columns: **IC Number**, **Full Name**, **Class/Grade**, **Registered Date**, **Active Loans Count**, and **Unpaid Fines Balance**.

### 10.2 Registering a student
1. Click **+ Register student**.
2. Enter the student's 12-digit Malaysian IC or MyKid number.
3. Enter **Full Name**, **Class / Grade** (e.g. `4 Melur`), and optional **Guardian Contact Phone**.
4. Click **Register**. The system immediately offers to print the student's library card.

### 10.3 Malaysian IC / MyKid validation rules
ErrinILS implements strict validation for Malaysian identity numbers:
- **Length**: Exactly 12 numeric digits (`YYMMDD-PB-###G`).
- **Calendar Verification**:
  - `MM` must be a valid month (`01` to `12`).
  - `DD` must be a valid calendar day for the specified month and year (e.g., February 30 is rejected).
- **Uniqueness**: Duplicate registrations of the same IC number are strictly blocked.

### 10.4 Finding a student
Type a student name, class name (e.g. `5 Cemerlang`), or IC number into the search box. Typing with or without dashes (`080415105421` or `080415-10-5421`) locates the student instantly.

### 10.5 The student account & borrowing history
Select any student and click **Account** (or double-click their row). The Account window reveals:
- Full student identity profile and guardian phone number.
- Total accumulated and unpaid fines.
- Complete borrowing ledger showing **Book Title**, **Date Borrowed**, **Due Date**, and **Date Returned**.
- Date scheduled for automatic privacy purge.

### 10.6 Printing library cards
Select a student and click **Print library card**. The generated card is standard credit-card size (**85.6 × 54 mm**) displaying school name, student name, class, IC number, and a scannable Code 128 barcode of their IC.

### 10.7 Printing cards for a whole class
Type the class name into the search box (e.g. `4 Melur`), then click **Print cards for list**. ErrinILS arranges all cards onto A4 sheets ready for laminating.

### 10.8 Deleting a student
Select the student and click **Delete**. If the student currently holds unreturned books, deletion is blocked (*"Student has unreturned items"*). Deletion requires the administrator password.

### 10.9 Automated 5-year data retention and purge rule
To comply with student privacy laws, ErrinILS automatically purges inactive student records **5 years** after their last recorded activity (loan, renewal, or registration). A student is never purged if they have unreturned books or unpaid fines. You can also run a manual audit in **Tools → Purge Inactive Data**.

---

## 11. Fines Management

### 11.1 How fines arise
Fines are created when an overdue book is scanned at the return desk:
$$\text{Fine Amount} = \text{Days Overdue} \times \text{Daily Fine Rate (RM 0.10)}$$
Fines accumulate per book. If a student returns three late books together, three distinct fine records are created.

### 11.2 The Fine Tracker page
Open the **Fines** tab:
- **Summary Cards**: Displays **Total Outstanding Fines** and **Total Collected Fines**.
- **Ledger Table**: Lists Date, Student Name, Book Title, Days Late, Amount (RM), and Status (`Unpaid`, `Paid`, or `Waived`).

### 11.3 Recording payment or waiving a fine
1. Select the fine record in the table.
2. Click **Pay** when the student presents cash payment.
3. Click **Waive** if the library excuses the fine (e.g., student was hospitalized with medical certificate).
4. Once paid or waived, the status is sealed and the student's checkout block is lifted immediately.

### 11.4 Handling lost or unreturned books
If a student reports a library book lost:
1. In the **Catalog** tab, select the book copy and click **Weed Copy**.
2. Select withdrawal reason: **Lost / Unreturned**.
3. In the **Fines** tab, assess the replacement fee according to your school policy.

---

## 12. Collection Weeding & Maintenance

### 12.1 Purpose of weeding
Weeding (de-accessioning) is the planned removal of damaged, obsolete, or unreturned books from the active collection. ErrinILS allows you to withdraw copies without destroying historical circulation records or skewing reading statistics.

### 12.2 Withdrawal reasons
When weeding a book, choose one of 4 standardized withdrawal reasons:
- **Condition / Damaged**: Physical wear, torn pages, water stains, binding failure.
- **Obsolete / Outdated**: Superseded curriculum syllabus or outdated scientific information.
- **Lost / Unreturned**: Unrecovered inventory following annual stocktaking.
- **Duplicate Excess**: Surplus extra copies being donated or transferred.

### 12.3 Step-by-step weeding workflow
1. In the **Catalog** tab, locate and select the book copy.
2. Click **Weed Copy** at the bottom of the screen.
3. Select the withdrawal reason from the dropdown.
4. Click **Confirm Weeding**.
5. The book is marked as weeded and blocked from future checkouts and hold requests.

### 12.4 Filtering active vs weeded copies
At the top of the **Catalog** tab, use the status filter dropdown:
- **Active Collection**: Displays active books available on shelves.
- **Weeded Items**: Displays all withdrawn books with their withdrawal date and reason.

### 12.5 Restoring weeded items to active circulation
If a lost book is subsequently returned by a student, open **Catalog → Weeded Items**, select the book, and click **Restore Item**. The copy is instantly returned to active circulation.

### 12.6 Permanent deletion vs weeding
- **Weeding (Recommended)**: Preserves catalog record, accession number, and historical loan counts.
- **Permanent Deletion**: Completely removes the record from the database. Requires administrator password.

---

## 13. Remote Access, QR Code Pairing & Mobile Web App

### 13.1 Architecture & network connectivity
ErrinILS includes a built-in, zero-dependency pure C# HTTP/1.1 server (`TcpListener`). It binds to `0.0.0.0`, allowing access across all school network interfaces:
- Ethernet LAN cables
- School Wi-Fi (2.4 GHz and 5 GHz SSIDs)
- Mobile Wi-Fi hotspots
- Tailscale mesh VPNs and Cloudflare Tunnels for remote inter-campus access

### 13.2 Enabling the remote web server
1. Open **Settings → Remote Access**.
2. Check the box **Enable Remote Web Server**.
3. Set the HTTP port (default: `8080`).
4. Click **Save Settings**. The status displays: *"Running on http://[Your-PC-IP]:8080"*.

### 13.3 Instant QR Code & 6-digit PIN pairing
To connect a mobile device without typing complex passwords:
1. In **Settings → Remote Access**, click **Pair New Device**.
2. A pairing dialog appears displaying a crisp, zero-dependency QR code and a temporary 6-digit PIN (e.g. `849 201`).
3. Scan the QR code using your smartphone or tablet camera.
4. The mobile browser opens the ErrinILS Web SPA with the pairing PIN pre-filled.
5. Tap **Verify & Pair**.
6. The server generates a persistent 32-byte cryptographically secure device token (`X-Errin-Token`) stored in your phone's browser storage. You will never need to enter a password again on that device.

> [!NOTE]
> Pairing PINs expire automatically after **5 minutes** and lock out after **3 failed attempts** to prevent brute-force attacks.

### 13.4 Using phone camera as a wireless barcode scanner
In the mobile Web App, tap the camera icon next to any barcode input field. Your smartphone's camera activates as a high-speed barcode scanner, allowing teachers and prefects to walk down library aisles performing shelf audits and stocktaking wirelessly.

### 13.5 Managing paired devices & token security
In **Settings → Remote Access**, view the **Paired Devices** register showing device name, IP address, pairing date, and last active timestamp. Click **Revoke** next to any device to instantly terminate its access.

---

## 14. Reports, Reading Champions & Analytics

### 14.1 Executive metrics & library participation
Open the **Reports** tab for deep collection intelligence:
- **Active Copies vs. Weeded Copies**
- **Active Borrowing Participation Rate**: Percentage of registered school students who borrowed at least one book this academic term.
- **Fines Collection Ratio**: Percentage of assessed fines successfully collected.

### 14.2 Reading Champions leaderboard (Nilam awards)
ErrinILS automatically tracks reading volume per student:
- **Reading Champions Leaderboard**: Ranked table of the top 25 student readers by total books borrowed.
- Perfect for generating Malaysian school **Nilam Program** award lists, certificates of achievement, and annual prize-giving ceremonies.

### 14.3 Circulation trajectory (6-month loan trends)
A visual historical bar chart displays monthly borrowing volume over the past 6 calendar months, enabling librarians to identify seasonal reading peaks during exam periods and school holidays.

### 14.4 Collection classification (Dewey Decimal 100s)
Categorizes your entire collection into the 10 core Dewey Decimal classes:
- `000` Computer Science, Information & General Works
- `100` Philosophy & Psychology
- `200` Religion
- `300` Social Sciences
- `400` Language
- `500` Science
- `600` Technology & Applied Science
- `700` Arts & Recreation
- `800` Literature
- `900` History & Geography

### 14.5 Dormant collection audit (Zero-circulation tracking)
Lists titles that have had zero borrowings over the past 12 months. Librarians can use this audit to move dormant titles to prominent display shelves or consider them for weeding.

### 14.6 One-Click Full Report export (CSV spreadsheet)
Click **Export Full Report (CSV)** to generate a comprehensive spreadsheet containing executive summaries, overdue notices, fine registers, and Reading Champion rankings ready for Microsoft Excel or Google Sheets.

---

## 15. Backup, Restore and Disaster Recovery

### 15.1 Why back up & disaster scenarios
Your library records are kept on the library computer. If the hard drive suffers hardware failure, ransomware infection, or physical theft, your records are gone without an off-site backup.

### 15.2 Making a backup (JSON & built-in ZIP archive)
1. Open the **Backup** tab.
2. Click **Download Backup (JSON)** or **Export Backup (ZIP)**.
3. Choose the destination folder (e.g. a USB flash drive).
4. ErrinILS creates a timestamped archive (e.g. `errin-backup-2026-10-09.zip`).
5. Remove the USB flash drive and store it in a secure location away from the computer.

### 15.3 Restoring from a backup
1. Open the **Backup** tab.
2. Click **Restore from backup (password)**.
3. Enter the administrator password.
4. Select the backup `.json` or `.zip` file.
5. Review the confirmation warning: *"Replace ALL current data with this backup?"*. Click **Yes**.
6. The database is restored and verified immediately.

### 15.4 Moving to a new computer
1. On the old computer, export a backup to a USB thumb drive.
2. Install ErrinILS on the new computer.
3. Open the **Backup** tab on the new computer and click **Restore from backup**.
4. All catalogued books, students, loans, and settings transfer seamlessly.

### 15.5 Recommended backup habits (3-2-1 strategy)
- **Daily**: Allow the automatic backup (`errin-data.bak`) to protect against sudden power cuts.
- **Weekly**: Export a backup `.zip` to an external USB flash drive every Friday afternoon.
- **Termly**: Upload a copy of the backup archive to official school cloud storage (Google Drive or OneDrive).
- **Test Restores**: Once per school term, test-restore a backup onto a spare PC to verify backup file integrity.

---

## 16. Settings and Administration

### 16.1 Opening Settings
Click the **Settings** tab. When prompted, enter the administrator password. The tab remains unlocked while you work on that page.

### 16.2 Configuring library policies
- **School / Library Name**: Appears on library cards, label headers, and executive reports.
- **Fine per Day Late**: Monetary fine charged per day overdue in RM (default: `RM 0.10`).
- **Max Items per Student**: Maximum concurrent checkouts permitted (default: `3`).
- **Block Checkout at Unpaid Fines of**: Cumulative fine threshold triggering borrowing blocks (default: `RM 5.00`).
- Click **Save** to apply changes.

### 16.3 Changing the administrator password
1. In the **Admin password** section, enter the **Current password**.
2. Enter the **New password** (minimum 4 characters) and re-type in **Confirm password**.
3. Click **Change password**. Store the new password in the school principal's office safe.

### 16.4 Data retention settings
Displays the automated 5-year inactive patron purge policy and provides the **Purge Inactive Data** button for annual audit cleanup.

### 16.5 Exporting the catalogue to another system
If your school ever transitions to another system (e.g. Koha, Follett Destiny, or Alma), you can export your entire collection without vendor lock-in:
- **Download Full Export (.zip)**: Exports your entire catalogue in 5 standard formats:
  1. `catalog.mrc`: Binary ISO 2709 MARC 21 standard.
  2. `catalog.xml`: Standard MARCXML.
  3. `catalog.mrk`: MarcEdit mnemonic format.
  4. `catalog.json`: Structured MARC-in-JSON.
  5. `catalog.csv`: Spreadsheet table.

### 16.6 The Z39.50 server (Optional network sharing)
Allows other libraries on your school district network to search your catalogue:
- **Server**: On / Off toggle.
- **Port**: Network port (default: `210`).
- **Database Name**: Identifier for remote clients (default: `errin`).
- **Reachable From**: *This computer only* (localhost) or *Other computers* (LAN).

### 16.7 The local database path
Displays the exact file path where your active database is stored (`%APPDATA%\Errin\errin-data.json`).

---

## 17. Recommended Operational Routines

### 17.1 Every day the library is open
1. **Morning**: Review the Dashboard Overdue Items table and note students requiring reminder notices.
2. **Desk Duty**: Operate Circulation or launch Simplified View for student self-service.
3. **Returns**: Put aside books flagged with *"Hold ready for [Student]"* and notify the reserving child.
4. **Fines**: Settle cash fines collected during recess in the **Fines** tab.

### 17.2 Every week
1. **Backups**: Export a `.zip` backup to an external USB flash drive every Friday.
2. **Holds Audit**: Review the **Holds queue** in Circulation and cancel uncollected reservations.
3. **Weeding Review**: Inspect books returned with severe damage and mark them weeded.

### 17.3 Every term
1. **Student Registration**: Onboard new students and print scannable library cards.
2. **Catalog Additions**: Catalog newly purchased books using 5-source ISBN lookup; apply spine and barcode labels.
3. **Analytics**: Review Reading Champions leaderboard to award school Nilam certificates.
4. **Disaster Recovery Drill**: Verify backup integrity by performing a test restore on a secondary PC.

### 17.4 Every year
1. **Annual Stocktaking**: Walk the library shelves with a barcode scanner or paired smartphone camera to audit inventory.
2. **Data Retention**: Run **Tools → Purge Inactive Data** to clear records of students who graduated 5 years ago.
3. **Policy Review**: Review fine rates, loan limits, and change the administrator password if student prefects have changed.

---

## 18. Troubleshooting Guide

### 18.1 Comprehensive problem & resolution table

| Problem Encountered | Likely Root Cause | Exact Solution |
|---|---|---|
| **"Windows protected your PC" on installation** | Executable is not digitally signed with a commercial certificate. | Click **More info**, then click **Run anyway**. Verify SHA-256 hash if needed. |
| **Setup reports .NET Framework missing** | Older Windows 7 without .NET 4.0 runtime. | Download and install free **Microsoft .NET Framework 4.8** from Microsoft. |
| **Status bar is red: "NOT saving"** | Hard drive full, read-only, or folder write permissions revoked. | Free disk space or run as administrator. Immediately export a backup to USB. |
| **Scanner does nothing or types wrong box** | Scanner wedge sent no Enter key, or cursor was not focused in input box. | Click in input box first. Test scanner in Notepad to verify Enter key transmission. |
| **"Student not found"** | IC mistyped or patron not registered. | Search Students tab. Register student if new. Scan card barcode to verify digits. |
| **"Item not found"** | Barcode sticker does not match catalogue record. | Search Catalog by book title. Reprint barcode label or catalog the book. |
| **Student blocked from borrowing** | Unpaid fines exceed RM 5.00 or student holds an overdue book. | Settle fine in Fines tab or return the late book. |
| **Renewal refused** | Book is overdue, renewed twice already, or another student placed a hold. | Return the book to the library desk. |
| **ISBN lookup cannot reach services** | Internet connection offline or local firewall blocking port 80/443/210. | Verify internet access in browser. If on Windows 7, ensure .NET 4.8 is installed. |
| **"No record found for this ISBN"** | Very recent or obscure local publication. | Enter book details manually in **+ Add record**. |
| **Labels print too small or misaligned** | Printer scaling set to *"Fit to page"*. | In Windows Print Dialog, change scaling to **Actual Size (100%)**. |
| **Z39.50 server: "Port already in use"** | Another software uses port 210. | In Settings, change port to `2100` and click Apply. |
| **Phone cannot connect to QR Web App** | Phone is on different Wi-Fi subnet or Windows Firewall blocked port. | Connect phone to the same Wi-Fi SSID; allow ErrinILS on Private Networks in firewall. |
| **Second ErrinILS window will not launch** | Single-instance protection mutex active. | ErrinILS is already running. Check your Windows taskbar or system tray. |
| **"Something went wrong" alert dialog** | Unhandled runtime exception occurred. | Click OK (data is preserved). Export a backup and restart ErrinILS. |

### 18.2 Resetting a forgotten administrator password
If staff members forget the administrator password:
1. Close ErrinILS.
2. Press `Win + R`, paste `%APPDATA%\Errin`, and press Enter.
3. Rename or delete `errin-config.json`.
4. Launch ErrinILS. The administrator password resets to the built-in factory default (`admin123`).
5. Open **Settings** and set a new, secure administrator password immediately.
*(Note: Deleting `errin-config.json` resets Z39.50 and web server pairing tokens, but never deletes your library catalogue or student records).*

---

## 19. Frequently Asked Questions (FAQ)

**Can two computers share the same library data across a local network?**  
ErrinILS is architected as an offline-first single-computer system. To share access with secondary devices, enable the built-in **Remote Web Server** in Settings. Secondary computers, tablets, and phones can access circulation and cataloguing wirelessly through their web browsers.

**Does ErrinILS send automatic overdue SMS or email alerts to parents?**  
No. ErrinILS avoids external cloud messaging subscriptions to preserve 100% offline capability and prevent recurring software fees. Guardian phone numbers are stored for reference; use the Dashboard Overdue table to print paper reminder slips.

**Can we change the default 7-day loan period?**  
The standard loan duration is 7 days with up to 2 renewals (up to 21 total days). Fine rates, maximum loan quantities, and fine block thresholds can be customized at any time in Settings.

**Do fines increase while a student still holds the late book?**  
Yes. The Dashboard displays the real-time accruing fine balance under *"Fine on item"*. However, the fine is only officially recorded in the financial ledger when the physical book is returned at the desk.

**A student lost a book. What is the procedure?**  
In the **Catalog** tab, locate the book and click **Weed Copy**. Select **Lost / Unreturned**. In the **Fines** tab, assess the replacement fee according to your school policy.

**How do I catalog a second copy of a book already in the library?**  
Click **+ Add record**, enter the ISBN in **Copy cataloguing**, and click **Look up**. ErrinILS recognizes that you already possess this title and auto-fills the details while generating a fresh, unique barcode for the new physical copy.

**Can I edit a student's IC number or name after registration?**  
To ensure data integrity, IC numbers cannot be renamed. To correct an erroneous registration, delete the student (permitted only when they have zero unreturned books) and register them with the correct details.

**What happens to loan history when a book is weeded or deleted?**  
Weeding preserves 100% of the book's history. If a book is permanently deleted, past loans remain visible in student accounts marked as `(deleted)`.

**Is student data private and secure?**  
100% yes. Your library database never leaves your local computer. When performing ISBN lookups, ErrinILS sends only the book's ISBN code to cataloguing servers. Student names, IC numbers, and loan records are never transmitted over the internet.

---

## Appendix A. Quick Task Reference Guide

| Goal | Navigation Tab | Action to Take |
|---|---|---|
| **Lend a book** | **Circulation** | Scan student card, scan book barcode. |
| **Return a book** | **Circulation** | Scan book barcode in Return box. |
| **Extend loan duration** | **Circulation** | Scan book barcode, click **Renew**. |
| **Reserve an on-loan book** | **Circulation** | In Place Hold card, enter student IC and book barcode. |
| **Add a new book** | **Catalog** | Click **+ Add record**, scan ISBN, click **Look up**, click **Save**. |
| **Batch import books** | **Catalog** | Click **Import MARC**, choose `.mrc` / `.xml` / `.mrk` file. |
| **Print book barcode sticker** | **Catalog** | Select book, click **Label**, choose copies, click **Print**. |
| **Print book spine label** | **Catalog** | Select book, click **Spine label**, click **Print**. |
| **Weed a damaged book** | **Catalog** | Select book, click **Weed Copy**, choose reason, confirm. |
| **Register new student** | **Students** | Click **+ Register student**, enter 12-digit IC, name, class. |
| **Print student library card** | **Students** | Select student, click **Print library card**. |
| **Record fine payment** | **Fines** | Select fine record, click **Pay**. |
| **Identify overdue readers** | **Dashboard** | Review the **Overdue items table**. |
| **Pair mobile phone via QR** | **Settings** | Go to Remote Access, click **Pair New Device**, scan QR. |
| **Export backup to USB** | **Backup** | Click **Export Backup (ZIP)**, save to USB drive. |
| **Customize fine rates** | **Settings** | Enter admin password, modify parameters, click **Save**. |

---

## Appendix B. Glossary of Terms

- **Accession / Acquisition Number**: A sequential identifier assigned to each physical copy in the chronological order it entered the school library collection (`ACQ-000001`).
- **Barcode**: A machine-readable optical representation of digits (Code 128) printed on sticky labels and scanned at the desk (`B1001`).
- **Call Number**: The alphanumeric shelf classification mark (e.g. `823.914 ROW` or `F DAH`) indicating where the book physically sits on the library shelf.
- **Circulation**: The operational process of lending, renewing, and returning library materials.
- **Control Number**: The unique master bibliographic identifier stored in MARC 21 field `001` (`ER00000001`).
- **Dewey Decimal Classification (DDC)**: A standardized international library classification system organizing knowledge into 10 categories from `000` to `900`.
- **Hold (Reservation)**: A queued request reserving a currently borrowed book for a specific patron when returned.
- **ILS (Integrated Library System)**: Software that manages library cataloguing, patron circulation, and inventory tracking.
- **Malaysian IC / MyKid**: The official 12-digit identity number (`YYMMDD-PB-###G`) issued by the National Registration Department of Malaysia, used as the primary patron ID in ErrinILS.
- **MARC 21**: *Machine-Readable Cataloging*—the international standard data format developed by the Library of Congress for exchanging library records.
- **Nilam**: The Malaysian Ministry of Education reading program (*Nadi Ilmu Amalan Membaca*) encouraging students to read books.
- **Weeding**: The deliberate, audited removal of obsolete, lost, or damaged books from the active collection.
- **Z39.50**: ANSI/NISO standard network protocol allowing library catalogues to search and retrieve bibliographic records across remote servers.

---

## Appendix C. Files and Folders Directory Reference

| Component | Standard File System Location |
|---|---|
| **Installed Program Binary** | `C:\Program Files (x86)\ErrinILS\ErrinILS.exe` |
| **Main Library Database** | `%APPDATA%\Errin\errin-data.json` |
| **Automatic Rollback Backup** | `%APPDATA%\Errin\errin-data.bak` |
| **Security & Remote Config** | `%APPDATA%\Errin\errin-config.json` |
| **User Backups (Exported)** | Custom location (e.g. `E:\errin-backup-YYYY-MM-DD.zip`) |
| **Catalogue Exports** | Custom location (e.g. `E:\errin-catalog-YYYY-MM-DD.zip`) |
| **Online Documentation** | `https://rzsy.github.io/ErrinILS/guide.html` |
| **GitHub Open Source Repo** | `https://github.com/RZSY/ErrinILS` |
