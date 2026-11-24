# ErrinILS Scanner (Android Companion App)

**ErrinILS Scanner** (v1.4.3) is a dedicated, high-performance Android companion application for **ErrinILS** (School Integrated Library System). Built natively using **Kotlin**, **Jetpack Compose**, **CameraX**, and **Google ML Kit Barcode Scanning**, it is focused on two core librarian capabilities and bidirectional system integration:

1. **⚡ Barcode Scanner (Wireless Barcode Gun / Wedge)**: Converts your Android device into a high-speed wireless barcode scanner that streams barcodes directly into ErrinILS desktop or any input field on your PC via Wi-Fi (with auto-Enter), or copies scanned codes to clipboard.
2. **📚 Add Book (ISBN Scanner & Cataloging)**: Scan book ISBN barcodes on back covers using the camera (or type manual ISBN), auto-populate bibliographic details via copy-cataloging lookup (ErrinILS 31 union catalogs, Google Books, OpenLibrary), edit fields, and save directly to the school catalog.
3. **🔄 Bidirectional System Connectivity**: Hosts a zero-dependency HTTP server on port `8586` listening for incoming ping latency checks, haptic test beeps, and direct push pairing from the desktop ErrinILS PC.

---

## 🎯 Features

### 1. ⚡ Barcode Scanner (Virtual Barcode Gun / Wedge)
* **Instant ML Kit Scanning**: Sub-second scanning of book barcodes, accession stickers, patron IDs, and spine labels (EAN-13, EAN-8, Code 128, Code 39, QR code, UPC-A, UPC-E).
* **Direct PC Streaming (`/api/wedge`)**: When paired to ErrinILS on desktop (default port `8585`), every scanned barcode is immediately typed into the active input box on your PC + presses Enter.
* **1-Second QR Pairing**: Scan the "Pair a device" QR code displayed on the desktop ErrinILS screen to pair wirelessly over local Wi-Fi.
* **Manual Host & Port Configuration**: Easily configure the PC IP address, port (default `8585`), and 6-digit pairing code in Settings.
* **Scan History & Clipboard**: Displays recent scans with one-tap individual copy, "Copy All", and clear history.
* **Manual Entry Fallback**: Quickly type damaged or faded barcodes.
* **Torch & Feedback**: Camera flashlight toggle, audible beep, and haptic vibration feedback.

### 2. 📚 Add Book (Add Catalog Record with ISBN Lookup)
* **ISBN Camera Scanner**: Dedicated camera viewfinder to scan the printed ISBN-13 / ISBN-10 barcode on the back of any book.
* **Multi-Source ISBN Lookup**: Automatically fetches:
  - **Book Title** (Judul Buku)
  - **Author / Writer** (Pengarang)
  - **Publisher** (Penerbit)
  - **Publication Year** (Tahun Terbitan)
  - **Call Number** (DDC / No. Panggilan)
  - **Subject / Category** (Subjek)
  - Queries ErrinILS PC (31 union catalog sources) or direct online fallbacks (Google Books API & OpenLibrary).
* **Physical Barcode Accessioning**: Scan the accession barcode sticker affixed to the book using the camera to assign its shelf barcode.
* **1-Tap Save**: Saves the record directly into your school library catalog via `/api/item` (or optional direct GitHub catalog sync).
* **Rapid Next Book Accession**: Tap "➕ Register Next Book" to clear the form and instantly open the ISBN camera scanner for high-speed batch accessioning.

### 3. 🔄 Bidirectional PC Connection (Port 8586 HTTP Listener)
* **Embedded Scanner Server**: Runs a pure Kotlin/Java socket server on port `8586` whenever the app is open.
* **Listener URL Display**: Shows `Phone Listener URL: http://<ip>:8586` directly in Settings.
* **Ping & Latency Check**: Responds to `GET /ping` from the desktop system, measuring Wi-Fi round-trip latency in milliseconds.
* **Remote Beep & Haptic Feedback**: Receives `POST /beep` from the PC, immediately triggering an audible tone and vibration pattern so librarians can test physical feedback or locate a misplaced phone.
* **Direct Push Pairing**: Accepts `POST /pair` from desktop ErrinILS ("+ Connect Scanner by IP" or "Discover Scanners"), instantly pairing the phone with zero manual PIN entry required on the mobile screen.

---

## 🛠️ Building & Running

### Building from Source
```cmd
cd ErrinILS-Android
.\gradlew.bat assembleRelease
```
The output APK will be generated at:
```text
ErrinILS-Android/app/build/outputs/apk/release/app-release.apk
```
