package org.errin.scanner.ui.scanner

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.errin.scanner.api.AppTab
import org.errin.scanner.api.BookItem
import org.errin.scanner.api.ErrinApi
import org.errin.scanner.api.ExternalLookup
import org.errin.scanner.api.IsbnLookupResult
import org.errin.scanner.api.ScanHistoryEntry
import org.errin.scanner.core.AppPreferences
import org.errin.scanner.core.FeedbackManager

data class ScannerUiState(
    val currentTab: AppTab = AppTab.SCANNER,
    val isTorchOn: Boolean = false,
    val isConnectedToPc: Boolean = false,
    val pcServerUrl: String = "",
    val connectionStatusText: String = "Standalone Mode (Not connected to PC)",
    // Tab 1: Barcode Scanner Gun
    val lastScannedBarcode: String = "",
    val lastScanStatus: String = "",
    val lastScanSuccess: Boolean = true,
    val scanHistory: List<ScanHistoryEntry> = emptyList(),
    val showManualBarcodeDialog: Boolean = false,
    // Tab 2: Add Catalog Record with ISBN Lookup
    val isbnInput: String = "",
    val isScanningIsbnCamera: Boolean = false,
    val isScanningItemBarcodeCamera: Boolean = false,
    val isLookingUpIsbn: Boolean = false,
    val lastIsbnLookup: IsbnLookupResult? = null,
    val formTitle: String = "",
    val formAuthor: String = "",
    val formPublisher: String = "",
    val formYear: String = "",
    val formIsbn: String = "",
    val formBarcode: String = "",
    val formCall: String = "",
    val formSubject: String = "",
    val isSavingBook: Boolean = false,
    val saveSuccessMessage: String? = null,
    val saveErrorMessage: String? = null
)

class ScannerViewModel(application: Application) : AndroidViewModel(application) {

    val prefs = AppPreferences(application)
    val feedbackManager = FeedbackManager(application)
    val errinApi = ErrinApi(prefs.serverUrl, prefs.serverToken)

    private val _uiState = MutableStateFlow(ScannerUiState())
    val uiState: StateFlow<ScannerUiState> = _uiState.asStateFlow()

    init {
        checkPcConnection()
    }

    fun setTab(tab: AppTab) {
        _uiState.update {
            it.copy(
                currentTab = tab,
                isScanningIsbnCamera = false,
                isScanningItemBarcodeCamera = false
            )
        }
    }

    fun toggleTorch() {
        _uiState.update { it.copy(isTorchOn = !it.isTorchOn) }
    }

    fun showManualBarcodeDialog(show: Boolean) {
        _uiState.update { it.copy(showManualBarcodeDialog = show) }
    }

    fun clearHistory() {
        _uiState.update { it.copy(scanHistory = emptyList()) }
    }

    fun checkPcConnection() {
        viewModelScope.launch {
            val url = prefs.serverUrl
            if (url.isNotBlank()) {
                errinApi.updateCredentials(url, prefs.serverToken)
                val verify = errinApi.verifyConnection()
                val ok = verify.isSuccess && verify.getOrNull()?.ok == true
                val displayHost = if (prefs.serverHost.isNotBlank()) "${prefs.serverHost}:${prefs.serverPort}" else url.removePrefix("http://").removePrefix("https://")
                _uiState.update {
                    it.copy(
                        isConnectedToPc = ok,
                        pcServerUrl = url,
                        connectionStatusText = if (ok) {
                            "Connected to PC ($displayHost)"
                        } else {
                            "PC Offline ($displayHost)"
                        }
                    )
                }
            } else {
                _uiState.update {
                    it.copy(
                        isConnectedToPc = false,
                        pcServerUrl = "",
                        connectionStatusText = "Standalone Mode (Tap Settings to connect PC)"
                    )
                }
            }
        }
    }

    fun handleBarcodeScanned(barcodeRaw: String, @Suppress("UNUSED_PARAMETER") format: Int) {
        val code = barcodeRaw.trim()
        if (code.isEmpty()) return

        val state = _uiState.value

        // Check if user is on Tab 2 and currently scanning an ISBN or Barcode with the camera
        if (state.currentTab == AppTab.ADD_CATALOG) {
            if (state.isScanningIsbnCamera) {
                _uiState.update {
                    it.copy(
                        isScanningIsbnCamera = false,
                        isbnInput = code
                    )
                }
                feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)
                lookupIsbn(code)
                return
            }

            if (state.isScanningItemBarcodeCamera) {
                _uiState.update {
                    it.copy(
                        isScanningItemBarcodeCamera = false,
                        formBarcode = code
                    )
                }
                feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)
                return
            }
        }

        // Tab 1: Barcode Scanner Gun
        processScannerGun(code)
    }

    private fun processScannerGun(barcode: String) {
        viewModelScope.launch {
            // Check if this is an ErrinILS desktop URL or pairing QR code
            if (barcode.startsWith("http://") || barcode.startsWith("https://")) {
                val uri = try { android.net.Uri.parse(barcode) } catch (_: Exception) { null }
                if (uri != null) {
                    val host = uri.host ?: ""
                    val port = if (uri.port != -1) uri.port else 8585
                    val scheme = uri.scheme ?: "http"
                    val codeParam = uri.getQueryParameter("code")

                    if (host.isNotEmpty()) {
                        val serverUrl = "$scheme://$host:$port"

                        // Case A: Pairing QR Code (contains ?code=123456)
                        if (!codeParam.isNullOrBlank()) {
                            val pairRes = errinApi.pair(serverUrl, codeParam, prefs.deviceName)
                            if (pairRes.isSuccess && pairRes.getOrNull()?.ok == true) {
                                prefs.serverHost = host
                                prefs.serverPort = port.toString()
                                prefs.serverUrl = serverUrl
                                prefs.serverToken = pairRes.getOrNull()?.token ?: ""
                                errinApi.updateCredentials(serverUrl, prefs.serverToken)

                                feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)
                                _uiState.update {
                                    it.copy(
                                        isConnectedToPc = true,
                                        pcServerUrl = serverUrl,
                                        connectionStatusText = "Connected to PC ($host:$port)",
                                        lastScannedBarcode = barcode,
                                        lastScanStatus = "✅ Successfully paired with Desktop PC!",
                                        lastScanSuccess = true
                                    )
                                }
                                return@launch
                            } else {
                                feedbackManager.playAlert(prefs.soundEnabled, prefs.vibrateEnabled)
                                _uiState.update {
                                    it.copy(
                                        lastScannedBarcode = barcode,
                                        lastScanStatus = "❌ Pairing failed: ${pairRes.getOrNull()?.error ?: "Check PC"}",
                                        lastScanSuccess = false
                                    )
                                }
                                return@launch
                            }
                        }

                        // Case B: General ErrinILS Remote URL (from 'Show QR Code' on PC)
                        prefs.serverHost = host
                        prefs.serverPort = port.toString()
                        prefs.serverUrl = serverUrl
                        errinApi.updateCredentials(serverUrl, prefs.serverToken)

                        // If already paired with token, test connection
                        if (prefs.serverToken.isNotEmpty()) {
                            val verify = errinApi.verifyConnection()
                            val ok = verify.isSuccess && verify.getOrNull()?.ok == true
                            if (ok) {
                                feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)
                                _uiState.update {
                                    it.copy(
                                        isConnectedToPc = true,
                                        pcServerUrl = serverUrl,
                                        connectionStatusText = "Connected to PC ($host:$port)",
                                        lastScannedBarcode = barcode,
                                        lastScanStatus = "✅ Connected to PC ($host:$port)",
                                        lastScanSuccess = true
                                    )
                                }
                                return@launch
                            }
                        }

                        // Needs pairing code
                        feedbackManager.playAlert(prefs.soundEnabled, prefs.vibrateEnabled)
                        _uiState.update {
                            it.copy(
                                lastScannedBarcode = barcode,
                                lastScanStatus = "Server set to $host:$port. Click '+ Pair new device' on PC for pairing code.",
                                lastScanSuccess = true
                            )
                        }
                        return@launch
                    }
                }
            }

            var sentToPc = false
            if (prefs.serverUrl.isNotBlank() && prefs.serverToken.isNotBlank()) {
                val res = errinApi.sendWedge(barcode, prefs.autoEnter)
                sentToPc = res.isSuccess && (res.getOrNull() == true)
            }

            feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)

            val statusText = if (sentToPc) "Typed directly to PC ✓" else "Scanned (Copied to clipboard)"
            val newEntry = ScanHistoryEntry(
                barcode = barcode,
                status = statusText,
                isSuccess = true
            )

            _uiState.update {
                it.copy(
                    lastScannedBarcode = barcode,
                    lastScanStatus = if (sentToPc) "✅ Typed to PC: $barcode" else "Scanned: $barcode",
                    lastScanSuccess = true,
                    scanHistory = listOf(newEntry) + it.scanHistory.take(50)
                )
            }
        }
    }

    // Tab 2 Functions: ISBN Scanning & Lookup
    fun setIsbnCameraScanning(active: Boolean) {
        _uiState.update {
            it.copy(
                isScanningIsbnCamera = active,
                isScanningItemBarcodeCamera = false
            )
        }
    }

    fun setItemBarcodeCameraScanning(active: Boolean) {
        _uiState.update {
            it.copy(
                isScanningItemBarcodeCamera = active,
                isScanningIsbnCamera = false
            )
        }
    }

    fun updateIsbnInput(input: String) {
        _uiState.update { it.copy(isbnInput = input) }
    }

    fun lookupIsbn(isbnRaw: String = _uiState.value.isbnInput) {
        val clean = isbnRaw.replace(Regex("[^0-9X]"), "")
        if (clean.length < 9) {
            feedbackManager.playAlert(prefs.soundEnabled, prefs.vibrateEnabled)
            _uiState.update {
                it.copy(
                    saveErrorMessage = "Please enter a valid ISBN (at least 10 digits).",
                    saveSuccessMessage = null
                )
            }
            return
        }

        viewModelScope.launch {
            _uiState.update {
                it.copy(
                    isLookingUpIsbn = true,
                    saveSuccessMessage = null,
                    saveErrorMessage = null
                )
            }

            // 1. Try desktop ErrinILS server if connected (queries 31 union catalogs)
            var result: IsbnLookupResult? = null
            if (prefs.isConnectedToPc) {
                val apiRes = errinApi.lookupIsbn(clean)
                if (apiRes.isSuccess && apiRes.getOrNull()?.found == true) {
                    result = apiRes.getOrNull()
                }
            }

            // 2. Fallback to direct online search (Google Books + OpenLibrary)
            if (result == null || !result.found) {
                val onlineRes = ExternalLookup.lookupIsbnOnline(clean)
                if (onlineRes.found) {
                    result = onlineRes
                }
            }

            if (result != null && result.found) {
                feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)
                val suggestedBarcode = if (_uiState.value.formBarcode.isBlank()) {
                    "B" + (System.currentTimeMillis() % 100000).toString().padStart(5, '0')
                } else _uiState.value.formBarcode

                _uiState.update {
                    it.copy(
                        isLookingUpIsbn = false,
                        lastIsbnLookup = result,
                        isbnInput = clean,
                        formTitle = result.title,
                        formAuthor = result.author,
                        formPublisher = result.publisher,
                        formYear = result.year,
                        formIsbn = clean,
                        formBarcode = suggestedBarcode,
                        formCall = result.call.ifBlank { "F" },
                        formSubject = result.subject,
                        saveErrorMessage = null
                    )
                }
            } else {
                feedbackManager.playAlert(prefs.soundEnabled, prefs.vibrateEnabled)
                _uiState.update {
                    it.copy(
                        isLookingUpIsbn = false,
                        lastIsbnLookup = result,
                        formIsbn = clean,
                        saveErrorMessage = "No information found for ISBN $clean. You may fill in the details manually.",
                        saveSuccessMessage = null
                    )
                }
            }
        }
    }

    // Form field setters
    fun updateFormTitle(v: String) = _uiState.update { it.copy(formTitle = v) }
    fun updateFormAuthor(v: String) = _uiState.update { it.copy(formAuthor = v) }
    fun updateFormPublisher(v: String) = _uiState.update { it.copy(formPublisher = v) }
    fun updateFormYear(v: String) = _uiState.update { it.copy(formYear = v) }
    fun updateFormIsbn(v: String) = _uiState.update { it.copy(formIsbn = v) }
    fun updateFormBarcode(v: String) = _uiState.update { it.copy(formBarcode = v) }
    fun updateFormCall(v: String) = _uiState.update { it.copy(formCall = v) }
    fun updateFormSubject(v: String) = _uiState.update { it.copy(formSubject = v) }

    fun saveCatalogRecord() {
        val state = _uiState.value
        val title = state.formTitle.trim()
        val barcode = state.formBarcode.trim().ifBlank {
            "B" + (System.currentTimeMillis() % 100000).toString().padStart(5, '0')
        }

        if (title.isBlank()) {
            feedbackManager.playAlert(prefs.soundEnabled, prefs.vibrateEnabled)
            _uiState.update { it.copy(saveErrorMessage = "Please enter the Book Title.") }
            return
        }

        val book = BookItem(
            barcode = barcode,
            title = title,
            author = state.formAuthor.trim(),
            isbn = state.formIsbn.trim(),
            publisher = state.formPublisher.trim(),
            year = state.formYear.trim(),
            call = state.formCall.trim().ifBlank { "F" },
            subject = state.formSubject.trim()
        )

        viewModelScope.launch {
            _uiState.update { it.copy(isSavingBook = true, saveErrorMessage = null, saveSuccessMessage = null) }

            var saved = false
            var errorMsg: String? = null

            // 1. Save to PC server if connected
            if (prefs.isConnectedToPc) {
                val res = errinApi.saveItem(book)
                if (res.isSuccess) {
                    saved = true
                } else {
                    errorMsg = res.exceptionOrNull()?.message
                }
            }

            // 2. If PC not available or failed, save to GitHub if configured
            if (!saved && prefs.githubRepo.isNotBlank() && prefs.githubToken.isNotBlank()) {
                val ghRes = errinApi.saveItemToGitHub(prefs.githubRepo, prefs.githubToken, book)
                if (ghRes.isSuccess) {
                    saved = true
                    errorMsg = null
                } else if (errorMsg == null) {
                    errorMsg = ghRes.exceptionOrNull()?.message
                }
            }

            if (saved) {
                feedbackManager.playSuccess(prefs.soundEnabled, prefs.vibrateEnabled)
                _uiState.update {
                    it.copy(
                        isSavingBook = false,
                        saveSuccessMessage = "Book '$title' successfully added to catalog! (Barcode: $barcode)",
                        saveErrorMessage = null
                    )
                }
            } else {
                feedbackManager.playAlert(prefs.soundEnabled, prefs.vibrateEnabled)
                _uiState.update {
                    it.copy(
                        isSavingBook = false,
                        saveErrorMessage = errorMsg ?: "Please pair the app with ErrinILS on PC, or configure GitHub in Settings to save.",
                        saveSuccessMessage = null
                    )
                }
            }
        }
    }

    fun prepareNextBook() {
        _uiState.update {
            it.copy(
                isbnInput = "",
                lastIsbnLookup = null,
                formTitle = "",
                formAuthor = "",
                formPublisher = "",
                formYear = "",
                formIsbn = "",
                formBarcode = "",
                formCall = "",
                formSubject = "",
                saveSuccessMessage = null,
                saveErrorMessage = null,
                isScanningIsbnCamera = true
            )
        }
    }

    override fun onCleared() {
        super.onCleared()
        feedbackManager.release()
    }
}
