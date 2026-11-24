package org.errin.scanner.server

import android.content.Context
import android.os.Handler
import android.os.Looper
import android.widget.Toast
import org.errin.scanner.core.AppPreferences
import org.errin.scanner.core.FeedbackManager
import org.json.JSONObject
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.OutputStream
import java.net.Inet4Address
import java.net.NetworkInterface
import java.net.ServerSocket
import java.net.Socket
import java.util.Collections
import java.util.concurrent.Executors

object ScannerHttpServer {

    const val DEFAULT_PORT = 8586
    private const val BUFFER_SIZE = 4096

    @Volatile
    private var isRunning = false

    private var serverSocket: ServerSocket? = null
    private var acceptThread: Thread? = null
    private val threadPool = Executors.newCachedThreadPool()
    private val mainHandler = Handler(Looper.getMainLooper())

    private var appContext: Context? = null
    private var feedbackManager: FeedbackManager? = null
    private var appPreferences: AppPreferences? = null

    var currentPort: Int = DEFAULT_PORT
        private set

    fun start(context: Context, port: Int = DEFAULT_PORT) {
        if (isRunning && serverSocket != null && !serverSocket!!.isClosed && currentPort == port) {
            return
        }

        stop()

        val appCtx = context.applicationContext
        appContext = appCtx
        feedbackManager = FeedbackManager(appCtx)
        appPreferences = AppPreferences(appCtx)
        currentPort = port

        try {
            val ss = ServerSocket(port)
            ss.reuseAddress = true
            serverSocket = ss
            isRunning = true

            acceptThread = Thread({
                while (isRunning && !ss.isClosed) {
                    try {
                        val client = ss.accept()
                        threadPool.execute {
                            handleClientSocket(client)
                        }
                    } catch (_: Exception) {
                        // socket closed or error
                    }
                }
            }, "ErrinScannerHttpServer").apply {
                isDaemon = true
                start()
            }
        } catch (_: Exception) {
            isRunning = false
        }
    }

    fun stop() {
        isRunning = false
        try {
            serverSocket?.close()
        } catch (_: Exception) {}
        serverSocket = null

        try {
            acceptThread?.interrupt()
        } catch (_: Exception) {}
        acceptThread = null
    }

    fun isServerRunning(): Boolean = isRunning && serverSocket?.isClosed == false

    fun getLocalIpAddress(): String {
        try {
            val interfaces = Collections.list(NetworkInterface.getNetworkInterfaces())
            for (intf in interfaces) {
                if (!intf.isUp || intf.isLoopback) continue
                val addrs = Collections.list(intf.inetAddresses)
                for (addr in addrs) {
                    if (!addr.isLoopbackAddress && addr is Inet4Address) {
                        val sAddr = addr.hostAddress ?: continue
                        if (sAddr.indexOf(':') < 0 && sAddr != "127.0.0.1") {
                            return sAddr
                        }
                    }
                }
            }
        } catch (_: Exception) {}
        return "127.0.0.1"
    }

    fun getListeningUrl(): String = "http://${getLocalIpAddress()}:$currentPort"

    private fun handleClientSocket(socket: Socket) {
        socket.use { s ->
            s.soTimeout = 8000
            val input = s.getInputStream()
            val output = s.getOutputStream()
            val reader = BufferedReader(InputStreamReader(input, Charsets.UTF_8))

            val requestLine = reader.readLine() ?: return
            val reqTokens = requestLine.split(" ")
            if (reqTokens.size < 2) return

            val method = reqTokens[0].uppercase()
            val rawPath = reqTokens[1]

            val headers = mutableMapOf<String, String>()
            while (true) {
                val headerLine = reader.readLine()
                if (headerLine.isNullOrEmpty()) break
                val colon = headerLine.indexOf(':')
                if (colon > 0) {
                    val k = headerLine.substring(0, colon).trim().lowercase()
                    val v = headerLine.substring(colon + 1).trim()
                    headers[k] = v
                }
            }

            val path = rawPath.substringBefore('?')

            if (method == "OPTIONS") {
                sendCors(output)
                return
            }

            var body = ""
            val contentLength = headers["content-length"]?.toIntOrNull() ?: 0
            if (contentLength > 0 && contentLength < 1048576) {
                val chars = CharArray(contentLength)
                var totalRead = 0
                while (totalRead < contentLength) {
                    val read = reader.read(chars, totalRead, contentLength - totalRead)
                    if (read <= 0) break
                    totalRead += read
                }
                body = String(chars, 0, totalRead)
            }

            dispatchRoute(method, path, body, output)
        }
    }

    private fun dispatchRoute(method: String, path: String, body: String, out: OutputStream) {
        val prefs = appPreferences
        val fb = feedbackManager
        val ctx = appContext

        when {
            // GET / or /ping
            (method == "GET" && (path == "/" || path == "/ping")) -> {
                val devName = prefs?.deviceName?.ifEmpty { "ErrinILS Scanner" } ?: "ErrinILS Scanner"
                val res = JSONObject().apply {
                    put("ok", true)
                    put("app", "ErrinILS Scanner")
                    put("version", "1.4.3")
                    put("name", devName)
                    put("ip", getLocalIpAddress())
                    put("port", currentPort)
                    put("paired", prefs?.isConnectedToPc ?: false)
                    put("serverUrl", prefs?.serverUrl ?: "")
                    put("status", "ready")
                }
                sendJson(out, 200, res)
            }

            // GET /status
            (method == "GET" && path == "/status") -> {
                val devName = prefs?.deviceName?.ifEmpty { "ErrinILS Scanner" } ?: "ErrinILS Scanner"
                val res = JSONObject().apply {
                    put("ok", true)
                    put("app", "ErrinILS Scanner")
                    put("version", "1.4.3")
                    put("name", devName)
                    put("ip", getLocalIpAddress())
                    put("port", currentPort)
                    put("paired", prefs?.isConnectedToPc ?: false)
                    put("serverUrl", prefs?.serverUrl ?: "")
                    put("sound", prefs?.soundEnabled ?: true)
                    put("vibrate", prefs?.vibrateEnabled ?: true)
                    put("status", "ready")
                }
                sendJson(out, 200, res)
            }

            // POST /beep or /test
            (method == "POST" && (path == "/beep" || path == "/test")) -> {
                fb?.playSuccess(
                    soundEnabled = prefs?.soundEnabled ?: true,
                    vibrateEnabled = prefs?.vibrateEnabled ?: true
                )
                mainHandler.post {
                    ctx?.let {
                        Toast.makeText(it, "🔔 Connection ping from ErrinILS PC!", Toast.LENGTH_SHORT).show()
                    }
                }
                val res = JSONObject().apply {
                    put("ok", true)
                    put("message", "Beep & vibrate triggered")
                }
                sendJson(out, 200, res)
            }

            // POST /alert
            (method == "POST" && path == "/alert") -> {
                val json = try { JSONObject(body) } catch (_: Exception) { JSONObject() }
                val msg = json.optString("message", "Alert from ErrinILS PC")
                val title = json.optString("title", "ErrinILS Alert")

                fb?.playAlert(
                    soundEnabled = prefs?.soundEnabled ?: true,
                    vibrateEnabled = prefs?.vibrateEnabled ?: true
                )
                mainHandler.post {
                    ctx?.let {
                        Toast.makeText(it, "⚠️ $title: $msg", Toast.LENGTH_LONG).show()
                    }
                }
                val res = JSONObject().apply {
                    put("ok", true)
                    put("received", msg)
                }
                sendJson(out, 200, res)
            }

            // POST /pair (Direct push pairing from PC)
            (method == "POST" && path == "/pair") -> {
                val json = try { JSONObject(body) } catch (_: Exception) { JSONObject() }
                val serverUrl = json.optString("serverUrl", "").trim()
                val serverHost = json.optString("serverHost", "").trim()
                val serverPort = json.optString("serverPort", "8585").trim()
                val token = json.optString("token", "").trim()
                val pcName = json.optString("serverName", "ErrinILS PC").trim()

                if (token.isEmpty() || (serverUrl.isEmpty() && serverHost.isEmpty())) {
                    val errRes = JSONObject().apply {
                        put("ok", false)
                        put("error", "Missing required pairing parameters: token and serverUrl or serverHost")
                    }
                    sendJson(out, 400, errRes)
                    return
                }

                if (prefs != null) {
                    if (serverHost.isNotEmpty()) {
                        prefs.serverHost = serverHost
                        prefs.serverPort = serverPort.ifEmpty { "8585" }
                    }
                    if (serverUrl.isNotEmpty()) {
                        prefs.serverUrl = serverUrl
                    }
                    prefs.serverToken = token

                    fb?.playSuccess(
                        soundEnabled = prefs.soundEnabled,
                        vibrateEnabled = prefs.vibrateEnabled
                    )

                    mainHandler.post {
                        ctx?.let {
                            Toast.makeText(it, "✅ Connected to $pcName!", Toast.LENGTH_SHORT).show()
                        }
                    }
                }

                val res = JSONObject().apply {
                    put("ok", true)
                    put("name", prefs?.deviceName ?: "ErrinILS Scanner")
                    put("paired", true)
                    put("message", "Successfully paired with $pcName")
                }
                sendJson(out, 200, res)
            }

            else -> {
                val notFound = JSONObject().apply {
                    put("ok", false)
                    put("error", "Endpoint not found: $path")
                }
                sendJson(out, 404, notFound)
            }
        }
    }

    private fun sendJson(out: OutputStream, statusCode: Int, json: JSONObject) {
        val bytes = json.toString().toByteArray(Charsets.UTF_8)
        val statusText = when (statusCode) {
            200 -> "200 OK"
            400 -> "400 Bad Request"
            404 -> "404 Not Found"
            500 -> "500 Internal Server Error"
            else -> "$statusCode Response"
        }
        val header = "HTTP/1.1 $statusText\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type, Authorization, X-Errin-Token, X-Scanner-Port\r\n" +
                "Content-Length: ${bytes.size}\r\n" +
                "Connection: close\r\n\r\n"

        out.write(header.toByteArray(Charsets.UTF_8))
        out.write(bytes)
        out.flush()
    }

    private fun sendCors(out: OutputStream) {
        val header = "HTTP/1.1 204 No Content\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type, Authorization, X-Errin-Token, X-Scanner-Port\r\n" +
                "Connection: close\r\n\r\n"
        out.write(header.toByteArray(Charsets.UTF_8))
        out.flush()
    }
}
