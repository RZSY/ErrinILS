package org.errin.scanner.ui.scanner

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Cloud
import androidx.compose.material.icons.filled.CloudSync
import androidx.compose.material.icons.filled.FlashOff
import androidx.compose.material.icons.filled.FlashOn
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FilterChipDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import org.errin.scanner.api.ScannerMode
import org.errin.scanner.camera.CameraScannerView
import org.errin.scanner.ui.components.ScannerOverlay
import org.errin.scanner.ui.theme.ErrinCard
import org.errin.scanner.ui.theme.ErrinDarkBg
import org.errin.scanner.ui.theme.ErrinGreen
import org.errin.scanner.ui.theme.ErrinRed
import org.errin.scanner.ui.theme.ErrinSurface
import org.errin.scanner.ui.theme.ErrinSurfaceVariant
import org.errin.scanner.ui.theme.ErrinTextMuted
import org.errin.scanner.ui.theme.ErrinTextPrimary
import org.errin.scanner.ui.theme.ErrinTextSecondary
import org.errin.scanner.ui.theme.ErrinYellow

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ScannerScreen(
    onNavigateToSettings: () -> Unit,
    modifier: Modifier = Modifier,
    viewModel: ScannerViewModel = viewModel()
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val context = LocalContext.current

    // Request camera permission
    var hasCameraPermission by remember { mutableStateOf(false) }
    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
        onResult = { granted -> hasCameraPermission = granted }
    )

    LaunchedEffect(Unit) {
        permissionLauncher.launch(Manifest.permission.CAMERA)
    }

    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = ErrinDarkBg,
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ErrinSurface),
                title = {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Box(
                            modifier = Modifier
                                .size(28.dp)
                                .clip(RoundedCornerShape(6.dp))
                                .background(ErrinYellow),
                            contentAlignment = Alignment.Center
                        ) {
                            Text("E", color = ErrinDarkBg, fontWeight = FontWeight.Black, fontSize = 16.sp)
                        }
                        Spacer(modifier = Modifier.width(10.dp))
                        Column {
                            Text(
                                "ErrinILS Scanner",
                                color = ErrinTextPrimary,
                                fontSize = 17.sp,
                                fontWeight = FontWeight.Bold
                            )
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Box(
                                    modifier = Modifier
                                        .size(6.dp)
                                        .clip(CircleShape)
                                        .background(ErrinGreen)
                                )
                                Spacer(modifier = Modifier.width(4.dp))
                                Text(
                                    uiState.syncStatusText,
                                    color = ErrinTextMuted,
                                    fontSize = 11.sp,
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis
                                )
                            }
                        }
                    }
                },
                actions = {
                    IconButton(onClick = { viewModel.syncNow() }) {
                        if (uiState.isSyncing) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(20.dp),
                                color = ErrinYellow,
                                strokeWidth = 2.dp
                            )
                        } else {
                            Icon(Icons.Default.CloudSync, contentDescription = "Sync", tint = ErrinYellow)
                        }
                    }
                    IconButton(onClick = { viewModel.toggleTorch() }) {
                        Icon(
                            if (uiState.isTorchOn) Icons.Default.FlashOn else Icons.Default.FlashOff,
                            contentDescription = "Torch",
                            tint = if (uiState.isTorchOn) ErrinYellow else ErrinTextSecondary
                        )
                    }
                    IconButton(onClick = onNavigateToSettings) {
                        Icon(Icons.Default.Settings, contentDescription = "Settings", tint = ErrinTextSecondary)
                    }
                }
            )
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            // Upper View: Camera Preview + HUD
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(0.48f)
                    .background(Color.Black)
            ) {
                if (hasCameraPermission) {
                    CameraScannerView(
                        onBarcodeScanned = { code, format ->
                            viewModel.handleBarcodeScanned(code, format)
                        },
                        isTorchOn = uiState.isTorchOn
                    )
                    ScannerOverlay(
                        hintText = when (uiState.mode) {
                            ScannerMode.RETURN -> "Imbas kod bar buku untuk pulangkan"
                            ScannerMode.CHECKOUT -> if (uiState.activePatron == null) "Imbas kad murid / MyKid" else "Imbas kod bar buku untuk pinjaman"
                            ScannerMode.LOOKUP -> "Arahkan ke kod bar ISBN buku baharu"
                            ScannerMode.INSPECT -> "Imbas kod bar untuk semak maklumat"
                            ScannerMode.WEDGE -> "Imbas untuk salin / hantar terus"
                        }
                    )
                } else {
                    Box(
                        modifier = Modifier.fillMaxSize(),
                        contentAlignment = Alignment.Center
                    ) {
                        Button(
                            onClick = { permissionLauncher.launch(Manifest.permission.CAMERA) },
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow)
                        ) {
                            Text("Beri Kebenaran Kamera", color = ErrinDarkBg)
                        }
                    }
                }

                // Quick manual barcode entry button
                Surface(
                    onClick = { viewModel.showManualDialog(true) },
                    modifier = Modifier
                        .align(Alignment.BottomEnd)
                        .padding(12.dp),
                    shape = RoundedCornerShape(20.dp),
                    color = ErrinCard.copy(alpha = 0.85f),
                    border = borderWithColor(ErrinSurfaceVariant)
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(Icons.Default.Keyboard, contentDescription = "Manual", tint = ErrinYellow, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(6.dp))
                        Text("Taip Kod", color = ErrinTextPrimary, fontSize = 12.sp)
                    }
                }
            }

            // Middle: Mode Switcher Chips
            Surface(
                color = ErrinSurface,
                modifier = Modifier.fillMaxWidth()
            ) {
                LazyRow(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 12.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    items(ScannerMode.values()) { mode ->
                        val isSelected = uiState.mode == mode
                        FilterChip(
                            selected = isSelected,
                            onClick = { viewModel.setMode(mode) },
                            label = {
                                Text(
                                    "${mode.badge} ${mode.title}",
                                    fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal
                                )
                            },
                            colors = FilterChipDefaults.filterChipColors(
                                selectedContainerColor = ErrinYellow,
                                selectedLabelColor = ErrinDarkBg,
                                containerColor = ErrinSurfaceVariant,
                                labelColor = ErrinTextSecondary
                            )
                        )
                    }
                }
            }

            // Bottom: Dynamic Result Card & History List
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(0.52f)
                    .background(ErrinDarkBg)
                    .padding(12.dp)
            ) {
                // Active Result Card
                when (uiState.mode) {
                    ScannerMode.RETURN -> ReturnModeContent(uiState)
                    ScannerMode.CHECKOUT -> CheckoutModeContent(uiState, onClearPatron = { viewModel.clearActivePatron() })
                    ScannerMode.LOOKUP -> LookupModeContent(
                        uiState = uiState,
                        onAddBook = { bc -> viewModel.saveLookedUpBookToCatalog(bc) }
                    )
                    ScannerMode.INSPECT -> InspectModeContent(uiState)
                    ScannerMode.WEDGE -> WedgeModeContent(
                        uiState = uiState,
                        onCopyAll = {
                            val all = uiState.scanHistory.joinToString("\n") { it.barcode }
                            val cm = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            cm.setPrimaryClip(ClipData.newPlainText("Errin Barcodes", all))
                            Toast.makeText(context, "Kod bar disalin ke papan klip!", Toast.LENGTH_SHORT).show()
                        },
                        onClear = { viewModel.clearHistory() }
                    )
                }

                Spacer(modifier = Modifier.height(10.dp))

                // Mini Scan History Log
                if (uiState.scanHistory.isNotEmpty()) {
                    Text(
                        "Rekod Imbasan Sesi Ini (${uiState.scanHistory.size})",
                        color = ErrinTextMuted,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.SemiBold,
                        modifier = Modifier.padding(bottom = 6.dp)
                    )
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxWidth()
                            .weight(1f),
                        verticalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        items(uiState.scanHistory) { entry ->
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clip(RoundedCornerShape(8.dp))
                                    .background(ErrinSurface)
                                    .padding(horizontal = 12.dp, vertical = 8.dp),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically
                            ) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Box(
                                        modifier = Modifier
                                            .size(8.dp)
                                            .clip(CircleShape)
                                            .background(if (entry.isSuccess) ErrinGreen else ErrinRed)
                                    )
                                    Spacer(modifier = Modifier.width(8.dp))
                                    Column {
                                        Text(entry.barcode, color = ErrinTextPrimary, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                                        if (!entry.title.isNullOrEmpty()) {
                                            Text(entry.title, color = ErrinTextMuted, fontSize = 11.sp, maxLines = 1)
                                        }
                                    }
                                }
                                Text(entry.status, color = if (entry.isSuccess) ErrinTextSecondary else ErrinRed, fontSize = 12.sp)
                            }
                        }
                    }
                }
            }
        }
    }

    // Manual Barcode Input Dialog
    if (uiState.showManualInputDialog) {
        var manualText by remember { mutableStateOf("") }
        AlertDialog(
            onDismissRequest = { viewModel.showManualDialog(false) },
            containerColor = ErrinSurface,
            title = { Text("Taip Kod Bar Manual", color = ErrinTextPrimary, fontWeight = FontWeight.Bold) },
            text = {
                Column {
                    Text("Sesuai untuk pelekat kod bar yang pudar atau rosak:", color = ErrinTextSecondary, fontSize = 13.sp)
                    Spacer(modifier = Modifier.height(8.dp))
                    OutlinedTextField(
                        value = manualText,
                        onValueChange = { manualText = it },
                        singleLine = true,
                        placeholder = { Text("cth. B1001 atau 9780064400558", color = ErrinTextMuted) },
                        colors = OutlinedTextFieldDefaults.colors(
                            focusedBorderColor = ErrinYellow,
                            unfocusedBorderColor = ErrinSurfaceVariant,
                            focusedTextColor = ErrinTextPrimary,
                            unfocusedTextColor = ErrinTextPrimary
                        ),
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        if (manualText.isNotBlank()) {
                            viewModel.handleBarcodeScanned(manualText.trim(), 0)
                            viewModel.showManualDialog(false)
                        }
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow)
                ) {
                    Text("Proses", color = ErrinDarkBg, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.showManualDialog(false) }) {
                    Text("Batal", color = ErrinTextSecondary)
                }
            }
        )
    }
}

@Composable
private fun ReturnModeContent(uiState: ScannerUiState) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = ErrinSurface),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("🔄 Mod Pulang Buku", color = ErrinYellow, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                Text(
                    "Jumlah: ${uiState.totalReturnedCount} buah",
                    color = ErrinTextSecondary,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.Medium
                )
            }
            Spacer(modifier = Modifier.height(8.dp))
            val res = uiState.lastCirculationResult
            if (res != null) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(if (res.ok) (if (res.isOverdue) ErrinRed.copy(alpha = 0.2f) else ErrinGreen.copy(alpha = 0.15f)) else ErrinRed.copy(alpha = 0.2f))
                        .padding(12.dp)
                ) {
                    Text(
                        text = res.message.ifEmpty { res.error ?: "" },
                        color = if (res.ok && !res.isOverdue) ErrinGreen else ErrinRed,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
            } else {
                Text(
                    "Imbas kod bar buku di bahagian belakang atau tulang buku untuk memulangkannya ke sistem perpustakaan.",
                    color = ErrinTextMuted,
                    fontSize = 13.sp
                )
            }
        }
    }
}

@Composable
private fun CheckoutModeContent(uiState: ScannerUiState, onClearPatron: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = ErrinSurface),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("📤 Mod Pinjam Buku", color = ErrinYellow, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                Text("Dipinjam: ${uiState.totalBorrowedCount}", color = ErrinTextSecondary, fontSize = 12.sp)
            }
            Spacer(modifier = Modifier.height(8.dp))

            val patron = uiState.activePatron
            if (patron != null) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(ErrinSurfaceVariant)
                        .padding(10.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Person, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(24.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Column {
                            Text(patron.name, color = ErrinTextPrimary, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                            Text("Kelas: ${patron.cls.ifEmpty { "Umum" }} • Pinjaman aktif: ${patron.activeLoansCount}/3", color = ErrinTextSecondary, fontSize = 11.sp)
                        }
                    }
                    Button(
                        onClick = onClearPatron,
                        colors = ButtonDefaults.buttonColors(containerColor = ErrinCard),
                        shape = RoundedCornerShape(6.dp),
                        contentPadding = androidx.compose.foundation.layout.PaddingValues(horizontal = 8.dp, vertical = 2.dp)
                    ) {
                        Text("Tukar", color = ErrinTextPrimary, fontSize = 11.sp)
                    }
                }
            } else {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(ErrinSurfaceVariant)
                        .padding(10.dp)
                ) {
                    Text("Langkah 1: Sila imbas ID Murid / No. MyKid terlebih dahulu.", color = ErrinYellow, fontSize = 13.sp)
                }
            }

            val res = uiState.lastCirculationResult
            if (res != null) {
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    res.message.ifEmpty { res.error ?: "" },
                    color = if (res.ok) ErrinGreen else ErrinRed,
                    fontSize = 12.sp
                )
            }
        }
    }
}

@Composable
private fun LookupModeContent(
    uiState: ScannerUiState,
    onAddBook: (String) -> Unit
) {
    var customBarcode by remember { mutableStateOf("") }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = ErrinSurface),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Text("🔍 Mod Daftar Buku Baharu (ISBN)", color = ErrinYellow, fontSize = 14.sp, fontWeight = FontWeight.Bold)
            Spacer(modifier = Modifier.height(8.dp))

            if (uiState.isLookingUpIsbn) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    CircularProgressIndicator(modifier = Modifier.size(16.dp), color = ErrinYellow, strokeWidth = 2.dp)
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Mencari maklumat di Google Books & OpenLibrary...", color = ErrinTextSecondary, fontSize = 12.sp)
                }
            } else if (uiState.lastIsbnLookup != null) {
                val item = uiState.lastIsbnLookup
                if (item.found) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(8.dp))
                            .background(ErrinSurfaceVariant)
                            .padding(10.dp)
                    ) {
                        Text(item.title, color = ErrinTextPrimary, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                        Text("Pengarang: ${item.author.ifEmpty { "Tidak dinyatakan" }}", color = ErrinTextSecondary, fontSize = 12.sp)
                        Text("Penerbit: ${item.publisher} (${item.year})", color = ErrinTextMuted, fontSize = 11.sp)
                        Text("Sumber: ${item.source}", color = ErrinYellow, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)

                        Spacer(modifier = Modifier.height(8.dp))
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            OutlinedTextField(
                                value = customBarcode,
                                onValueChange = { customBarcode = it },
                                singleLine = true,
                                placeholder = { Text("Auto Barcode", color = ErrinTextMuted, fontSize = 11.sp) },
                                modifier = Modifier
                                    .weight(1f)
                                    .height(48.dp),
                                colors = OutlinedTextFieldDefaults.colors(
                                    focusedBorderColor = ErrinYellow,
                                    unfocusedBorderColor = ErrinCard,
                                    focusedTextColor = ErrinTextPrimary,
                                    unfocusedTextColor = ErrinTextPrimary
                                )
                            )
                            Spacer(modifier = Modifier.width(8.dp))
                            Button(
                                onClick = { onAddBook(customBarcode) },
                                enabled = !uiState.isAddingBook,
                                colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow),
                                shape = RoundedCornerShape(6.dp)
                            ) {
                                if (uiState.isAddingBook) {
                                    CircularProgressIndicator(modifier = Modifier.size(14.dp), color = ErrinDarkBg, strokeWidth = 2.dp)
                                } else {
                                    Text("Simpan ke GitHub", color = ErrinDarkBg, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                                }
                            }
                        }
                    }
                } else {
                    Text(item.error ?: "Tiada maklumat.", color = ErrinRed, fontSize = 12.sp)
                }
            } else {
                Text("Imbas kod bar ISBN pada kulit belakang buku untuk mendapatkan maklumat buku dan mendaftarkannya terus ke GitHub Catalog.", color = ErrinTextMuted, fontSize = 12.sp)
            }
        }
    }
}

@Composable
private fun InspectModeContent(uiState: ScannerUiState) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = ErrinSurface),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Text("📦 Semakan Koleksi & Status", color = ErrinYellow, fontSize = 14.sp, fontWeight = FontWeight.Bold)
            Spacer(modifier = Modifier.height(8.dp))

            val book = uiState.lastBookItem
            if (book != null) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(8.dp))
                        .background(ErrinSurfaceVariant)
                        .padding(10.dp)
                ) {
                    Text(book.title, color = ErrinTextPrimary, fontSize = 15.sp, fontWeight = FontWeight.Bold)
                    Text("Pengarang: ${book.author}", color = ErrinTextSecondary, fontSize = 13.sp)
                    Text("No Panggilan: ${book.call} • Barcode: ${book.barcode}", color = ErrinTextMuted, fontSize = 12.sp)
                    Spacer(modifier = Modifier.height(6.dp))
                    Box(
                        modifier = Modifier
                            .clip(RoundedCornerShape(4.dp))
                            .background(if (book.onLoan) ErrinYellow.copy(alpha = 0.2f) else ErrinGreen.copy(alpha = 0.2f))
                            .padding(horizontal = 8.dp, vertical = 4.dp)
                    ) {
                        Text(
                            text = if (book.onLoan) "Sedang Dipinjam oleh ${book.loanPatronName ?: ""} (Pulang: ${book.loanDue ?: ""})" else "Tersedia di Rak",
                            color = if (book.onLoan) ErrinYellow else ErrinGreen,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }
                }
            } else {
                val err = uiState.lastCirculationResult?.error
                if (err != null) {
                    Text(err, color = ErrinRed, fontSize = 13.sp)
                } else {
                    Text("Imbas mana-mana buku di atas rak untuk menyemak maklumat dan status pinjamannya.", color = ErrinTextMuted, fontSize = 13.sp)
                }
            }
        }
    }
}

@Composable
private fun WedgeModeContent(
    uiState: ScannerUiState,
    onCopyAll: () -> Unit,
    onClear: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = ErrinSurface),
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(14.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("⚡ Mod Pengimbas Kod Bar", color = ErrinYellow, fontSize = 14.sp, fontWeight = FontWeight.Bold)
                Row {
                    TextButton(onClick = onCopyAll) {
                        Text("Salin Semua", color = ErrinYellow, fontSize = 12.sp)
                    }
                    TextButton(onClick = onClear) {
                        Text("Padam", color = ErrinTextMuted, fontSize = 12.sp)
                    }
                }
            }
            val isPcConnected = uiState.lastCirculationResult?.message?.contains("PC") == true || (uiState.scanHistory.any { it.status.contains("PC") })
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(8.dp))
                    .background(ErrinSurfaceVariant)
                    .padding(10.dp)
            ) {
                Text(
                    uiState.lastCirculationResult?.message
                        ?: "🎯 Fungsi Pengimbas Fizikal Tanpa Wayar (Virtual Keyboard Wedge):\nApabila disambungkan ke PC melalui Wi-Fi, setiap kod bar yang diimbas akan ditaip terus ke dalam perisian ErrinILS, Microsoft Excel, Notepad, atau mana-mana kotak input di PC + tekan Enter secara automatik!\n\n💡 Tip: Imbas kod QR 'Pair a device' di skrin PC untuk menghubungkan telefon serta-merta.",
                    color = if (isPcConnected) ErrinGreen else ErrinTextSecondary,
                    fontSize = 12.sp,
                    lineHeight = 17.sp
                )
            }
        }
    }
}

private fun borderWithColor(color: Color) = androidx.compose.foundation.BorderStroke(1.dp, color)
