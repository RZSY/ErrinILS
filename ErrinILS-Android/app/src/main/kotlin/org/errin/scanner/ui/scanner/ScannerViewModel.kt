package org.errin.scanner.ui.scanner

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.errin.scanner.api.BookItem
import org.errin.scanner.api.CirculationResult
import org.errin.scanner.api.ErrinApi
import org.errin.scanner.api.ExternalLookup
import org.errin.scanner.api.IsbnLookupResult
import org.errin.scanner.api.PatronSummary
import org.errin.scanner.api.ScanHistoryEntry
import org.errin.scanner.api.ScannerMode
import org.errin.scanner.data.CloudDataStore
import org.errin.scanner.data.FeedbackManager

data class ScannerUiState(
    val mode: ScannerMode = ScannerMode.RETURN,
    val isTorchOn: Boolean = false,
    val isSyncing: Boolean = false,
    val syncStatusText: String = "Pustaka Terbuka Cloud",
    val activePatron: PatronSummary? = null,
    val lastCirculationResult: CirculationResult? = null,
    val lastBookItem: BookItem? = null,
    val lastIsbnLookup: IsbnLookupResult? = null,
    val isLookingUpIsbn: Boolean = false,
    val isAddingBook: Boolean = false,
    val addBookSuccess: Boolean = false,
    val scanHistory: List<ScanHistoryEntry> = emptyList(),
    val showManualInputDialog: Boolean = false,
    val totalReturnedCount: Int = 0,
    val totalBorrowedCount: Int = 0
)

class ScannerViewModel(application: Application) : AndroidViewModel(application) {

    val cloudDataStore = CloudDataStore(application)
    val feedbackManager = FeedbackManager(application)
    val errinApi = ErrinApi(cloudDataStore.localServerUrl, cloudDataStore.localServerToken)

    private val _uiState = MutableStateFlow(ScannerUiState())
    val uiState: StateFlow<ScannerUiState> = _uiState.asStateFlow()

    init {
        // Load initial offline cache
        cloudDataStore.loadLocalCache()
        updateSyncStatus()

        // Background initial sync with Google Drive and GitHub
        syncNow()
    }

    fun setMode(newMode: ScannerMode) {
        _uiState.update {
            it.copy(
                mode = newMode,
                lastCirculationResult = null,
                lastBookItem = null,
                lastIsbnLookup = null
            )
        }
    }

    fun toggleTorch() {
        _uiState.update { it.copy(isTorchOn = !it.isTorchOn) }
    }

    fun showManualDialog(show: Boolean) {
        _uiState.update { it.copy(showManualInputDialog = show) }
    }

    fun clearActivePatron() {
        _uiState.update { it.copy(activePatron = null) }
    }

    fun clearHistory() {
        _uiState.update { it.copy(scanHistory = emptyList()) }
    }

    fun syncNow() {
        viewModelScope.launch {
            _uiState.update { it.copy(isSyncing = true, syncStatusText = "Menyegerak data awan...") }
            val res = cloudDataStore.syncAll()
            _uiState.update { it.copy(isSyncing = false) }
            if (res.isSuccess) {
                updateSyncStatus()
            } else {
                _uiState.update {
                    it.copy(syncStatusText = "Awan luar talian (${cloudDataStore.cachedItems.size} buku)")
                }
            }
        }
    }

    private fun updateSyncStatus() {
        val bookCount = cloudDataStore.cachedItems.size
        val patronCount = cloudDataStore.cachedPatrons.size
        _uiState.update {
            it.copy(syncStatusText = "SK Taman Ria • $bookCount buku, $patronCount murid")
        }
    }

    fun handleBarcodeScanned(barcode: String, @Suppress("UNUSED_PARAMETER") format: Int) {
        val code = barcode.trim()
        if (code.isEmpty()) return

        when (_uiState.value.mode) {
            ScannerMode.RETURN -> processReturn(code)
            ScannerMode.CHECKOUT -> processCheckout(code)
            ScannerMode.LOOKUP -> processIsbnLookup(code)
            ScannerMode.INSPECT -> processInspect(code)
            ScannerMode.WEDGE -> processWedge(code)
        }
    }

    private fun processReturn(barcode: String) {
        viewModelScope.launch {
            val result = if (cloudDataStore.isStandaloneMode || cloudDataStore.localServerUrl.isEmpty()) {
                cloudDataStore.returnBookStandalone(barcode)
            } else {
                errinApi.returnBook(barcode).getOrElse {
                    cloudDataStore.returnBookStandalone(barcode)
                }
            }

            if (result.ok) {
                if (result.isOverdue) {
                    feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                } else {
                    feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                }
                _uiState.update {
                    val newCount = it.totalReturnedCount + 1
                    val newHistory = listOf(
                        ScanHistoryEntry(
                            barcode = barcode,
                            mode = ScannerMode.RETURN,
                            status = if (result.isOverdue) "LEWAT (Denda)" else "Dipulangkan",
                            isSuccess = true
                        )
                    ) + it.scanHistory.take(40)
                    it.copy(
                        lastCirculationResult = result,
                        totalReturnedCount = newCount,
                        scanHistory = newHistory
                    )
                }
            } else {
                feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                _uiState.update {
                    val newHistory = listOf(
                        ScanHistoryEntry(
                            barcode = barcode,
                            mode = ScannerMode.RETURN,
                            status = result.error ?: "Ralat",
                            isSuccess = false
                        )
                    ) + it.scanHistory.take(40)
                    it.copy(lastCirculationResult = result, scanHistory = newHistory)
                }
            }
        }
    }

    private fun processCheckout(barcode: String) {
        viewModelScope.launch {
            val curPatron = _uiState.value.activePatron

            // If no patron selected yet, check if this barcode is a student ID / IC
            if (curPatron == null) {
                val patron = cloudDataStore.findPatronByIdOrIc(barcode)
                if (patron != null) {
                    feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                    _uiState.update {
                        it.copy(
                            activePatron = patron,
                            lastCirculationResult = CirculationResult(
                                ok = true,
                                message = "Peminjam: ${patron.name} (${patron.cls})\nSila imbas buku sekarang."
                            )
                        )
                    }
                } else {
                    feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                    _uiState.update {
                        it.copy(
                            lastCirculationResult = CirculationResult(
                                ok = false,
                                error = "Kod '$barcode' bukan ID/IC murid berdaftar. Sila imbas kad murid dahulu."
                            )
                        )
                    }
                }
                return@launch
            }

            // Patron is active: check if this is another patron ID or a book
            val anotherPatron = cloudDataStore.findPatronByIdOrIc(barcode)
            if (anotherPatron != null && anotherPatron.id != curPatron.id) {
                // Switch patron
                feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                _uiState.update {
                    it.copy(
                        activePatron = anotherPatron,
                        lastCirculationResult = CirculationResult(
                            ok = true,
                            message = "Ditukar kepada: ${anotherPatron.name} (${anotherPatron.cls})"
                        )
                    )
                }
                return@launch
            }

            // Issue book loan
            val result = if (cloudDataStore.isStandaloneMode || cloudDataStore.localServerUrl.isEmpty()) {
                cloudDataStore.checkoutBookStandalone(curPatron.id, barcode)
            } else {
                errinApi.checkoutBook(curPatron.id, barcode).getOrElse {
                    cloudDataStore.checkoutBookStandalone(curPatron.id, barcode)
                }
            }

            if (result.ok) {
                feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                _uiState.update {
                    val newCount = it.totalBorrowedCount + 1
                    val newHistory = listOf(
                        ScanHistoryEntry(
                            barcode = barcode,
                            mode = ScannerMode.CHECKOUT,
                            title = curPatron.name,
                            status = "Dipinjam",
                            isSuccess = true
                        )
                    ) + it.scanHistory.take(40)
                    it.copy(
                        lastCirculationResult = result,
                        totalBorrowedCount = newCount,
                        scanHistory = newHistory
                    )
                }
            } else {
                feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                _uiState.update {
                    val newHistory = listOf(
                        ScanHistoryEntry(
                            barcode = barcode,
                            mode = ScannerMode.CHECKOUT,
                            status = result.error ?: "Ralat",
                            isSuccess = false
                        )
                    ) + it.scanHistory.take(40)
                    it.copy(lastCirculationResult = result, scanHistory = newHistory)
                }
            }
        }
    }

    private fun processIsbnLookup(isbn: String) {
        viewModelScope.launch {
            _uiState.update {
                it.copy(
                    isLookingUpIsbn = true,
                    lastIsbnLookup = null,
                    addBookSuccess = false
                )
            }

            val lookupRes = if (!cloudDataStore.isStandaloneMode && cloudDataStore.localServerUrl.isNotEmpty()) {
                errinApi.lookupIsbn(isbn).getOrElse { ExternalLookup.lookupIsbnOnline(isbn) }
            } else {
                ExternalLookup.lookupIsbnOnline(isbn)
            }

            _uiState.update { it.copy(isLookingUpIsbn = false, lastIsbnLookup = lookupRes) }

            if (lookupRes.found) {
                feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
            } else {
                feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
            }
        }
    }

    fun saveLookedUpBookToCatalog(customBarcode: String) {
        val lookup = _uiState.value.lastIsbnLookup ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(isAddingBook = true) }
            val nextBc = customBarcode.ifBlank {
                "B" + (1000 + cloudDataStore.cachedItems.size + 1)
            }
            val book = BookItem(
                id = java.util.UUID.randomUUID().toString().replace("-", "").substring(0, 16),
                barcode = nextBc,
                title = lookup.title,
                author = lookup.author,
                isbn = lookup.isbn,
                publisher = lookup.publisher,
                year = lookup.year,
                call = lookup.call.ifEmpty { "F" },
                subject = lookup.subject
            )

            val res = cloudDataStore.addBookToGitHubCatalog(book)
            _uiState.update {
                it.copy(
                    isAddingBook = false,
                    addBookSuccess = res.isSuccess,
                    lastCirculationResult = if (res.isSuccess) {
                        CirculationResult(ok = true, message = "Buku '${book.title}' berjaya didaftarkan ke GitHub Catalog! (No Barcode: ${book.barcode})")
                    } else {
                        CirculationResult(ok = false, error = "Gagal simpan ke GitHub: ${res.exceptionOrNull()?.message}")
                    }
                )
            }
            if (res.isSuccess) {
                feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                updateSyncStatus()
            } else {
                feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
            }
        }
    }

    private fun processInspect(barcode: String) {
        val book = cloudDataStore.findBookByBarcode(barcode)
        if (book != null) {
            val openLoan = cloudDataStore.findOpenLoan(book.id)
            val patronName = if (openLoan != null) {
                val pid = openLoan.optString("pid", "")
                cloudDataStore.findPatronByIdOrIc(pid)?.name ?: pid
            } else null
            val dueDate = if (openLoan != null && openLoan.has("due")) openLoan.optString("due") else null

            val enrichedBook = book.copy(
                onLoan = openLoan != null,
                loanPatronName = patronName,
                loanDue = dueDate
            )

            feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
            _uiState.update {
                it.copy(
                    lastBookItem = enrichedBook,
                    lastCirculationResult = null
                )
            }
        } else {
            feedbackManager.playAlert(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
            _uiState.update {
                it.copy(
                    lastBookItem = null,
                    lastCirculationResult = CirculationResult(
                        ok = false,
                        error = "Buku '$barcode' tiada dalam katalog SK Taman Ria."
                    )
                )
            }
        }
    }

    private fun processWedge(barcode: String) {
        viewModelScope.launch {
            // Check if this is a desktop pairing QR code: e.g. http://192.168.1.50:8585/?code=123456
            if ((barcode.startsWith("http://") || barcode.startsWith("https://")) && barcode.contains("code=")) {
                val uri = android.net.Uri.parse(barcode)
                val codeParam = uri.getQueryParameter("code")
                val host = uri.host ?: ""
                val port = uri.port
                if (codeParam != null && host.isNotEmpty()) {
                    val scheme = uri.scheme ?: "http"
                    val serverUrl = "$scheme://$host" + (if (port != -1) ":$port" else "")
                    val pairRes = errinApi.pair(serverUrl, codeParam, "ErrinILS Android Scanner")
                    if (pairRes.isSuccess && pairRes.getOrNull()?.ok == true) {
                        cloudDataStore.localServerUrl = serverUrl
                        cloudDataStore.localServerToken = pairRes.getOrNull()?.token ?: ""
                        feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
                        _uiState.update {
                            it.copy(
                                lastCirculationResult = CirculationResult(
                                    ok = true,
                                    message = "✅ Berjaya dipasangkan dengan Komputer PC ($serverUrl)!\nSekarang telefon akan menaip terus ke PC seperti pengimbas tanpa wayar fizikal."
                                )
                            )
                        }
                        return@launch
                    }
                }
            }

            var sentToPc = false
            if (cloudDataStore.localServerUrl.isNotEmpty()) {
                val res = errinApi.sendWedge(barcode, enter = true)
                sentToPc = res.isSuccess && (res.getOrNull() == true)
            }

            feedbackManager.playSuccess(cloudDataStore.soundEnabled, cloudDataStore.vibrateEnabled)
            _uiState.update {
                val statusText = if (sentToPc) "Ditaip ke PC ✓" else "Diimbas (Klip)"
                val entry = ScanHistoryEntry(
                    barcode = barcode,
                    mode = ScannerMode.WEDGE,
                    status = statusText,
                    isSuccess = true
                )
                it.copy(
                    scanHistory = listOf(entry) + it.scanHistory.take(50),
                    lastCirculationResult = CirculationResult(
                        ok = true,
                        message = if (sentToPc) "✅ Ditaip terus ke PC: $barcode" else "Imbasan: $barcode"
                    )
                )
            }
        }
    }

    override fun onCleared() {
        super.onCleared()
        feedbackManager.release()
    }
}
