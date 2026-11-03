# ErrinILS — Comprehensive User Guide & Librarian Manual
**Version 1.3.1 (Windows Edition)**  
*Lightweight, Zero-Dependency School Integrated Library System*

---

## Table of Contents
1. [Introduction & Core Philosophy](#1-introduction--core-philosophy)
2. [System Requirements & Installation](#2-system-requirements--installation)
3. [First-Time Setup & Configuration](#3-first-time-setup--configuration)
4. [Student & Patron Management](#4-student--patron-management)
5. [Cataloguing & Copy-Cataloguing](#5-cataloguing--copy-cataloguing)
6. [Daily Circulation (Checkouts, Returns, Holds)](#6-daily-circulation)
7. [Student Self-Checkout Kiosk Mode](#7-student-self-checkout-kiosk-mode)
8. [Remote Access, QR Code Pairing & Mobile Web App](#8-remote-access--device-pairing)
9. [Fines Management](#9-fines-management)
10. [Collection Weeding & Maintenance](#10-collection-weeding--maintenance)
11. [Reports, Reading Champions & Analytics](#11-reports--analytics)
12. [Backups & Disaster Recovery](#12-backups--disaster-recovery)
13. [Z39.50 Server Integration](#13-z3950-server-integration)
14. [Troubleshooting & Frequently Asked Questions](#14-troubleshooting--faq)

---

## 1. Introduction & Core Philosophy

**ErrinILS** is an open-source, zero-dependency integrated library management system designed specifically for primary, secondary, and vernacular schools. 

Unlike heavy web-based systems that require Linux servers, Apache/MySQL, or complex cloud subscriptions, ErrinILS runs as a **single standalone Windows executable (`ErrinILS.exe`)**. It requires zero database setup, zero runtime dependencies, and stores everything in a human-readable, portable JSON database (`%APPDATA%\Errin\errin-data.json`).

### Key Design Pillars
- **Strict Zero External Dependencies**: Built with pure C# and standard Microsoft .NET Framework 4.0 assemblies. Runs out-of-the-box on Windows 7, 8, 8.1, 10, and 11.
- **Low Hardware Footprint**: Operates comfortably on budget PCs with as little as 1 GB RAM and ~10 MB disk space.
- **Offline-First Resilience**: Full circulation and cataloguing work completely offline. Internet is only used when querying remote cataloguing databases or pairing remote devices.
- **School Localization**: Built-in support for Malaysian National Registration IC / MyKid formats (`YYMMDD-PB-###G`), Ringgit Malaysia (RM) fine calculations, and child-friendly terminology.

---

## 2. System Requirements & Installation

### System Requirements
| Component | Minimum Requirement | Recommended |
|---|---|---|
| **Operating System** | Windows 7 SP1 (32-bit or 64-bit) | Windows 10 or 11 (64-bit) |
| **Runtime** | Microsoft .NET Framework 4.0 | Microsoft .NET Framework 4.8 (built-in on Win 10/11) |
| **Processor** | 1.0 GHz x86/x64 processor | Dual-core 1.6 GHz or better |
| **RAM** | 1 GB | 2 GB or more |
| **Disk Space** | 10 MB application space | 50 MB for catalog and backups |
| **Display** | 1024 x 600 resolution | 1366 x 768 or higher (High-DPI supported) |
| **Peripherals** | Standard Keyboard & Mouse | USB Barcode Scanner (HID Keyboard emulation), Standard Printer |

### Installation Methods

#### Option A: Windows Installer (Recommended for School Desktops)
1. Download **`ErrinILS-Setup-1.3.1.exe`** from the official release page.
2. Double-click the installer. If a Windows SmartScreen notification appears ("Windows protected your PC"), click **"More info"** and then **"Run anyway"**.
3. Follow the setup wizard. The installer automatically detects whether .NET Framework is installed, creates Start Menu and Desktop shortcuts, and sets up an uninstaller in Control Panel.
4. Check **"Start ErrinILS now"** and click **Finish**.

#### Option B: Standalone Portable Copy (USB Drives & Lab Computers)
1. Download **`ErrinILS-Windows.zip`**.
2. Extract the folder anywhere (e.g. `C:\Library\ErrinILS` or a USB flash drive).
3. Double-click **`ErrinILS.exe`**. No installation or administrator rights required.

---

## 3. First-Time Setup & Configuration

When launching ErrinILS for the first time, navigate to the **Settings** tab in the top navigation bar.

### 1. School Information & Circulation Policies
- **School Name**: Enter the official name of your institution (e.g., `SK Damansara Jaya`, `SMK Convent Bukit Nanas`). This name prints on student library cards and export reports.
- **Loan Duration**: Set default loan duration (default is `7 days`).
- **Fine Rate per Day**: Set daily fine for late returns (default is `RM 0.10`).
- **Borrowing Limit**: Maximum simultaneous items allowed per student (default is `3 books`).
- **Blocking Threshold**: Maximum unpaid fine balance before borrowing is blocked (default is `RM 5.00`).
- Click **Apply Policies** to save changes.

### 2. Admin Password Protection
To protect student privacy and prevent unauthorized deletion of catalog items or fine records:
1. In the **Admin Password** card, enter a new password (minimum 4 characters).
2. Type the password again in the confirmation field and click **Set Password**.
3. Once set, access to Settings, data deletion, and system restoration will require password authentication.

### 3. High-DPI Sharpness & Scaling
ErrinILS includes native PerMonitorV2 high-DPI scaling. On high-resolution laptops (125%, 150%, 200% Windows scaling), text and barcodes render with razor-sharp ClearType grid fitting without blurry font scaling.

---

## 4. Student & Patron Management

Manage patron records under the **Students** tab.

### Registering a Student
1. Click **+ Add Student** (or press the Add button).
2. **IC / MyKid Number**: Enter the 12-digit identification number. Dashes are formatted automatically (`YYMMDD-PB-###G`). The system checks for valid birth month and calendar day.
3. **Full Name**: Enter the student's legal name.
4. **Class / Grade**: Enter class designation (e.g. `3 Amanah`, `5 Cemerlang`, `Grade 7A`).
5. **Guardian Contact**: Phone number or email for overdue notices.
6. Click **Register**.

### Printing Standard Student Library Cards
ErrinILS generates ISO/IEC 7810 ID-1 standard credit-card sized cards (**85.6 x 54 mm**):
1. In the Students list, click the student's name to view their profile.
2. Click **Print Library Card**.
3. The preview dialog displays the school banner, student name, class, IC number, and a high-contrast **Code 128 barcode**.
4. Click **Print Card** to send to any desktop card printer or heavy card stock paper.
5. Students can scan their card barcode at the circulation desk for instantaneous checkouts.

---

## 5. Cataloguing & Copy-Cataloguing

ErrinILS supports both high-speed automated copy-cataloguing and comprehensive MARC21 record editing under the **Catalog** tab.

### High-Speed ISBN Lookup (Copy-Cataloguing)
When adding books, you do not need to type book details manually:
1. Click **+ Add Book** in the Catalog view.
2. In the **ISBN** field, scan or type the book's 10-digit or 13-digit ISBN.
3. ErrinILS automatically queries 5 international library cataloguing databases in sequence:
   - **Library of Congress (Z39.50 `lx2.loc.gov:210`)**
   - **Open Library API**
   - **Google Books API**
   - **HathiTrust Digital Library**
   - **K10plus German Union Catalogue (SRU MARCXML)**
4. Title, Subtitle, Author, Publisher, Year, Dewey Call Number, Pages, and Subjects populate in seconds.
5. Verify the details, assign an accession barcode (or keep the auto-generated barcode), and click **Save**.

### Barcode & Spine Label Printing
- **Item Barcode Labels (70 x 30 mm)**: Print Code 128 barcode labels with book title and call number to affix to the inside back cover.
- **Spine Labels (1 x 1.5 inch)**: Print multi-line Dewey call numbers (e.g. `599.77` over `MIL`) with auto-fitting font sizes.
- Bulk printing is supported across all selected catalog records.

### Importing Existing Catalogues
If migrating from Koha, SLIMS, Electron Errin, or another ILS:
1. Click **Import MARC**.
2. Select any binary MARC21 file (`.mrc`), MARCXML (`.xml`), MarcEdit text (`.mrk`), or JSON.
3. Choose whether to skip duplicates or import as new copies.

---

## 6. Daily Circulation

The **Circulation** tab is the heart of everyday library operations.

### Checking Out a Book
1. Focus is placed on the **Student IC** field. Scan the student's library card barcode (or type their IC).
2. Scan the **Book Barcode**.
3. Click **Check Out** (or press Enter).
4. ErrinILS verifies:
   - Student borrowing limits (not exceeding maximum allowed books).
   - Unreturned overdue items (blocks checkout if overdue books exist).
   - Outstanding fines (blocks if unpaid fines exceed the policy threshold).
   - Active holds on the item.
5. If clear, the loan is recorded with the calculated due date.

### Returning Books
1. Scan the returned book's barcode into the **Item Barcode** box.
2. Click **Return**.
3. If the item is returned late, ErrinILS automatically calculates the fine based on days overdue and prompts whether to collect cash or record as unpaid fine.
4. If a hold is waiting for another student, an alert displays with the reservee's name.

### Renewals
- Books can be renewed if not reserved by another student and not overdue.
- Select the loan from the Active Loans table and click **Renew (+7 days)**.

---

## 7. Student Self-Checkout Kiosk Mode

For busy break times or primary school libraries, ErrinILS features a **Simplified Kiosk Mode**:
1. Click **Simplified Kiosk** on the main navigation bar.
2. The interface switches to full-screen, high-contrast, large-button layout designed for children.
3. Students scan their library card, scan their book, and see friendly animated visual confirmations with book covers.
4. Password protection prevents students from leaving kiosk mode or altering records.

---

## 8. Remote Access, Device Pairing & Mobile Web App

ErrinILS 1.3.1 allows librarians, teachers, and student assistants to circulate books, look up titles, and manage students from **mobile phones, iPads, tablets, or other PCs** over the local school network.

### How to Connect a Phone or iPad via QR Code
1. On the host Windows computer, open **Settings > Remote Access**.
2. Ensure Server is **On** and click **+ Pair New Device**.
3. A pairing dialog appears displaying:
   - A sharp, auto-generated **QR code**.
   - The server network address (e.g., `http://192.168.1.105:8585`).
   - A 6-digit one-time code (valid for 5 minutes).
4. Open the camera on your smartphone or iPad and point it at the QR code on the computer screen.
5. Tap the link banner. The ErrinILS Mobile Web App opens with the 6-digit code **already pre-filled**.
6. Type a device nickname (e.g. *Teacher Room iPad*) and tap **Connect Device**.
7. The device receives a 256-bit cryptographically secure token and is now permanently paired until revoked by the librarian.

### Multi-Network & Hotspot Support
- **School Wi-Fi & VLANs**: Works across all standard school access points.
- **Phone Hotspot / Tethering**: If the school library PC has no router, simply turn on Personal Hotspot on a smartphone, connect the PC to the hotspot, and ErrinILS automatically discovers the hotspot IP.
- **Windows Firewall**: ErrinILS automatically registers itself with the Windows Firewall service on launch.

---

## 9. Fines Management

Access fine tracking under the **Fines** tab.
- **Daily Calculation**: Fines accrue each day an item remains unreturned past its due date (`Days Late × Daily Rate`).
- **Collecting Fines**: Select a fine from the list and click **Mark Paid**.
- **Waiving Fines**: Librarians can waive fines for excused absences or special circumstances by clicking **Waive Fine**.
- **Borrower Blocking**: Patrons owing more than the configured block threshold cannot borrow additional books until settled.

---

## 10. Collection Weeding & Maintenance

Weeding (deselection) removes obsolete, damaged, lost, or duplicate books from active circulation while preserving complete statistical history:
1. In the Catalog tab, select the book copy.
2. Click **Weed Copy**.
3. Choose the withdrawal reason:
   - **Condition / Damaged** (torn pages, water damage, worn bindings)
   - **Obsolete / Outdated** (superseded syllabus, expired reference facts)
   - **Lost / Missing** (unreturned after multiple notices, inventory discrepancy)
   - **Duplicate / Excess** (surplus copies no longer needed)
4. Weeded books are immediately blocked from checkout and holds.
5. In the Catalog filter dropdown, librarians can switch between **Active Items**, **Weeded Items**, or **All Items**.
6. Any weeded book can be restored to active status at any time by clicking **Restore (Unweed)**.

---

## 11. Reports, Reading Champions & Analytics

The **Reports** tab provides real-time circulation intelligence and audit-ready spreadsheets:

### Key Metrics Ribbon
- **Active Collection**: Total clean, active titles available.
- **Books on Loan**: Real-time count of active loans.
- **Overdue Items**: Overdue books count with percentage of active loans.
- **Active Readers**: Student reader participation percentage (percentage of enrolled students who borrow).
- **Fines Assessed & Unpaid**: Total fines accrued vs. outstanding uncollected balance.

### Analytical Visualizations
- **Circulation Trend**: Checkouts per month across the last 6 months.
- **Reading Champions (Leaderboard)**: Top student reader ranking (crucial for school NILAM reading badges, certificates, and prizes).
- **Dewey Decimal Classification (DDC)**: Collection strength across 100s categories (000 Computers, 100 Philosophy, 200 Religion, 300 Social Sciences, 500 Science, 600 Technology, 800 Literature, etc.).
- **Dormant Collection Audit**: Identifies active titles with zero checkout history to guide promotional reading displays or weeding decisions.
- **Weeding Breakdown**: Distribution of withdrawn books by reason.

### Exporting Full Report (CSV)
Click **Export full report (CSV)** at the top of Reports. This exports an executive spreadsheet containing:
1. Executive Summary & School Policies
2. Monthly Checkouts Breakdown
3. Top 25 Most Borrowed Books
4. Top 25 Reading Champions
5. Circulation by Class
6. DDC Subject Breakdown
7. Active Overdue Notices List
8. Outstanding Unpaid Fines List
9. Collection Weeding Log

---

## 12. Backups & Disaster Recovery

Your data is critical. ErrinILS stores data in `%APPDATA%\Errin\errin-data.json` and creates automatic `.bak` snapshot copies on every write.

### Manual Backup (Recommended Weekly)
1. Open the **Backup** tab.
2. Click **Download Backup**.
3. Choose a folder or USB drive (e.g. `errin-backup-2026-11-03.json`).
4. Save the file. Keep a secondary copy on a USB flash drive or school cloud storage (Google Drive / OneDrive).

### Restoring a Backup
1. Click **Restore from backup (password)**.
2. Enter the librarian administrator password.
3. Select your `.json` backup file.
4. Confirm the replacement prompt. ErrinILS restores the complete catalog, student list, active loans, and fine histories instantly.

### Automatic Data Retention (5-Year Purge)
To comply with student data privacy laws, student records with no active loans and zero unpaid fines are automatically pruned 5 years after their last recorded activity.

---

## 13. Z39.50 Server Integration

ErrinILS includes a built-in pure C# **Z39.50 v3 server** (`Z3950.cs`):
- Enables university libraries, state library networks, or district education offices to search your school catalog remotely.
- Listens on standard port 210 (or any custom port).
- Supports ASN.1 Basic Encoding Rules (BER), Type-1 Bib-1 queries (Use attributes 4 for Title, 7 for ISBN, 1003 for Author, 21 for Subject).
- Enable under **Settings > Z39.50 Server**.

---

## 14. Troubleshooting & Frequently Asked Questions

### Q: Windows SmartScreen says "Windows protected your PC" on install.
**A**: This is expected because ErrinILS is an open-source, non-commercial school binary that is not signed with an expensive commercial digital certificate. Click **"More info"**, then **"Run anyway"**.

### Q: A phone or iPad cannot connect to Remote Access.
1. Check that both the computer and the mobile device are connected to the same Wi-Fi network or mobile hotspot.
2. Ensure Remote Access is turned **On** and applied in **Settings**.
3. Verify that Windows Firewall has not blocked ErrinILS. ErrinILS automatically registers rules, but you can also allow port `8585` in Windows Defender Firewall.

### Q: Barcode scanner is not reading barcodes properly.
**A**: Ensure your USB scanner is configured in "HID Keyboard Emulation" mode (default for 99% of barcode scanners) and sends a carriage return (Enter key) after scanning.

### Q: Can ErrinILS run on a USB drive without installing?
**A**: Yes! Unzip `ErrinILS-Windows.zip` onto any USB flash drive and run `ErrinILS.exe`. You can take the library system anywhere.

---
*ErrinILS — Crafted with zero dependencies for schools worldwide.*  
*Documentation updated for Release 1.3.1.*
