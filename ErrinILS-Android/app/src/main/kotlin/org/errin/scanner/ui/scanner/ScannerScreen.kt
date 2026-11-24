package org.errin.scanner.ui.scanner

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
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
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.ContentCopy
import androidx.compose.material.icons.filled.FlashOff
import androidx.compose.material.icons.filled.FlashOn
import androidx.compose.material.icons.filled.Keyboard
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.TabRowDefaults
import androidx.compose.material3.TabRowDefaults.tabIndicatorOffset
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
import org.errin.scanner.api.AppTab
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
        viewModel.checkPcConnection()
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
                                fontSize = 16.sp,
                                fontWeight = FontWeight.Bold
                            )
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Box(
                                    modifier = Modifier
                                        .size(7.dp)
                                        .clip(CircleShape)
                                        .background(if (uiState.isConnectedToPc) ErrinGreen else Color(0xFFFFA000))
                                )
                                Spacer(modifier = Modifier.width(4.dp))
                                Text(
                                    uiState.connectionStatusText,
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
                    IconButton(onClick = { viewModel.checkPcConnection() }) {
                        Icon(Icons.Default.Refresh, contentDescription = "Refresh Connection", tint = ErrinYellow)
                    }
                    IconButton(onClick = { viewModel.toggleTorch() }) {
                        Icon(
                            if (uiState.isTorchOn) Icons.Default.FlashOn else Icons.Default.FlashOff,
                            contentDescription = "Flashlight",
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
            // Tab Header (Two core capabilities: Barcode Scanner & Add Catalog Record)
            TabRow(
                selectedTabIndex = uiState.currentTab.ordinal,
                containerColor = ErrinSurface,
                contentColor = ErrinYellow,
                indicator = { tabPositions ->
                    TabRowDefaults.SecondaryIndicator(
                        Modifier.tabIndicatorOffset(tabPositions[uiState.currentTab.ordinal]),
                        color = ErrinYellow
                    )
                }
            ) {
                Tab(
                    selected = uiState.currentTab == AppTab.SCANNER,
                    onClick = { viewModel.setTab(AppTab.SCANNER) },
                    text = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text("⚡", fontSize = 14.sp)
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                "Barcode Scanner",
                                fontWeight = if (uiState.currentTab == AppTab.SCANNER) FontWeight.Bold else FontWeight.Normal,
                                fontSize = 13.sp
                            )
                        }
                    },
                    selectedContentColor = ErrinYellow,
                    unselectedContentColor = ErrinTextSecondary
                )
                Tab(
                    selected = uiState.currentTab == AppTab.ADD_CATALOG,
                    onClick = { viewModel.setTab(AppTab.ADD_CATALOG) },
                    text = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Text("📚", fontSize = 14.sp)
                            Spacer(modifier = Modifier.width(6.dp))
                            Text(
                                "Add Book (ISBN)",
                                fontWeight = if (uiState.currentTab == AppTab.ADD_CATALOG) FontWeight.Bold else FontWeight.Normal,
                                fontSize = 13.sp
                            )
                        }
                    },
                    selectedContentColor = ErrinYellow,
                    unselectedContentColor = ErrinTextSecondary
                )
            }

            // Tab Content
            when (uiState.currentTab) {
                AppTab.SCANNER -> {
                    BarcodeScannerTabContent(
                        uiState = uiState,
                        hasCameraPermission = hasCameraPermission,
                        onScanBarcode = { code, format -> viewModel.handleBarcodeScanned(code, format) },
                        onManualDialog = { viewModel.showManualBarcodeDialog(true) },
                        onRequestCamera = { permissionLauncher.launch(Manifest.permission.CAMERA) },
                        onCopyAll = {
                            val all = uiState.scanHistory.joinToString("\n") { it.barcode }
                            val cm = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            cm.setPrimaryClip(ClipData.newPlainText("Errin Barcodes", all))
                            Toast.makeText(context, "All barcodes copied to clipboard!", Toast.LENGTH_SHORT).show()
                        },
                        onCopyOne = { code ->
                            val cm = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
                            cm.setPrimaryClip(ClipData.newPlainText("Errin Barcode", code))
                            Toast.makeText(context, "Copied: $code", Toast.LENGTH_SHORT).show()
                        },
                        onClearHistory = { viewModel.clearHistory() }
                    )
                }

                AppTab.ADD_CATALOG -> {
                    AddCatalogTabContent(
                        uiState = uiState,
                        hasCameraPermission = hasCameraPermission,
                        onRequestCamera = { permissionLauncher.launch(Manifest.permission.CAMERA) },
                        onScanBarcode = { code, format -> viewModel.handleBarcodeScanned(code, format) },
                        onStartScanIsbn = { viewModel.setIsbnCameraScanning(true) },
                        onStopScanIsbn = { viewModel.setIsbnCameraScanning(false) },
                        onStartScanItemBarcode = { viewModel.setItemBarcodeCameraScanning(true) },
                        onStopScanItemBarcode = { viewModel.setItemBarcodeCameraScanning(false) },
                        onIsbnChange = { viewModel.updateIsbnInput(it) },
                        onLookupIsbn = { viewModel.lookupIsbn() },
                        onTitleChange = { viewModel.updateFormTitle(it) },
                        onAuthorChange = { viewModel.updateFormAuthor(it) },
                        onPublisherChange = { viewModel.updateFormPublisher(it) },
                        onYearChange = { viewModel.updateFormYear(it) },
                        onBarcodeChange = { viewModel.updateFormBarcode(it) },
                        onCallChange = { viewModel.updateFormCall(it) },
                        onSubjectChange = { viewModel.updateFormSubject(it) },
                        onSaveRecord = { viewModel.saveCatalogRecord() },
                        onPrepareNext = { viewModel.prepareNextBook() }
                    )
                }
            }
        }
    }

    // Manual Barcode Dialog for Tab 1
    if (uiState.showManualBarcodeDialog) {
        var manualText by remember { mutableStateOf("") }
        AlertDialog(
            onDismissRequest = { viewModel.showManualBarcodeDialog(false) },
            containerColor = ErrinSurface,
            title = { Text("Type Barcode Manually", color = ErrinTextPrimary, fontWeight = FontWeight.Bold) },
            text = {
                Column {
                    Text("Enter book or student barcode if the camera cannot read the label:", color = ErrinTextSecondary, fontSize = 13.sp)
                    Spacer(modifier = Modifier.height(10.dp))
                    OutlinedTextField(
                        value = manualText,
                        onValueChange = { manualText = it },
                        singleLine = true,
                        placeholder = { Text("e.g. B1001 or 9789830112345", color = ErrinTextMuted) },
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            },
            confirmButton = {
                Button(
                    onClick = {
                        if (manualText.isNotBlank()) {
                            viewModel.handleBarcodeScanned(manualText.trim(), 0)
                            viewModel.showManualBarcodeDialog(false)
                        }
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow)
                ) {
                    Text("Submit", color = ErrinDarkBg, fontWeight = FontWeight.Bold)
                }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.showManualBarcodeDialog(false) }) {
                    Text("Cancel", color = ErrinTextSecondary)
                }
            }
        )
    }
}

/**
 * Tab 1: Virtual Barcode Gun / Wedge Screen
 */
@Composable
private fun BarcodeScannerTabContent(
    uiState: ScannerUiState,
    hasCameraPermission: Boolean,
    onScanBarcode: (String, Int) -> Unit,
    onManualDialog: () -> Unit,
    onRequestCamera: () -> Unit,
    onCopyAll: () -> Unit,
    onCopyOne: (String) -> Unit,
    onClearHistory: () -> Unit
) {
    Column(modifier = Modifier.fillMaxSize()) {
        // Upper View: Camera Preview + HUD
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .weight(0.50f)
                .background(Color.Black)
        ) {
            if (hasCameraPermission) {
                CameraScannerView(
                    onBarcodeScanned = onScanBarcode,
                    isTorchOn = uiState.isTorchOn
                )
                ScannerOverlay(
                    hintText = if (uiState.isConnectedToPc) "Point camera at barcode to type directly to PC" else "Point camera at barcode to scan"
                )
            } else {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    Button(
                        onClick = onRequestCamera,
                        colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow)
                    ) {
                        Text("Grant Camera Permission", color = ErrinDarkBg)
                    }
                }
            }

            // Quick manual barcode entry button
            Surface(
                onClick = onManualDialog,
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(12.dp),
                shape = RoundedCornerShape(20.dp),
                color = ErrinCard.copy(alpha = 0.88f)
            ) {
                Row(
                    modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(Icons.Default.Keyboard, contentDescription = "Manual", tint = ErrinYellow, modifier = Modifier.size(16.dp))
                    Spacer(modifier = Modifier.width(6.dp))
                    Text("Type Manual", color = ErrinTextPrimary, fontSize = 12.sp)
                }
            }
        }

        // Lower View: Status + Scan History
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .weight(0.50f)
                .background(ErrinDarkBg)
                .padding(12.dp)
        ) {
            // Live Status Card
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(10.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            if (uiState.lastScannedBarcode.isNotEmpty()) "Recent Scan:" else "Scanner Status:",
                            color = ErrinYellow,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.SemiBold
                        )
                        Text(
                            uiState.lastScanStatus.ifEmpty {
                                if (uiState.isConnectedToPc) "Ready to scan. Barcodes are typed directly into PC."
                                else "Standalone mode. Scan to copy barcode, or scan PC pairing QR code."
                            },
                            color = if (uiState.lastScanSuccess) ErrinTextPrimary else ErrinRed,
                            fontSize = 13.sp,
                            fontWeight = FontWeight.Medium
                        )
                    }
                    if (uiState.lastScannedBarcode.isNotEmpty()) {
                        IconButton(onClick = { onCopyOne(uiState.lastScannedBarcode) }) {
                            Icon(Icons.Default.ContentCopy, contentDescription = "Copy", tint = ErrinYellow)
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(10.dp))

            // Scan History Header
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    "Recent Scans This Session (${uiState.scanHistory.size})",
                    color = ErrinTextMuted,
                    fontSize = 12.sp,
                    fontWeight = FontWeight.SemiBold
                )
                Row {
                    if (uiState.scanHistory.isNotEmpty()) {
                        TextButton(onClick = onCopyAll) {
                            Text("Copy All", color = ErrinYellow, fontSize = 12.sp)
                        }
                        TextButton(onClick = onClearHistory) {
                            Text("Clear", color = ErrinTextMuted, fontSize = 12.sp)
                        }
                    }
                }
            }

            // History List
            if (uiState.scanHistory.isEmpty()) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .weight(1f),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        "No barcodes scanned yet.\nPoint camera at any book or patron card barcode.",
                        color = ErrinTextMuted,
                        fontSize = 12.sp,
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center
                    )
                }
            } else {
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
                                .clickable { onCopyOne(entry.barcode) }
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
                                Text(
                                    entry.barcode,
                                    color = ErrinTextPrimary,
                                    fontSize = 14.sp,
                                    fontWeight = FontWeight.Bold
                                )
                            }
                            Text(
                                entry.status,
                                color = if (entry.isSuccess) ErrinTextSecondary else ErrinRed,
                                fontSize = 12.sp
                            )
                        }
                    }
                }
            }
        }
    }
}

/**
 * Tab 2: Add New Catalog Record with ISBN Lookup & ISBN Barcode Scanner
 */
@Composable
private fun AddCatalogTabContent(
    uiState: ScannerUiState,
    hasCameraPermission: Boolean,
    onRequestCamera: () -> Unit,
    onScanBarcode: (String, Int) -> Unit,
    onStartScanIsbn: () -> Unit,
    onStopScanIsbn: () -> Unit,
    onStartScanItemBarcode: () -> Unit,
    onStopScanItemBarcode: () -> Unit,
    onIsbnChange: (String) -> Unit,
    onLookupIsbn: () -> Unit,
    onTitleChange: (String) -> Unit,
    onAuthorChange: (String) -> Unit,
    onPublisherChange: (String) -> Unit,
    onYearChange: (String) -> Unit,
    onBarcodeChange: (String) -> Unit,
    onCallChange: (String) -> Unit,
    onSubjectChange: (String) -> Unit,
    onSaveRecord: () -> Unit,
    onPrepareNext: () -> Unit
) {
    // If camera is currently active for scanning ISBN or Accession Barcode
    if (uiState.isScanningIsbnCamera || uiState.isScanningItemBarcodeCamera) {
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(Color.Black)
        ) {
            if (hasCameraPermission) {
                CameraScannerView(
                    onBarcodeScanned = onScanBarcode,
                    isTorchOn = uiState.isTorchOn
                )
                ScannerOverlay(
                    hintText = if (uiState.isScanningIsbnCamera)
                        "Point at ISBN barcode (EAN-13) on book back cover"
                    else
                        "Point at book barcode sticker (e.g. B1001)"
                )
            } else {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center
                ) {
                    Button(
                        onClick = onRequestCamera,
                        colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow)
                    ) {
                        Text("Grant Camera Permission", color = ErrinDarkBg)
                    }
                }
            }

            // Top banner with close button
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp)
                    .align(Alignment.TopCenter),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Surface(
                    color = ErrinCard.copy(alpha = 0.9f),
                    shape = RoundedCornerShape(16.dp)
                ) {
                    Text(
                        if (uiState.isScanningIsbnCamera) "🔍 Scan Book ISBN Barcode" else "🏷️ Scan Book Barcode Sticker",
                        color = ErrinYellow,
                        fontSize = 13.sp,
                        fontWeight = FontWeight.Bold,
                        modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp)
                    )
                }

                IconButton(
                    onClick = {
                        if (uiState.isScanningIsbnCamera) onStopScanIsbn()
                        else onStopScanItemBarcode()
                    },
                    modifier = Modifier
                        .size(36.dp)
                        .clip(CircleShape)
                        .background(ErrinCard.copy(alpha = 0.9f))
                ) {
                    Icon(Icons.Default.Close, contentDescription = "Close Camera", tint = Color.White)
                }
            }
        }
        return
    }

    // Standard Form View
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(14.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        // Section 1: ISBN Lookup Box with Camera Scan Button
        Card(
            colors = CardDefaults.cardColors(containerColor = ErrinSurface),
            shape = RoundedCornerShape(12.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(14.dp)) {
                Text(
                    "🔍 Book Information Lookup (ISBN)",
                    color = ErrinYellow,
                    fontWeight = FontWeight.Bold,
                    fontSize = 14.sp
                )
                Text(
                    "Scan the ISBN barcode on the book cover or type the ISBN to look up details automatically.",
                    color = ErrinTextMuted,
                    fontSize = 12.sp
                )

                Spacer(modifier = Modifier.height(10.dp))

                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = uiState.isbnInput,
                        onValueChange = onIsbnChange,
                        singleLine = true,
                        placeholder = { Text("e.g. 9789830112345", color = ErrinTextMuted, fontSize = 13.sp) },
                        modifier = Modifier.weight(1f),
                        colors = outlinedColors()
                    )

                    Button(
                        onClick = onLookupIsbn,
                        enabled = !uiState.isLookingUpIsbn && uiState.isbnInput.isNotBlank(),
                        colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow),
                        shape = RoundedCornerShape(8.dp)
                    ) {
                        if (uiState.isLookingUpIsbn) {
                            CircularProgressIndicator(modifier = Modifier.size(16.dp), color = ErrinDarkBg, strokeWidth = 2.dp)
                        } else {
                            Icon(Icons.Default.Search, contentDescription = "Search", tint = ErrinDarkBg, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Search", color = ErrinDarkBg, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                        }
                    }
                }

                Spacer(modifier = Modifier.height(10.dp))

                // Button to Launch Camera for ISBN barcode scanning
                Button(
                    onClick = onStartScanIsbn,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.buttonColors(containerColor = ErrinSurfaceVariant),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    Icon(Icons.Default.QrCodeScanner, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Scan ISBN Barcode with Camera", color = ErrinTextPrimary, fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
                }

                // Lookup source indicator badge
                if (uiState.lastIsbnLookup != null && uiState.lastIsbnLookup.found) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "✅ Information retrieved from: ${uiState.lastIsbnLookup.source}",
                        color = ErrinGreen,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Medium
                    )
                }
            }
        }

        // Section 2: Book Catalog Record Form
        Card(
            colors = CardDefaults.cardColors(containerColor = ErrinSurface),
            shape = RoundedCornerShape(12.dp),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(
                modifier = Modifier.padding(14.dp),
                verticalArrangement = Arrangement.spacedBy(10.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        "📖 Catalog Record Details",
                        color = ErrinYellow,
                        fontWeight = FontWeight.Bold,
                        fontSize = 14.sp
                    )
                    if (uiState.formTitle.isNotEmpty()) {
                        TextButton(onClick = onPrepareNext) {
                            Text("Clear", color = ErrinTextMuted, fontSize = 11.sp)
                        }
                    }
                }

                // Title
                OutlinedTextField(
                    value = uiState.formTitle,
                    onValueChange = onTitleChange,
                    label = { Text("Book Title *", fontSize = 12.sp) },
                    singleLine = true,
                    colors = outlinedColors(),
                    modifier = Modifier.fillMaxWidth()
                )

                // Author
                OutlinedTextField(
                    value = uiState.formAuthor,
                    onValueChange = onAuthorChange,
                    label = { Text("Author / Writer", fontSize = 12.sp) },
                    singleLine = true,
                    colors = outlinedColors(),
                    modifier = Modifier.fillMaxWidth()
                )

                // Publisher & Year
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = uiState.formPublisher,
                        onValueChange = onPublisherChange,
                        label = { Text("Publisher", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.weight(1.8f)
                    )
                    OutlinedTextField(
                        value = uiState.formYear,
                        onValueChange = onYearChange,
                        label = { Text("Year", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.weight(1f)
                    )
                }

                // Accession Barcode + Camera Scan Button
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = uiState.formBarcode,
                        onValueChange = onBarcodeChange,
                        label = { Text("Book Barcode (Accession No.)", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.weight(1f)
                    )
                    Button(
                        onClick = onStartScanItemBarcode,
                        colors = ButtonDefaults.buttonColors(containerColor = ErrinSurfaceVariant),
                        shape = RoundedCornerShape(8.dp)
                    ) {
                        Icon(Icons.Default.QrCodeScanner, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(16.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Scan", color = ErrinTextPrimary, fontSize = 12.sp)
                    }
                }

                // Call Number & Subject
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    OutlinedTextField(
                        value = uiState.formCall,
                        onValueChange = onCallChange,
                        label = { Text("Call Number (DDC)", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.weight(1f)
                    )
                    OutlinedTextField(
                        value = uiState.formSubject,
                        onValueChange = onSubjectChange,
                        label = { Text("Subject / Category", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.weight(1.5f)
                    )
                }

                Spacer(modifier = Modifier.height(4.dp))

                // Error Banner
                if (uiState.saveErrorMessage != null) {
                    Text(
                        uiState.saveErrorMessage ?: "",
                        color = ErrinRed,
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Medium
                    )
                }

                // Success Banner
                if (uiState.saveSuccessMessage != null) {
                    Column(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(8.dp))
                            .background(ErrinGreen.copy(alpha = 0.15f))
                            .padding(10.dp)
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.CheckCircle, contentDescription = null, tint = ErrinGreen, modifier = Modifier.size(18.dp))
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                uiState.saveSuccessMessage ?: "",
                                color = ErrinGreen,
                                fontSize = 13.sp,
                                fontWeight = FontWeight.Bold
                            )
                        }
                        Spacer(modifier = Modifier.height(8.dp))
                        Button(
                            onClick = onPrepareNext,
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinGreen),
                            shape = RoundedCornerShape(6.dp)
                        ) {
                            Text("➕ Register Next Book", color = Color.White, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                        }
                    }
                }

                // Save Button
                Button(
                    onClick = onSaveRecord,
                    enabled = !uiState.isSavingBook && uiState.formTitle.isNotBlank(),
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow),
                    shape = RoundedCornerShape(8.dp)
                ) {
                    if (uiState.isSavingBook) {
                        CircularProgressIndicator(modifier = Modifier.size(20.dp), color = ErrinDarkBg, strokeWidth = 2.dp)
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Saving to catalog...", color = ErrinDarkBg, fontWeight = FontWeight.Bold)
                    } else {
                        Icon(Icons.Default.Save, contentDescription = null, tint = ErrinDarkBg)
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Save to Catalog", color = ErrinDarkBg, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }
                }
            }
        }
    }
}

@Composable
private fun outlinedColors() = OutlinedTextFieldDefaults.colors(
    focusedBorderColor = ErrinYellow,
    unfocusedBorderColor = ErrinSurfaceVariant,
    focusedTextColor = ErrinTextPrimary,
    unfocusedTextColor = ErrinTextPrimary,
    focusedLabelColor = ErrinYellow,
    unfocusedLabelColor = ErrinTextMuted
)
