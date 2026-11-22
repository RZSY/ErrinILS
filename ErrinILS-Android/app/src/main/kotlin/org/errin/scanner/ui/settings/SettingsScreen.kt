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
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Code
import androidx.compose.material.icons.filled.Computer
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

    var localServerUrl by remember { mutableStateOf(prefs.serverUrl) }
    var deviceName by remember { mutableStateOf(prefs.deviceName) }
    var soundEnabled by remember { mutableStateOf(prefs.soundEnabled) }
    var vibrateEnabled by remember { mutableStateOf(prefs.vibrateEnabled) }
    var autoEnter by remember { mutableStateOf(prefs.autoEnter) }
    var githubRepo by remember { mutableStateOf(prefs.githubRepo) }
    var githubToken by remember { mutableStateOf(prefs.githubToken) }

    var isTesting by remember { mutableStateOf(false) }
    var testResult by remember { mutableStateOf<String?>(null) }
    var testSuccess by remember { mutableStateOf(false) }

    Scaffold(
        modifier = modifier.fillMaxSize(),
        containerColor = ErrinDarkBg,
        topBar = {
            TopAppBar(
                colors = TopAppBarDefaults.topAppBarColors(containerColor = ErrinSurface),
                title = { Text("Tetapan Pengimbas", color = ErrinTextPrimary, fontSize = 17.sp, fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Kembali", tint = ErrinYellow)
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
                        Text("Sambungan Komputer PC ErrinILS", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    Text(
                        "Sambungkan telefon ke perisian ErrinILS di PC untuk menaip kod bar secara langsung ke kotak teks atau mendaftarkan buku terus ke pengkalan data sekolah.",
                        color = ErrinTextMuted,
                        fontSize = 12.sp
                    )

                    OutlinedTextField(
                        value = localServerUrl,
                        onValueChange = { localServerUrl = it },
                        label = { Text("URL Komputer PC (cth. http://192.168.1.50:8080)", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    OutlinedTextField(
                        value = deviceName,
                        onValueChange = { deviceName = it },
                        label = { Text("Nama Peranti Telefon", fontSize = 12.sp) },
                        singleLine = true,
                        colors = outlinedColors(),
                        modifier = Modifier.fillMaxWidth()
                    )

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(
                            onClick = {
                                prefs.serverUrl = localServerUrl.trim()
                                prefs.deviceName = deviceName.trim()
                                api.updateCredentials(prefs.serverUrl, prefs.serverToken)
                                Toast.makeText(context, "Tetapan disimpan!", Toast.LENGTH_SHORT).show()
                            },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinYellow),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            Icon(Icons.Default.Save, contentDescription = null, tint = ErrinDarkBg, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("Simpan", color = ErrinDarkBg, fontWeight = FontWeight.Bold)
                        }

                        Button(
                            onClick = {
                                scope.launch {
                                    isTesting = true
                                    testResult = null
                                    prefs.serverUrl = localServerUrl.trim()
                                    prefs.deviceName = deviceName.trim()
                                    api.updateCredentials(prefs.serverUrl, prefs.serverToken)

                                    val res = api.verifyConnection()
                                    isTesting = false
                                    testSuccess = res.isSuccess && (res.getOrNull()?.ok == true)
                                    testResult = if (testSuccess) {
                                        "✅ Sambungan ke PC Berjaya! (${res.getOrNull()?.name})"
                                    } else {
                                        "❌ Gagal disambung: ${res.getOrNull()?.error ?: res.exceptionOrNull()?.message ?: "Ralat"}"
                                    }
                                }
                            },
                            modifier = Modifier.weight(1f),
                            enabled = !isTesting && localServerUrl.isNotBlank(),
                            colors = ButtonDefaults.buttonColors(containerColor = ErrinSurfaceVariant),
                            shape = RoundedCornerShape(8.dp)
                        ) {
                            if (isTesting) {
                                CircularProgressIndicator(modifier = Modifier.size(16.dp), color = ErrinYellow, strokeWidth = 2.dp)
                            } else {
                                Icon(Icons.Default.Sync, contentDescription = null, tint = ErrinYellow, modifier = Modifier.size(16.dp))
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Uji Sambungan", color = ErrinTextPrimary, fontSize = 12.sp)
                            }
                        }
                    }

                    if (testResult != null) {
                        Text(
                            text = testResult ?: "",
                            color = if (testSuccess) ErrinGreen else ErrinRed,
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium
                        )
                    }

                    if (prefs.isConnectedToPc) {
                        TextButton(
                            onClick = {
                                prefs.clearConnection()
                                localServerUrl = ""
                                testResult = "Sambungan PC telah dipadam."
                                testSuccess = false
                            }
                        ) {
                            Icon(Icons.Default.LinkOff, contentDescription = null, tint = ErrinRed, modifier = Modifier.size(16.dp))
                            Spacer(modifier = Modifier.width(6.dp))
                            Text("Putuskan Padanan dengan PC", color = ErrinRed, fontSize = 12.sp)
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
                        Text("Maklum Balas & Mod Imbasan", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text("Bunyi Beep Semasa Imbas", color = ErrinTextPrimary, fontSize = 13.sp)
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
                        Text("Getaran Semasa Imbas (Haptic)", color = ErrinTextPrimary, fontSize = 13.sp)
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
                            Text("Tekan 'Enter' Automatik", color = ErrinTextPrimary, fontSize = 13.sp)
                            Text("Hantar ketukan kekunci Enter selepas kod bar dihantar ke PC.", color = ErrinTextMuted, fontSize = 11.sp)
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
                        Text("Pilihan: GitHub Catalog (Luar Talian PC)", color = ErrinYellow, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                    }

                    Text(
                        "Jika anda ingin menyimpan buku terus ke repositori katalog GitHub sekolah semasa komputer PC tidak aktif:",
                        color = ErrinTextMuted,
                        fontSize = 12.sp
                    )

                    OutlinedTextField(
                        value = githubRepo,
                        onValueChange = {
                            githubRepo = it
                            prefs.githubRepo = it.trim()
                        },
                        label = { Text("Repositori GitHub (cth. pustaka-terbuka/sk-taman-ria)", fontSize = 12.sp) },
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
                    Text("ErrinILS Scanner v1.4.0", color = ErrinTextPrimary, fontWeight = FontWeight.Bold, fontSize = 13.sp)
                    Text("Pengimbas Kod Bar & Pendaftaran Buku Pantas", color = ErrinTextSecondary, fontSize = 11.sp)
                    Spacer(modifier = Modifier.height(4.dp))
                    Text("Direka untuk Guru Perpustakaan & Pengawas PSS.", color = ErrinTextMuted, fontSize = 11.sp)
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
