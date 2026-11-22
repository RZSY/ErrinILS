# ErrinILS Scanner (Android Companion App)

**ErrinILS Scanner** is a dedicated, high-performance Android companion application for **ErrinILS** (School Integrated Library System). Built natively using **Kotlin**, **Jetpack Compose**, **CameraX**, and **Google ML Kit Barcode Scanning**, it is focused entirely on two core librarian capabilities:

1. **⚡ Pengimbas Kod Bar (Wireless Barcode Gun / Wedge)**: Converts your Android device into a high-speed wireless barcode scanner that streams barcodes directly into ErrinILS desktop or any input field on your PC via Wi-Fi (with auto-Enter), or copies scanned codes to clipboard.
2. **📚 Daftar Rekod Katalog Baharu (Add New Catalog Record with ISBN Scanner)**: Scan book ISBN barcodes on back covers using the camera (or type manual ISBN), auto-populate bibliographic details via copy-cataloging lookup (ErrinILS union catalogs, Google Books, OpenLibrary), edit fields, and save directly to the school catalog.

---

## 🎯 Features

### 1. ⚡ Pengimbas Kod Bar (Virtual Barcode Gun / Wedge)
* **Instant ML Kit Scanning**: Sub-second scanning of book barcodes, accession stickers, patron IDs, and spine labels (EAN-13, EAN-8, Code 128, Code 39, QR code, UPC-A, UPC-E).
* **Direct PC Streaming (`/api/wedge`)**: When paired to ErrinILS on desktop, every scanned barcode is immediately typed into the active input box on your PC + presses Enter.
* **1-Second QR Pairing**: Scan the "Pair a device" QR code displayed on the desktop ErrinILS screen to pair wirelessly over local Wi-Fi.
* **Scan History & Clipboard**: Displays recent scans with one-tap individual copy, "Salin Semua" (Copy All), and clear history.
* **Manual Entry Fallback**: Quickly type damaged or faded barcodes.
* **Torch & Feedback**: Camera flashlight toggle, audible beep, and haptic vibration feedback.

### 2. 📚 Daftar Buku Baharu (Add Catalog Record with ISBN Lookup)
* **ISBN Camera Scanner**: Dedicated camera viewfinder to scan the printed ISBN-13 / ISBN-10 barcode on the back of any book.
* **Multi-Source ISBN Lookup**: Automatically fetches:
  - **Judul Buku** (Title)
  - **Pengarang** (Author)
  - **Penerbit** (Publisher)
  - **Tahun Terbitan** (Publication Year)
  - **No. Panggilan** (DDC / Call Number)
  - **Subjek / Kategori** (Subject)
  - Queries ErrinILS PC (31 union catalog sources) or direct online fallbacks (Google Books API & OpenLibrary).
* **Physical Barcode Accessioning**: Scan the accession barcode sticker affixed to the book using the camera to assign its shelf barcode.
* **1-Tap Save**: Saves the record directly into your school library catalog via `/api/item` (or optional direct GitHub catalog sync).
* **Rapid Next Book Accession**: Tap "➕ Daftar Buku Seterusnya" to clear the form and instantly open the ISBN camera scanner for high-speed batch accessioning.

---

## 🛠️ Building & Running

### Building from Source
```cmd
cd ErrinILS-Android
.\gradlew.bat assembleDebug
```
The output APK will be generated at:
```text
ErrinILS-Android/app/build/outputs/apk/debug/app-debug.apk
```

To install directly to a connected device via ADB:
```cmd
adb install -r app/build/outputs/apk/debug/app-debug.apk
```
