# ErrinILS Scanner (Android Companion App)

**ErrinILS Scanner** is the high-performance Android companion application for **ErrinILS** (School Integrated Library System). Built natively using **Kotlin**, **Jetpack Compose**, **CameraX**, and **Google ML Kit Barcode Scanning**, it is engineered for teachers, school media librarians (*Guru Perpustakaan dan Media*), and student librarians (*Pengawas PSS*).

---

## 🚀 Standalone Cloud Architecture (No PC Required!)

The app operates in **100% Standalone Mode** by default, connecting directly to your school's cloud infrastructure:

* **Google Drive Student Records (`tamanria.sk@gmail.com`)**:
  * Syncs active student rosters, current borrowings, and unpaid fines directly via Google Apps Script Web App:
    `https://script.google.com/macros/s/AKfycbzXCg-AsaOjAAUokIwe0_YfCdoKXiyEEMsIqJqxW2mq_Chw03VREzoQAm-7hxGHE5X6/exec`
* **GitHub Open Catalog (`pustaka-terbuka/sk-taman-ria`)**:
  * Reads the school's bibliographic catalog from GitHub (`catalog.json`).
  * Enables direct one-tap cataloging of newly scanned books with instant commits via your GitHub PAT token (`gho_...`).
* **Offline First Resilience**:
  * All catalog books and student records are cached on the device. Even if Wi-Fi drops at the far corner of the library, loans, returns, and barcode lookups continue to work seamlessly.

*(Optional Local Mode: If desktop ErrinILS is running on the local Wi-Fi, the app can also connect directly via 1-second QR pairing to stream barcodes to the PC screen.)*

---

## 📱 5 Core Scanning Modes

1. **🔄 Pulang Buku (Check-In / Return)**
   * Rapid continuous scanning of book barcodes or spine labels.
   * Instant visual & audio feedback: Book Title, Borrower, Due Date.
   * **Overdue Alert:** Automatically flags late returns and displays calculated fines in Malaysian Ringgit (`RM 0.10/day`).

2. **📤 Pinjam Buku (Check-Out / Borrowing)**
   * **Step 1:** Scan student barcode (Student ID or Malaysian MyKid/IC `YYMMDD-PB-###G`). Displays student photo avatar, class, and active borrowings.
   * **Step 2:** Continuously scan books to issue loans.
   * Enforces 3-book loan limit and blocks loans if overdue fines exist.

3. **🔍 Daftar Buku Baharu (ISBN Quick-Cataloging)**
   * Scan physical printed ISBN-13 / ISBN-10 barcodes on book covers.
   * Queries **Google Books** and **OpenLibrary** APIs directly.
   * Displays title, author, publisher, year, and subject.
   * **1-Tap Save:** Automatically assigns next accession barcode and commits the book to your GitHub catalog (`pustaka-terbuka/sk-taman-ria`).

4. **📦 Semak Koleksi & Inventori (Shelf Audit / Inspect)**
   * Scan any barcode in the stacks.
   * Displays whether the item is **Available on Shelf** (*Tersedia di Rak*) or **On Loan** (*Sedang Dipinjam oleh [Nama Murid] sehingga [Tarikh]*).

5. **⚡ Pengimbas Kod Bar (Virtual Barcode Gun / Wedge)**
   * Converts the Android smartphone into a handheld wireless barcode gun.
   * Scans stacks of books and allows one-tap **Copy All** to clipboard or live streaming.

---

## 🛠️ Installation & Build

### 1. Pre-built APK
The ready-to-install standalone APK is located at:
```text
dist/ErrinILS-Scanner.apk
```

To install directly to a connected Android phone via ADB:
```cmd
adb install -r dist/ErrinILS-Scanner.apk
```
Or simply copy `ErrinILS-Scanner.apk` to your phone via USB or WhatsApp/Google Drive and tap to install.

### 2. Building from Source
```cmd
cd ErrinILS-Android
.\gradlew.bat assembleDebug
```
The APK will be generated at: `app/build/outputs/apk/debug/app-debug.apk`.
