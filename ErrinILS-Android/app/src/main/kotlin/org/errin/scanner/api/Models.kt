package org.errin.scanner.api

import org.json.JSONObject

data class PairResult(
    val ok: Boolean,
    val token: String = "",
    val name: String = "",
    val error: String? = null
)

data class MeResult(
    val ok: Boolean,
    val name: String = "",
    val paired: String = "",
    val error: String? = null
)

data class BookItem(
    val id: String = "",
    val barcode: String = "",
    val title: String = "",
    val author: String = "",
    val isbn: String = "",
    val call: String = "",
    val subject: String = "",
    val publisher: String = "",
    val year: String = ""
) {
    companion object {
        fun fromJson(json: JSONObject): BookItem {
            return BookItem(
                id = json.optString("id", ""),
                barcode = json.optString("barcode", ""),
                title = json.optString("title", ""),
                author = json.optString("author", ""),
                isbn = json.optString("isbn", ""),
                call = json.optString("call", ""),
                subject = json.optString("subject", ""),
                publisher = json.optString("pub", json.optString("publisher", "")),
                year = json.optString("year", "")
            )
        }
    }

    fun toJson(): JSONObject {
        return JSONObject().apply {
            if (id.isNotBlank()) put("id", id)
            put("barcode", barcode.trim())
            put("title", title.trim())
            put("author", author.trim())
            put("isbn", isbn.trim())
            put("pub", publisher.trim())
            put("year", year.trim())
            put("call", call.trim())
            put("subject", subject.trim())
        }
    }
}

data class IsbnLookupResult(
    val ok: Boolean,
    val found: Boolean = false,
    val title: String = "",
    val author: String = "",
    val publisher: String = "",
    val year: String = "",
    val isbn: String = "",
    val call: String = "",
    val subject: String = "",
    val source: String = "",
    val alreadyExists: Boolean = false,
    val error: String? = null
) {
    companion object {
        fun fromJson(json: JSONObject): IsbnLookupResult {
            val ok = json.optBoolean("ok", false)
            if (!ok) {
                return IsbnLookupResult(
                    ok = false,
                    error = json.optString("error", "Lookup search failed")
                )
            }
            val found = json.optBoolean("found", false)
            if (!found) {
                return IsbnLookupResult(
                    ok = true,
                    found = false,
                    error = json.optString("message", "No bibliographic record found for this ISBN")
                )
            }
            val data = json.optJSONObject("data") ?: JSONObject()
            return IsbnLookupResult(
                ok = true,
                found = true,
                title = data.optString("title", ""),
                author = data.optString("author", ""),
                publisher = data.optString("publisher", data.optString("pub", "")),
                year = data.optString("year", ""),
                isbn = data.optString("isbn", ""),
                call = data.optString("call", ""),
                subject = data.optString("subject", ""),
                source = json.optString("source", "Library Catalog"),
                alreadyExists = json.optBoolean("alreadyExists", false)
            )
        }
    }
}

enum class AppTab(val title: String, val badge: String, val description: String) {
    SCANNER("Barcode Scanner", "⚡", "Stream barcodes directly to PC like a wireless scanner gun"),
    ADD_CATALOG("Add Book (ISBN)", "📚", "Scan ISBN barcode & create new catalog records")
}

data class ScanHistoryEntry(
    val id: String = java.util.UUID.randomUUID().toString(),
    val timestamp: Long = System.currentTimeMillis(),
    val barcode: String,
    val status: String,
    val isSuccess: Boolean
)
