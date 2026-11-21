package org.errin.scanner.camera

import androidx.annotation.OptIn
import androidx.camera.core.ExperimentalGetImage
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import com.google.mlkit.vision.barcode.BarcodeScannerOptions
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage

class BarcodeAnalyzer(
    private val onBarcodeDetected: (String, Int) -> Unit
) : ImageAnalysis.Analyzer {

    private val options = BarcodeScannerOptions.Builder()
        .setBarcodeFormats(
            Barcode.FORMAT_EAN_13,
            Barcode.FORMAT_EAN_8,
            Barcode.FORMAT_CODE_128,
            Barcode.FORMAT_CODE_39,
            Barcode.FORMAT_QR_CODE,
            Barcode.FORMAT_UPC_A,
            Barcode.FORMAT_UPC_E,
            Barcode.FORMAT_ITF,
            Barcode.FORMAT_DATA_MATRIX
        )
        .build()

    private val scanner = BarcodeScanning.getClient(options)

    private var lastScannedCode: String = ""
    private var lastScanTime: Long = 0L

    @OptIn(ExperimentalGetImage::class)
    override fun analyze(imageProxy: ImageProxy) {
        val mediaImage = imageProxy.image
        if (mediaImage == null) {
            imageProxy.close()
            return
        }

        val image = InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees)
        scanner.process(image)
            .addOnSuccessListener { barcodes ->
                val now = System.currentTimeMillis()
                for (barcode in barcodes) {
                    val rawValue = barcode.rawValue ?: continue
                    if (rawValue.isBlank()) continue

                    // Debounce logic: prevent duplicate scans of the same code within 1600ms
                    if (rawValue == lastScannedCode && (now - lastScanTime) < 1600) {
                        continue
                    }

                    // Fast turnaround for different barcodes (400ms cooldown)
                    if (now - lastScanTime < 400) {
                        continue
                    }

                    lastScannedCode = rawValue
                    lastScanTime = now
                    onBarcodeDetected(rawValue, barcode.format)
                    break
                }
            }
            .addOnCompleteListener {
                imageProxy.close()
            }
    }
}
