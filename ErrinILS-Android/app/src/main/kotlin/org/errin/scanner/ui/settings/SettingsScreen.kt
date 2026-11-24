package org.errin.scanner.ui.settings

import android.widget.Toast
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Code
import androidx.compose.material.icons.filled.Computer
import androidx.compose.material.icons.filled.Link
import androidx.compose.material.icons.filled.LinkOff
import androidx.compose.material.icons.filled.Save
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.VolumeUp
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
import androidx.compose.material3.Switch
import androidx.compose.material3.SwitchDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import kotlinx.coroutines.launch
import org.errin.scanner.api.ErrinApi
import org.errin.scanner.core.AppPreferences
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
fun SettingsScreen(
    onNavigateBack: () -> Unit,
    modifier: Modifier = Modifier
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val prefs = remember { AppPreferences(context) }
    val api = remember { ErrinApi(prefs.serverUrl, prefs.serverToken) }

    var localServerHost by remember { mutableStateOf(prefs.serverHost) }
    var localServerPort by remember { mutableStateOf(prefs.serverPort) }
    var deviceName by remember { mutableStateOf(prefs.deviceName) }
    var pairingCode by remember { mutableStateOf("") }

    var soundEnabled by remember { mutableStateOf(prefs.soundEnabled) }
    var vibrateEnabled by remember { mutableStateOf(prefs.vibrateEnabled) }
    var autoEnter by remember { mutableStateOf(prefs.autoEnter) }
    var githubRepo by remember { mutableStateOf(prefs.githubRepo) }
    var githubToken by remember { mutableStateOf(prefs.githubToken) }

    var isConnecting by remember { mutableStateOf(false) }
    var isTesting by remember { mutableStateOf(false) }
    var statusMessage by remember { mutableStateOf<String?>(null) }
    var statusSuccess by remember { mutableStateOf(false) }

    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = ErrinDarkBg,
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ErrinSurface),
                title = { Text("Scanner Settings", color = ErrinTextPrimary, fontSize = 17.sp, fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back", tint = ErrinYellow)
                    }
                }
            )
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            // 1. Desktop PC Connection
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Computer, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(20.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("ErrinILS Desktop Connection", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    Text(
                        "Connect this device to ErrinILS on your PC to stream barcodes wirelessly into any text field or register catalog records directly into the school database.",
                        color = ErrinTextMuted,
                        fontSize = 12.sp
                    )

                    // Incoming connection info for PC
                    Card(
                        colors = CardDefaults.cardColors(containerColor = ErrinSurfaceVariant),
                        shape = RoundedCornerShape(8.dp),
                        modifier = Modifier.fillMaxWidth()
                    ) {
                        Column(modifier = Modifier.padding(10.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(Icons.Default.Computer, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Scanner Server (Connect from PC)", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                            }
                            val listenUrl = org.errin.scanner.server.ScannerHttpServer.getListeningUrl()
                            Text(
                                "Phone Listener URL: $listenUrl",
                                color = ErrinTextPrimary,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.SemiBold
                            )
                            Text(
                                "On ErrinILS PC, go to Settings \u2192 Remote Access and click '+ Connect Scanner by IP' or 'Ping App' to connect directly.",
                                color = ErrinTextMuted,
                                fontSize = 11.sp
                            )
                        }
                    }

                    // Host & Port Row
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        OutlinedTextField(
                            value = localServerHost,
                            onValueChange = { localServerHost = it },
                            label = { Text("Server Host / IP", fontSize = 12.sp) },
                            placeholder = { Text("e.g. 192.168.1.50", color = ErrinTextMuted, fontSize = 12.sp) },
                            singleLine = true,
                            colors = outlinedColors(),
                            modifier = Modifier.weight(2.2f)
                        )

                        OutlinedTextField(
                            value = localServerPort,
                            onValueChange = { localServerPort = it },
                            label = { Text("Port", fontSize = 12.sp) },
                            placeholder = { Text("8585", color = ErrinTextMuted, fontSize = 12.sp) },
                            singleLine = true,
                            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                            colors = outlinedColors(),
                            modifier = Modifier.weight(1f)
                        )
                    }

                    OutlinedTextField(
                        value = deviceName,
                        onValueChange = { deviceName = it },
                        label = { Text("Device Name", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    // Pairing Code Box (for initial connection or re-pairing)
                    OutlinedTextField(
                        value = pairingCode,
                        onValueChange = { pairingCode = it },
                        label = { Text("6-Digit Pairing Code", fontSize = 12.sp) },
                        placeholder = { Text("Click '+ Pair new device' on PC", color = ErrinTextMuted, fontSize = 12.sp) },
                        singleLine = true,
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    // Action Buttons Row
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        // Pair with Code button
                        Button(
                            onClick = {
                                scope.launch {
                                    val host = localServerHost.trim()
                                    val port = localServerPort.trim().ifEmpty { "8585" }
                                    val code = pairingCode.trim()

                                    if (host.isEmpty()) {
                                        statusMessage = "❌ Please enter Server Host / IP."
                                        statusSuccess = false
                                        return@launch
                                    }
                                    if (code.isEmpty()) {
                                        statusMessage = "❌ Please enter the 6-digit Pairing Code displayed on PC."
                                        statusSuccess = false
                                        return@launch
                                    }

                                    isConnecting = true
                                    statusMessage = null

                                    prefs.serverHost = host
                                    prefs.serverPort = port
                                    prefs.deviceName = deviceName.trim().ifEmpty { "ErrinILS Scanner" }

                                    val url = "http://${host.removePrefix("http://").removePrefix("https://").trimEnd('/')}:$port"
                                    val res = api.pair(url, code, prefs.deviceName)
                                    isConnecting = false

                                    if (res.isSuccess && res.getOrNull()?.ok == true) {
                                        val token = res.getOrNull()?.token ?: ""
                                        prefs.serverToken = token
                                        prefs.serverUrl = url
                                        api.updateCredentials(url, token)
                                        statusSuccess = true
                                        statusMessage = "✅ Successfully paired with PC! (${res.getOrNull()?.name})"
                                        pairingCode = ""
                                        Toast.makeText(context, "Connected to PC!", Toast.LENGTH_SHORT).show()
                                    } else {
                                        statusSuccess = false
                                        statusMessage = "❌ Pairing failed: ${res.getOrNull()?.error ?: res.exceptionOrNull()?.message ?: "Check PC connection"}"
                                    }
                                }
                            },
                            modifier = Modifier.weight(1.2f),
                            enabled = !isConnecting && !isTesting,
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            if (isConnecting) {
                                CircularProgressIndicator(modifier = Modifier.size(16.dp), color = ErrinDarkBg, strokeWidth = 2.dp)
                            } else {
                                Icon(Icons.Default.Link, contentDescription = null, tint = ErrinDarkBg, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(4.dp))
                                Text("Pair with PC", color = ErrinDarkBg, fontWeight = FontWeight.Bold, fontSize = 12.sp)
                            }
                        }

                        // Test Connection Button
                        Button(
                            onClick = {
                                scope.launch {
                                    val host = localServerHost.trim()
                                    val port = localServerPort.trim().ifEmpty { "8585" }

                                    if (host.isEmpty()) {
                                        statusMessage = "❌ Please enter Server Host / IP."
                                        statusSuccess = false
                                        return@launch
                                    }

                                    isTesting = true
                                    statusMessage = null

                                    prefs.serverHost = host
                                    prefs.serverPort = port
                                    prefs.deviceName = deviceName.trim().ifEmpty { "ErrinILS Scanner" }
                                    val url = "http://${host.removePrefix("http://").removePrefix("https://").trimEnd('/')}:$port"
                                    api.updateCredentials(url, prefs.serverToken)

                                    val res = api.verifyConnection()
                                    isTesting = false

                                    statusSuccess = res.isSuccess && (res.getOrNull()?.ok == true)
                                    statusMessage = if (statusSuccess) {
                                        "✅ Connection Verified! Connected to ${res.getOrNull()?.name}"
                                    } else {
                                        val err = res.getOrNull()?.error ?: res.exceptionOrNull()?.message ?: "Connection error"
                                        if (prefs.serverToken.isEmpty()) {
                                            "❌ Not paired yet. Please enter the 6-digit Pairing Code above and click 'Pair with PC'."
                                        } else {
                                            "❌ Connection failed: $err"
                                        }
                                    }
                                }
                            },
                            modifier = Modifier.weight(1.2f),
                            enabled = !isConnecting && !isTesting && localServerHost.isNotBlank(),
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinSurfaceVariant),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            if (isTesting) {
                                CircularProgressIndicator(modifier = Modifier.size(16.dp), color = ErrinYellow, strokeWidth = 2.dp)
                            } else {
                                Icon(Icons.Default.Sync, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(4.dp))
                                Text("Test Connection", color = ErrinTextPrimary, fontSize = 12.sp)
                            }
                        }

                        // Save Address Button
                        IconButton(
                            onClick = {
                                prefs.serverHost = localServerHost.trim()
                                prefs.serverPort = localServerPort.trim().ifEmpty { "8585" }
                                prefs.deviceName = deviceName.trim().ifEmpty { "ErrinILS Scanner" }
                                api.updateCredentials(prefs.serverUrl, prefs.serverToken)
                                Toast.makeText(context, "Settings saved!", Toast.LENGTH_SHORT).show()
                            }
                        ) {
                            Icon(Icons.Default.Save, contentDescription = "Save", tint = ErrinYellow)
                        }
                    }

                    if (statusMessage != null) {
                        Text(
                            text = statusMessage ?: "",
                            color = if (statusSuccess) ErrinGreen else ErrinRed,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium
                        )
                    }

                    if (prefs.isConnectedToPc) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Text(
                                "Status: Paired (${prefs.serverHost}:${prefs.serverPort})",
                                color = ErrinGreen,
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Bold
                            )
                            TextButton(
                                onClick = {
                                    prefs.clearConnection()
                                    pairingCode = ""
                                    statusMessage = "PC connection removed."
                                    statusSuccess = false
                                }
                            ) {
                                Icon(Icons.Default.LinkOff, contentDescription = null, tint = ErrinRed, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(4.dp))
                                Text("Disconnect", color = ErrinRed, fontSize = 12.sp)
                            }
                        }
                    }
                }
            }

            // 2. Scan Feedback & Wedge Configuration
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.VolumeUp, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(20.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Scan Feedback & Wedge Mode", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text("Audible Beep on Scan", color = ErrinTextPrimary, fontSize = 13.sp)
                        Switch(
                            checked = soundEnabled,
                            onCheckedChange = {
                                soundEnabled = it
                                prefs.soundEnabled = it
                            },
                            colors = SwitchDefaults.colors(checkedThumbColor = ErrinYellow, checkedTrackColor = ErrinCard)
                        )
                    }

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text("Vibration on Scan (Haptic)", color = ErrinTextPrimary, fontSize = 13.sp)
                        Switch(
                            checked = vibrateEnabled,
                            onCheckedChange = {
                                vibrateEnabled = it
                                prefs.vibrateEnabled = it
                            },
                            colors = SwitchDefaults.colors(checkedThumbColor = ErrinYellow, checkedTrackColor = ErrinCard)
                        )
                    }

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Column(modifier = Modifier.weight(1f)) {
                            Text("Automatic 'Enter' Key", color = ErrinTextPrimary, fontSize = 13.sp)
                            Text("Send Enter keystroke after barcode is transmitted to PC.", color = ErrinTextMuted, fontSize = 11.sp)
                        }
                        Switch(
                            checked = autoEnter,
                            onCheckedChange = {
                                autoEnter = it
                                prefs.autoEnter = it
                            },
                            colors = SwitchDefaults.colors(checkedThumbColor = ErrinYellow, checkedTrackColor = ErrinCard)
                        )
                    }
                }
            }

            // 3. Optional GitHub Direct Catalog Storage
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurface),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Code, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(20.dp))
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Optional: GitHub Catalog (Offline Storage)", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    Text(
                        "If you wish to save books directly to your school's GitHub catalog repository when the desktop PC is inactive:",
                        color = ErrinTextMuted,
                        fontSize = 12.sp
                    )

                    OutlinedTextField(
                        value = githubRepo,
                        onValueChange = {
                            githubRepo = it
                            prefs.githubRepo = it.trim()
                        },
                        label = { Text("GitHub Repository (e.g. pustaka-terbuka/sk-taman-ria)", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    OutlinedTextField(
                        value = githubToken,
                        onValueChange = {
                            githubToken = it
                            prefs.githubToken = it.trim()
                        },
                        label = { Text("GitHub Personal Access Token (PAT)", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            }

            // 4. About Info
            Card(
                colors = CardDefaults.cardColors(containerColor = ErrinSurfaceVariant),
                shape = RoundedCornerShape(12.dp)
            ) {
                Column(modifier = Modifier.padding(14.dp)) {
                    Text("ErrinILS Scanner v1.4.2", color = ErrinTextPrimary, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                    Text("Wireless Barcode Gun & Rapid Book Cataloging", color = ErrinTextSecondary, fontSize = 11.sp)
                    Spacer(modifier = Modifier.height(4.dp))
                    Text("Designed for School Librarians & Library Helpers.", color = ErrinTextMuted, fontSize = 11.sp)
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
