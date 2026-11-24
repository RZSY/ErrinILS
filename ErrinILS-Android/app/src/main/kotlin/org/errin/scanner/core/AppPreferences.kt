package org.errin.scanner.core

import android.content.Context
import android.content.SharedPreferences

class AppPreferences(context: Context) {

    private val prefs: SharedPreferences = context.getSharedPreferences("errin_scanner_prefs", Context.MODE_PRIVATE)

    var serverHost: String
        get() {
            val host = prefs.getString("server_host", "") ?: ""
            if (host.isNotEmpty()) return host
            val raw = prefs.getString("server_url", "") ?: ""
            if (raw.isNotEmpty()) {
                val clean = raw.removePrefix("https://").removePrefix("http://").trimEnd('/')
                val parts = clean.split(":")
                return parts[0]
            }
            return ""
        }
        set(value) {
            val h = value.trim().removePrefix("https://").removePrefix("http://").trimEnd('/')
            prefs.edit().putString("server_host", h).apply()
            updateCombinedServerUrl(h, serverPort)
        }

    var serverPort: String
        get() {
            val p = prefs.getString("server_port", "") ?: ""
            if (p.isNotEmpty()) return p
            val raw = prefs.getString("server_url", "") ?: ""
            if (raw.isNotEmpty()) {
                val clean = raw.removePrefix("https://").removePrefix("http://").trimEnd('/')
                val parts = clean.split(":")
                if (parts.size > 1) {
                    val portPart = parts[1].split("/")[0]
                    if (portPart.isNotEmpty()) return portPart
                }
            }
            return "8585"
        }
        set(value) {
            val p = value.trim().ifEmpty { "8585" }
            prefs.edit().putString("server_port", p).apply()
            updateCombinedServerUrl(serverHost, p)
        }

    var serverUrl: String
        get() {
            val host = serverHost
            val port = serverPort
            if (host.isNotEmpty()) {
                val cleanHost = host.removePrefix("https://").removePrefix("http://").trimEnd('/')
                val p = if (port.isNotBlank()) port.trim() else "8585"
                return "http://$cleanHost:$p"
            }
            return prefs.getString("server_url", "") ?: ""
        }
        set(value) {
            val v = value.trim()
            prefs.edit().putString("server_url", v).apply()
            if (v.isNotEmpty()) {
                val clean = v.removePrefix("https://").removePrefix("http://").trimEnd('/')
                val parts = clean.split(":")
                val hostPart = parts[0]
                var portPart = "8585"
                if (parts.size > 1) {
                    portPart = parts[1].split("/")[0].ifEmpty { "8585" }
                }
                prefs.edit().putString("server_host", hostPart).putString("server_port", portPart).apply()
            }
        }

    private fun updateCombinedServerUrl(host: String, port: String) {
        if (host.isNotBlank()) {
            val cleanHost = host.removePrefix("https://").removePrefix("http://").trimEnd('/')
            val p = if (port.isNotBlank()) port.trim() else "8585"
            prefs.edit().putString("server_url", "http://$cleanHost:$p").apply()
        }
    }

    var serverToken: String
        get() = prefs.getString("server_token", "") ?: ""
        set(value) = prefs.edit().putString("server_token", value.trim()).apply()

    var deviceName: String
        get() = prefs.getString("device_name", "ErrinILS Scanner") ?: "ErrinILS Scanner"
        set(value) = prefs.edit().putString("device_name", value.trim()).apply()

    var soundEnabled: Boolean
        get() = prefs.getBoolean("sound_enabled", true)
        set(value) = prefs.edit().putBoolean("sound_enabled", value).apply()

    var vibrateEnabled: Boolean
        get() = prefs.getBoolean("vibrate_enabled", true)
        set(value) = prefs.edit().putBoolean("vibrate_enabled", value).apply()

    var autoEnter: Boolean
        get() = prefs.getBoolean("auto_enter", true)
        set(value) = prefs.edit().putBoolean("auto_enter", value).apply()

    var githubRepo: String
        get() = prefs.getString("github_repo", "") ?: ""
        set(value) = prefs.edit().putString("github_repo", value.trim()).apply()

    var githubToken: String
        get() = prefs.getString("github_token", "") ?: ""
        set(value) = prefs.edit().putString("github_token", value.trim()).apply()

    val isConnectedToPc: Boolean
        get() = serverUrl.isNotBlank() && serverToken.isNotBlank()

    fun clearConnection() {
        prefs.edit()
            .remove("server_url")
            .remove("server_host")
            .remove("server_port")
            .remove("server_token")
            .apply()
    }
}
