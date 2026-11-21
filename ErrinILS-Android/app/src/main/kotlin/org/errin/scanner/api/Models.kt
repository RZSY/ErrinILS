package org.errin.scanner.api

import org.json.JSONArray
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

data class CirculationResult(
    val ok: Boolean,
    val message: String = "",
    val error: String? = null,
    val friendly: String? = null,
    val isOverdue: Boolean = false,
    val fineAmount: Double = 0.0
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
    val year: String = "",
    val onLoan: Boolean = false,
    val loanPatronName: String? = null,
    val loanDue: String? = null
) {
    companion object {
        fun fromJson(json: JSONObject): BookItem {
            return BookItem(
                id = json.optString("id", ""),
                barcode = json.optString("barcode", ""),
                title = json.optString("title", "Untitled"),
                author = json.optString("author", "Unknown"),
                isbn = json.optString("isbn", ""),
                call = json.optString("call", ""),
                subject = json.optString("subject", ""),
                publisher = json.optString("pub", json.optString("publisher", "")),
                year = json.optString("year", ""),
                onLoan = json.optBoolean("onLoan", false),
                loanPatronName = if (json.has("loanPatronName")) json.optString("loanPatronName") else null,
                loanDue = if (json.has("loanDue")) json.optString("loanDue") else null
            )
        }
    }
}

data class PatronSummary(
    val id: String = "",
    val name: String = "",
    val cls: String = "",
    val contact: String = "",
    val activeLoansCount: Int = 0,
    val unpaidFines: Double = 0.0,
    val isBlocked: Boolean = false
) {
    companion object {
        fun fromJson(json: JSONObject): PatronSummary {
            val activeLoans = json.optJSONArray("activeLoans") ?: JSONArray()
            return PatronSummary(
                id = json.optString("id", ""),
                name = json.optString("name", "Unknown Patron"),
                cls = json.optString("cls", ""),
                contact = json.optString("contact", ""),
                activeLoansCount = activeLoans.length(),
                unpaidFines = json.optDouble("finesUnpaid", 0.0),
                isBlocked = json.optBoolean("blocked", false)
            )
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
                    error = json.optString("error", "Lookup failed")
                )
            }
            val found = json.optBoolean("found", false)
            if (!found) {
                return IsbnLookupResult(
                    ok = true,
                    found = false,
                    error = json.optString("message", "No record found across union catalogs")
                )
            }
            val data = json.optJSONObject("data") ?: JSONObject()
            return IsbnLookupResult(
                ok = true,
                found = true,
                title = data.optString("title", ""),
                author = data.optString("author", ""),
                publisher = data.optString("publisher", ""),
                year = data.optString("year", ""),
                isbn = data.optString("isbn", ""),
                call = data.optString("call", ""),
                subject = data.optString("subject", ""),
                source = json.optString("source", "Union Catalog"),
                alreadyExists = json.optBoolean("alreadyExists", false)
            )
        }
    }
}

enum class ScannerMode(val title: String, val badge: String, val description: String) {
    RETURN("Return", "🔄", "Check in books to library"),
    CHECKOUT("Borrow", "📤", "Issue books to a student"),
    LOOKUP("ISBN Catalog", "🔍", "Scan ISBN to fetch & add books"),
    INSPECT("Info", "📦", "View book & loan details"),
    WEDGE("Barcode Gun", "⚡", "Live stream scans to desktop")
}

data class ScanHistoryEntry(
    val id: String = java.util.UUID.randomUUID().toString(),
    val timestamp: Long = System.currentTimeMillis(),
    val barcode: String,
    val mode: ScannerMode,
    val title: String? = null,
    val status: String,
    val isSuccess: Boolean
)
