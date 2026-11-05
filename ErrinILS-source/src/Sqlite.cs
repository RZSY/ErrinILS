// Zero-dependency SQLite interface and persistence store for ErrinILS.
// Uses native SQLite dynamic loading (Windows built-in winsqlite3.dll or sqlite3.dll, Linux/macOS libsqlite3).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Errin
{
    public class SqliteException : Exception
    {
        public int ErrorCode { get; private set; }
        public SqliteException(int code, string msg)
            : base(string.Format("SQLite error {0}: {1}", code, msg))
        {
            ErrorCode = code;
        }
    }

    public static class SqliteNative
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("libdl", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dlopen")]
        static extern IntPtr dlopen(string filename, int flags);
        [DllImport("libdl", CallingConvention = CallingConvention.Cdecl, EntryPoint = "dlsym")]
        static extern IntPtr dlsym(IntPtr handle, string symbol);

        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;
        public const int SQLITE_NULL = 5;

        public const int SQLITE_OPEN_READWRITE = 0x00000002;
        public const int SQLITE_OPEN_CREATE = 0x00000004;
        public const int SQLITE_OPEN_FULLMUTEX = 0x00010000;

        public static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int OpenV2Del(byte[] filename, out IntPtr db, int flags, IntPtr zVfs);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int CloseDel(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ExecDel(IntPtr db, byte[] sql, IntPtr cb, IntPtr arg, out IntPtr err);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int PrepareV2Del(IntPtr db, byte[] sql, int len, out IntPtr stmt, out IntPtr tail);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int StepDel(IntPtr stmt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ResetDel(IntPtr stmt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ClearBindingsDel(IntPtr stmt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int FinalizeDel(IntPtr stmt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BindTextDel(IntPtr stmt, int col, byte[] val, int len, IntPtr dtor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BindIntDel(IntPtr stmt, int col, int val);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BindInt64Del(IntPtr stmt, int col, long val);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BindDoubleDel(IntPtr stmt, int col, double val);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BindNullDel(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ColumnTextDel(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ColumnBytesDel(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ColumnIntDel(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate long ColumnInt64Del(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate double ColumnDoubleDel(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ColumnTypeDel(IntPtr stmt, int col);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr ErrmsgDel(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ChangesDel(IntPtr db);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr LibversionDel();

        public static OpenV2Del sqlite3_open_v2;
        public static CloseDel sqlite3_close;
        public static ExecDel sqlite3_exec;
        public static PrepareV2Del sqlite3_prepare_v2;
        public static StepDel sqlite3_step;
        public static ResetDel sqlite3_reset;
        public static ClearBindingsDel sqlite3_clear_bindings;
        public static FinalizeDel sqlite3_finalize;
        public static BindTextDel sqlite3_bind_text;
        public static BindIntDel sqlite3_bind_int;
        public static BindInt64Del sqlite3_bind_int64;
        public static BindDoubleDel sqlite3_bind_double;
        public static BindNullDel sqlite3_bind_null;
        public static ColumnTextDel sqlite3_column_text;
        public static ColumnBytesDel sqlite3_column_bytes;
        public static ColumnIntDel sqlite3_column_int;
        public static ColumnInt64Del sqlite3_column_int64;
        public static ColumnDoubleDel sqlite3_column_double;
        public static ColumnTypeDel sqlite3_column_type;
        public static ErrmsgDel sqlite3_errmsg;
        public static ChangesDel sqlite3_changes;
        public static LibversionDel sqlite3_libversion;

        static bool initialized = false;
        static readonly object initLock = new object();

        public static bool IsAvailable
        {
            get
            {
                EnsureInit();
                return sqlite3_open_v2 != null;
            }
        }

        public static void EnsureInit()
        {
            if (initialized) return;
            lock (initLock)
            {
                if (initialized) return;
                IntPtr hModule = IntPtr.Zero;
                bool isWindows = Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX;

                if (isWindows)
                {
                    // 1. Windows built-in winsqlite3.dll (Windows 10, 11, Windows Server 2016+)
                    hModule = LoadLibrary("winsqlite3.dll");
                    // 2. Application folder sqlite3.dll
                    if (hModule == IntPtr.Zero)
                    {
                        string appDir = AppDomain.CurrentDomain.BaseDirectory;
                        if (!string.IsNullOrEmpty(appDir))
                        {
                            string appDll = Path.Combine(appDir, "sqlite3.dll");
                            if (File.Exists(appDll)) hModule = LoadLibrary(appDll);
                        }
                    }
                    // 3. System PATH sqlite3.dll
                    if (hModule == IntPtr.Zero) hModule = LoadLibrary("sqlite3.dll");
                }
                else
                {
                    // Linux / macOS
                    string[] libs = { "libsqlite3.so.0", "libsqlite3.so", "libsqlite3.dylib" };
                    foreach (var lib in libs)
                    {
                        try
                        {
                            hModule = dlopen(lib, 2 /* RTLD_NOW */);
                            if (hModule != IntPtr.Zero) break;
                        }
                        catch { }
                    }
                }

                if (hModule != IntPtr.Zero)
                {
                    Func<string, IntPtr> getProc = delegate(string name)
                    {
                        if (isWindows) return GetProcAddress(hModule, name);
                        try { return dlsym(hModule, name); } catch { return IntPtr.Zero; }
                    };

                    sqlite3_open_v2 = GetDel<OpenV2Del>(getProc("sqlite3_open_v2"));
                    sqlite3_close = GetDel<CloseDel>(getProc("sqlite3_close"));
                    sqlite3_exec = GetDel<ExecDel>(getProc("sqlite3_exec"));
                    sqlite3_prepare_v2 = GetDel<PrepareV2Del>(getProc("sqlite3_prepare_v2"));
                    sqlite3_step = GetDel<StepDel>(getProc("sqlite3_step"));
                    sqlite3_reset = GetDel<ResetDel>(getProc("sqlite3_reset"));
                    sqlite3_clear_bindings = GetDel<ClearBindingsDel>(getProc("sqlite3_clear_bindings"));
                    sqlite3_finalize = GetDel<FinalizeDel>(getProc("sqlite3_finalize"));
                    sqlite3_bind_text = GetDel<BindTextDel>(getProc("sqlite3_bind_text"));
                    sqlite3_bind_int = GetDel<BindIntDel>(getProc("sqlite3_bind_int"));
                    sqlite3_bind_int64 = GetDel<BindInt64Del>(getProc("sqlite3_bind_int64"));
                    sqlite3_bind_double = GetDel<BindDoubleDel>(getProc("sqlite3_bind_double"));
                    sqlite3_bind_null = GetDel<BindNullDel>(getProc("sqlite3_bind_null"));
                    sqlite3_column_text = GetDel<ColumnTextDel>(getProc("sqlite3_column_text"));
                    sqlite3_column_bytes = GetDel<ColumnBytesDel>(getProc("sqlite3_column_bytes"));
                    sqlite3_column_int = GetDel<ColumnIntDel>(getProc("sqlite3_column_int"));
                    sqlite3_column_int64 = GetDel<ColumnInt64Del>(getProc("sqlite3_column_int64"));
                    sqlite3_column_double = GetDel<ColumnDoubleDel>(getProc("sqlite3_column_double"));
                    sqlite3_column_type = GetDel<ColumnTypeDel>(getProc("sqlite3_column_type"));
                    sqlite3_errmsg = GetDel<ErrmsgDel>(getProc("sqlite3_errmsg"));
                    sqlite3_changes = GetDel<ChangesDel>(getProc("sqlite3_changes"));
                    sqlite3_libversion = GetDel<LibversionDel>(getProc("sqlite3_libversion"));
                }

                initialized = true;
            }
        }

        static T GetDel<T>(IntPtr ptr) where T : class
        {
            if (ptr == IntPtr.Zero) return null;
            return Marshal.GetDelegateForFunctionPointer(ptr, typeof(T)) as T;
        }

        public static byte[] Utf8Bytes(string s)
        {
            if (s == null) s = "";
            return Encoding.UTF8.GetBytes(s + "\0");
        }

        public static string Utf8String(IntPtr ptr, int len)
        {
            if (ptr == IntPtr.Zero || len <= 0) return "";
            byte[] buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }
    }

    public class SqliteDb : IDisposable
    {
        IntPtr db = IntPtr.Zero;
        public IntPtr Handle { get { return db; } }

        public SqliteDb(string path)
        {
            if (!SqliteNative.IsAvailable)
                throw new InvalidOperationException("SQLite native library (winsqlite3.dll or sqlite3.dll) not found.");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            int flags = SqliteNative.SQLITE_OPEN_READWRITE | SqliteNative.SQLITE_OPEN_CREATE | SqliteNative.SQLITE_OPEN_FULLMUTEX;
            int rc = SqliteNative.sqlite3_open_v2(SqliteNative.Utf8Bytes(path), out db, flags, IntPtr.Zero);
            if (rc != SqliteNative.SQLITE_OK)
            {
                string msg = LastError();
                Close();
                throw new SqliteException(rc, msg);
            }
        }

        public string LastError()
        {
            if (db == IntPtr.Zero || SqliteNative.sqlite3_errmsg == null) return "Unknown error";
            IntPtr ptr = SqliteNative.sqlite3_errmsg(db);
            if (ptr == IntPtr.Zero) return "Unknown error";
            return Marshal.PtrToStringAnsi(ptr);
        }

        public void Execute(string sql)
        {
            if (db == IntPtr.Zero) throw new ObjectDisposedException("SqliteDb");
            IntPtr err;
            int rc = SqliteNative.sqlite3_exec(db, SqliteNative.Utf8Bytes(sql), IntPtr.Zero, IntPtr.Zero, out err);
            if (rc != SqliteNative.SQLITE_OK)
            {
                string msg = LastError();
                throw new SqliteException(rc, msg);
            }
        }

        public SqliteCommand CreateCommand(string sql)
        {
            if (db == IntPtr.Zero) throw new ObjectDisposedException("SqliteDb");
            return new SqliteCommand(this, sql);
        }

        public void BeginTransaction()
        {
            Execute("BEGIN IMMEDIATE;");
        }

        public void Commit()
        {
            Execute("COMMIT;");
        }

        public void Rollback()
        {
            try { Execute("ROLLBACK;"); } catch { }
        }

        public void Close()
        {
            if (db != IntPtr.Zero)
            {
                if (SqliteNative.sqlite3_close != null)
                    SqliteNative.sqlite3_close(db);
                db = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        ~SqliteDb()
        {
            Close();
        }
    }

    public class SqliteCommand : IDisposable
    {
        readonly SqliteDb db;
        IntPtr stmt = IntPtr.Zero;

        public SqliteCommand(SqliteDb db, string sql)
        {
            this.db = db;
            IntPtr tail;
            byte[] sqlBytes = SqliteNative.Utf8Bytes(sql);
            int rc = SqliteNative.sqlite3_prepare_v2(db.Handle, sqlBytes, -1, out stmt, out tail);
            if (rc != SqliteNative.SQLITE_OK)
                throw new SqliteException(rc, db.LastError());
        }

        public void Bind(int index, string val)
        {
            if (val == null)
            {
                SqliteNative.sqlite3_bind_null(stmt, index);
            }
            else
            {
                byte[] bytes = Encoding.UTF8.GetBytes(val);
                SqliteNative.sqlite3_bind_text(stmt, index, bytes, bytes.Length, SqliteNative.SQLITE_TRANSIENT);
            }
        }

        public void Bind(int index, int val)
        {
            SqliteNative.sqlite3_bind_int(stmt, index, val);
        }

        public void Bind(int index, long val)
        {
            SqliteNative.sqlite3_bind_int64(stmt, index, val);
        }

        public void Bind(int index, double val)
        {
            SqliteNative.sqlite3_bind_double(stmt, index, val);
        }

        public void BindNull(int index)
        {
            SqliteNative.sqlite3_bind_null(stmt, index);
        }

        public bool Step()
        {
            int rc = SqliteNative.sqlite3_step(stmt);
            if (rc == SqliteNative.SQLITE_ROW) return true;
            if (rc == SqliteNative.SQLITE_DONE) return false;
            throw new SqliteException(rc, db.LastError());
        }

        public void ExecuteNonQuery()
        {
            Step();
            Reset();
        }

        public void Reset()
        {
            if (stmt != IntPtr.Zero)
            {
                SqliteNative.sqlite3_reset(stmt);
                SqliteNative.sqlite3_clear_bindings(stmt);
            }
        }

        public string GetText(int col)
        {
            if (IsNull(col)) return "";
            IntPtr ptr = SqliteNative.sqlite3_column_text(stmt, col);
            int len = SqliteNative.sqlite3_column_bytes(stmt, col);
            return SqliteNative.Utf8String(ptr, len);
        }

        public string GetTextOrNull(int col)
        {
            if (IsNull(col)) return null;
            IntPtr ptr = SqliteNative.sqlite3_column_text(stmt, col);
            int len = SqliteNative.sqlite3_column_bytes(stmt, col);
            string s = SqliteNative.Utf8String(ptr, len);
            return s == "" ? null : s;
        }

        public int GetInt(int col)
        {
            return SqliteNative.sqlite3_column_int(stmt, col);
        }

        public long GetInt64(int col)
        {
            return SqliteNative.sqlite3_column_int64(stmt, col);
        }

        public double GetDouble(int col)
        {
            return SqliteNative.sqlite3_column_double(stmt, col);
        }

        public bool IsNull(int col)
        {
            return SqliteNative.sqlite3_column_type(stmt, col) == SqliteNative.SQLITE_NULL;
        }

        public void Dispose()
        {
            if (stmt != IntPtr.Zero)
            {
                if (SqliteNative.sqlite3_finalize != null)
                    SqliteNative.sqlite3_finalize(stmt);
                stmt = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~SqliteCommand()
        {
            Dispose();
        }
    }

    public static class SqliteStore
    {
        public static void InitSchema(SqliteDb db)
        {
            db.Execute("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");

            db.Execute(@"
                CREATE TABLE IF NOT EXISTS items (
                    id TEXT PRIMARY KEY,
                    ctl TEXT,
                    acq TEXT,
                    barcode TEXT,
                    title TEXT,
                    sub TEXT,
                    author TEXT,
                    isbn TEXT,
                    publisher TEXT,
                    place TEXT,
                    year TEXT,
                    pages TEXT,
                    subject TEXT,
                    call_num TEXT,
                    weeded TEXT,
                    weed_reason TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_items_barcode ON items(barcode);
                CREATE INDEX IF NOT EXISTS idx_items_isbn ON items(isbn);
                CREATE INDEX IF NOT EXISTS idx_items_ctl ON items(ctl);
                CREATE INDEX IF NOT EXISTS idx_items_title ON items(title);

                CREATE TABLE IF NOT EXISTS patrons (
                    id TEXT PRIMARY KEY,
                    name TEXT,
                    cls TEXT,
                    contact TEXT,
                    reg TEXT,
                    last TEXT,
                    id_murid TEXT,
                    no_pengenalan TEXT,
                    dob TEXT,
                    status_pengajian TEXT,
                    tarikh_masuk_sekolah TEXT,
                    tarikh_masuk_kelas TEXT,
                    tahun_tingkatan TEXT,
                    nama_kelas TEXT,
                    nama_guru_kelas TEXT,
                    jantina TEXT,
                    kaum TEXT,
                    agama TEXT,
                    warganegara TEXT,
                    status_oku TEXT,
                    status_yatim TEXT,
                    nama_penjaga1 TEXT,
                    tel_penjaga1 TEXT,
                    nama_penjaga2 TEXT,
                    tel_penjaga2 TEXT,
                    alamat1 TEXT,
                    alamat2 TEXT,
                    alamat3 TEXT,
                    poskod TEXT,
                    bandar TEXT,
                    daerah TEXT,
                    negeri TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_patrons_name ON patrons(name);
                CREATE INDEX IF NOT EXISTS idx_patrons_cls ON patrons(cls);

                CREATE TABLE IF NOT EXISTS loans (
                    id TEXT PRIMARY KEY,
                    iid TEXT,
                    pid TEXT,
                    out_date TEXT,
                    due_date TEXT,
                    ret_date TEXT,
                    ren INTEGER
                );
                CREATE INDEX IF NOT EXISTS idx_loans_iid ON loans(iid);
                CREATE INDEX IF NOT EXISTS idx_loans_pid ON loans(pid);
                CREATE INDEX IF NOT EXISTS idx_loans_ret ON loans(ret_date);

                CREATE TABLE IF NOT EXISTS holds (
                    id TEXT PRIMARY KEY,
                    iid TEXT,
                    pid TEXT,
                    date TEXT,
                    st TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_holds_iid ON holds(iid);
                CREATE INDEX IF NOT EXISTS idx_holds_pid ON holds(pid);

                CREATE TABLE IF NOT EXISTS fines (
                    id TEXT PRIMARY KEY,
                    pid TEXT,
                    iid TEXT,
                    loan TEXT,
                    days INTEGER,
                    amt REAL,
                    date TEXT,
                    paid TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_fines_pid ON fines(pid);
                CREATE INDEX IF NOT EXISTS idx_fines_paid ON fines(paid);

                CREATE TABLE IF NOT EXISTS settings (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    school TEXT,
                    rate REAL,
                    max_loans INTEGER,
                    block REAL,
                    zt_json TEXT,
                    patron_req_json TEXT,
                    opac_login INTEGER DEFAULT 0
                );

                CREATE TABLE IF NOT EXISTS class_history (
                    year INTEGER,
                    cls TEXT,
                    student_count INTEGER,
                    total_loans INTEGER,
                    students_json TEXT,
                    PRIMARY KEY (year, cls)
                );

                CREATE TABLE IF NOT EXISTS archived_patrons (
                    id TEXT PRIMARY KEY,
                    name TEXT,
                    cls TEXT,
                    contact TEXT,
                    reg TEXT,
                    last TEXT,
                    archived_date TEXT,
                    loans_json TEXT,
                    fines_json TEXT,
                    extra_json TEXT
                );
            ");

            string[] patronCols = new[] {
                "id_murid", "no_pengenalan", "dob", "status_pengajian",
                "tarikh_masuk_sekolah", "tarikh_masuk_kelas", "tahun_tingkatan",
                "nama_kelas", "nama_guru_kelas", "jantina", "kaum", "agama",
                "warganegara", "status_oku", "status_yatim", "nama_penjaga1",
                "tel_penjaga1", "nama_penjaga2", "tel_penjaga2", "alamat1", "alamat2", "alamat3",
                "poskod", "bandar", "daerah", "negeri"
            };
            foreach (var col in patronCols)
            {
                try { db.Execute("ALTER TABLE patrons ADD COLUMN " + col + " TEXT;"); } catch { }
            }
            try { db.Execute("ALTER TABLE settings ADD COLUMN patron_req_json TEXT;"); } catch { }
            try { db.Execute("ALTER TABLE settings ADD COLUMN opac_login INTEGER DEFAULT 0;"); } catch { }
            try { db.Execute("ALTER TABLE archived_patrons ADD COLUMN extra_json TEXT;"); } catch { }
        }

        public static Db Load(string dbPath)
        {
            if (!File.Exists(dbPath)) return null;

            var db = new Db();
            using (var sdb = new SqliteDb(dbPath))
            {
                InitSchema(sdb);

                // Load items
                using (var cmd = sdb.CreateCommand("SELECT id, ctl, acq, barcode, title, sub, author, isbn, publisher, place, year, pages, subject, call_num, weeded, weed_reason FROM items;"))
                {
                    while (cmd.Step())
                    {
                        var it = new Item();
                        it.id = cmd.GetText(0);
                        it.ctl = cmd.GetText(1);
                        it.acq = cmd.GetText(2);
                        it.barcode = cmd.GetText(3);
                        it.title = cmd.GetText(4);
                        it.sub = cmd.GetText(5);
                        it.author = cmd.GetText(6);
                        it.isbn = cmd.GetText(7);
                        it.publisher = cmd.GetText(8);
                        it.place = cmd.GetText(9);
                        it.year = cmd.GetText(10);
                        it.pages = cmd.GetText(11);
                        it.subject = cmd.GetText(12);
                        it.call = cmd.GetText(13);
                        it.weeded = cmd.GetText(14);
                        it.weed_reason = cmd.GetText(15);
                        db.Items.Add(it);
                    }
                }

                // Load patrons
                using (var cmd = sdb.CreateCommand(@"SELECT id, name, cls, contact, reg, last,
                    id_murid, no_pengenalan, dob, status_pengajian, tarikh_masuk_sekolah, tarikh_masuk_kelas,
                    tahun_tingkatan, nama_kelas, nama_guru_kelas, jantina, kaum, agama,
                    warganegara, status_oku, status_yatim, nama_penjaga1, tel_penjaga1, nama_penjaga2, tel_penjaga2,
                    alamat1, alamat2, alamat3, poskod, bandar, daerah, negeri FROM patrons;"))
                {
                    while (cmd.Step())
                    {
                        var p = new Patron
                        {
                            id = cmd.GetText(0),
                            name = cmd.GetText(1),
                            cls = cmd.GetText(2),
                            contact = cmd.GetText(3),
                            reg = cmd.GetText(4),
                            last = cmd.GetText(5),
                            id_murid = cmd.GetText(6),
                            no_pengenalan = cmd.GetText(7),
                            dob = cmd.GetText(8),
                            status_pengajian = cmd.GetText(9),
                            tarikh_masuk_sekolah = cmd.GetText(10),
                            tarikh_masuk_kelas = cmd.GetText(11),
                            tahun_tingkatan = cmd.GetText(12),
                            nama_kelas = cmd.GetText(13),
                            nama_guru_kelas = cmd.GetText(14),
                            jantina = cmd.GetText(15),
                            kaum = cmd.GetText(16),
                            agama = cmd.GetText(17),
                            warganegara = cmd.GetText(18),
                            status_oku = cmd.GetText(19),
                            status_yatim = cmd.GetText(20),
                            nama_penjaga1 = cmd.GetText(21),
                            tel_penjaga1 = cmd.GetText(22),
                            nama_penjaga2 = cmd.GetText(23),
                            tel_penjaga2 = cmd.GetText(24),
                            alamat1 = cmd.GetText(25),
                            alamat2 = cmd.GetText(26),
                            alamat3 = cmd.GetText(27),
                            poskod = cmd.GetText(28),
                            bandar = cmd.GetText(29),
                            daerah = cmd.GetText(30),
                            negeri = cmd.GetText(31)
                        };
                        if (string.IsNullOrEmpty(p.no_pengenalan)) p.no_pengenalan = p.id;
                        if (string.IsNullOrEmpty(p.id_murid)) p.id_murid = p.id;
                        if (string.IsNullOrEmpty(p.nama_kelas)) p.nama_kelas = p.cls;
                        if (string.IsNullOrEmpty(p.tel_penjaga1)) p.tel_penjaga1 = p.contact;
                        db.Patrons.Add(p);
                    }
                }

                // Load loans
                using (var cmd = sdb.CreateCommand("SELECT id, iid, pid, out_date, due_date, ret_date, ren FROM loans;"))
                {
                    while (cmd.Step())
                    {
                        db.Loans.Add(new Loan
                        {
                            id = cmd.GetText(0),
                            iid = cmd.GetText(1),
                            pid = cmd.GetText(2),
                            @out = cmd.GetText(3),
                            due = cmd.GetText(4),
                            ret = cmd.GetTextOrNull(5),
                            ren = cmd.GetInt(6)
                        });
                    }
                }

                // Load holds
                using (var cmd = sdb.CreateCommand("SELECT id, iid, pid, date, st FROM holds;"))
                {
                    while (cmd.Step())
                    {
                        db.Holds.Add(new Hold
                        {
                            id = cmd.GetText(0),
                            iid = cmd.GetText(1),
                            pid = cmd.GetText(2),
                            date = cmd.GetText(3),
                            st = cmd.GetText(4)
                        });
                    }
                }

                // Load fines
                using (var cmd = sdb.CreateCommand("SELECT id, pid, iid, loan, days, amt, date, paid FROM fines;"))
                {
                    while (cmd.Step())
                    {
                        db.Fines.Add(new Fine
                        {
                            id = cmd.GetText(0),
                            pid = cmd.GetText(1),
                            iid = cmd.GetText(2),
                            loan = cmd.GetText(3),
                            days = cmd.GetInt(4),
                            amt = cmd.GetDouble(5),
                            date = cmd.GetText(6),
                            paid = cmd.GetText(7)
                        });
                    }
                }

                // Load settings
                using (var cmd = sdb.CreateCommand("SELECT school, rate, max_loans, block, zt_json, patron_req_json, opac_login FROM settings WHERE id = 1;"))
                {
                    if (cmd.Step())
                    {
                        db.Set.school = cmd.GetText(0);
                        db.Set.rate = cmd.GetDouble(1);
                        db.Set.maxLoans = cmd.GetInt(2);
                        db.Set.block = cmd.GetDouble(3);
                        string ztJson = cmd.GetText(4);
                        if (!string.IsNullOrEmpty(ztJson))
                        {
                            try
                            {
                                var arr = Json.Parse(ztJson) as List<object>;
                                if (arr != null)
                                {
                                    foreach (var o in arr)
                                    {
                                        var d = o as Dictionary<string, object>;
                                        if (d == null) continue;
                                        db.Set.zt.Add(new ZTarget
                                        {
                                            n = Json.Str(d, "n"),
                                            host = Json.Str(d, "host"),
                                            port = (int)Json.Num(d, "port"),
                                            db = Json.Str(d, "db")
                                        });
                                    }
                                }
                            }
                            catch { }
                        }
                        string reqJson = cmd.GetText(5);
                        if (!string.IsNullOrEmpty(reqJson))
                        {
                            try
                            {
                                var arr = Json.Parse(reqJson) as List<object>;
                                if (arr != null)
                                {
                                    db.Set.requiredPatronFields = arr.Select(x => x != null ? x.ToString() : "").ToList();
                                }
                            }
                            catch { }
                        }
                        db.Set.opacLogin = cmd.GetInt(6) != 0;
                    }
                }

                // Load class history
                using (var cmd = sdb.CreateCommand("SELECT year, cls, student_count, total_loans, students_json FROM class_history;"))
                {
                    while (cmd.Step())
                    {
                        var rec = new ClassYearRecord
                        {
                            year = cmd.GetInt(0),
                            cls = cmd.GetText(1),
                            studentCount = cmd.GetInt(2),
                            totalLoans = cmd.GetInt(3)
                        };
                        string sJson = cmd.GetText(4);
                        if (!string.IsNullOrEmpty(sJson))
                        {
                            try
                            {
                                var arr = Json.Parse(sJson) as List<object>;
                                if (arr != null)
                                {
                                    foreach (var so in arr)
                                    {
                                        var sd = so as Dictionary<string, object>;
                                        if (sd == null) continue;
                                        rec.students.Add(new ClassYearStudent
                                        {
                                            id = Json.Str(sd, "id"),
                                            name = Json.Str(sd, "name"),
                                            loans = (int)Json.Num(sd, "loans")
                                        });
                                    }
                                }
                            }
                            catch { }
                        }
                        db.ClassHistory.Add(rec);
                    }
                }

                // Load archived patrons
                using (var cmd = sdb.CreateCommand("SELECT id, name, cls, contact, reg, last, archived_date, loans_json, fines_json, extra_json FROM archived_patrons;"))
                {
                    while (cmd.Step())
                    {
                        var ap = new ArchivedPatron
                        {
                            id = cmd.GetText(0),
                            name = cmd.GetText(1),
                            cls = cmd.GetText(2),
                            contact = cmd.GetText(3),
                            reg = cmd.GetText(4),
                            last = cmd.GetText(5),
                            archivedDate = cmd.GetText(6)
                        };
                        string lJson = cmd.GetText(7);
                        if (!string.IsNullOrEmpty(lJson))
                        {
                            try
                            {
                                var arr = Json.Parse(lJson) as List<object>;
                                if (arr != null)
                                {
                                    foreach (var lo in arr)
                                    {
                                        var ld = lo as Dictionary<string, object>;
                                        if (ld == null) continue;
                                        var r = Json.Str(ld, "ret");
                                        ap.loans.Add(new Loan
                                        {
                                            id = Json.Str(ld, "id"),
                                            iid = Json.Str(ld, "iid"),
                                            pid = Json.Str(ld, "pid"),
                                            @out = Json.Str(ld, "out"),
                                            due = Json.Str(ld, "due"),
                                            ret = r == "" ? null : r,
                                            ren = (int)Json.Num(ld, "ren")
                                        });
                                    }
                                }
                            }
                            catch { }
                        }
                        string fJson = cmd.GetText(8);
                        if (!string.IsNullOrEmpty(fJson))
                        {
                            try
                            {
                                var arr = Json.Parse(fJson) as List<object>;
                                if (arr != null)
                                {
                                    foreach (var fo in arr)
                                    {
                                        var fd = fo as Dictionary<string, object>;
                                        if (fd == null) continue;
                                        ap.fines.Add(new Fine
                                        {
                                            id = Json.Str(fd, "id"),
                                            pid = Json.Str(fd, "pid"),
                                            iid = Json.Str(fd, "iid"),
                                            loan = Json.Str(fd, "loan"),
                                            date = Json.Str(fd, "date"),
                                            days = (int)Json.Num(fd, "days"),
                                            amt = Json.Num(fd, "amt"),
                                            paid = Json.Str(fd, "paid")
                                        });
                                    }
                                }
                            }
                            catch { }
                        }
                        string exJson = cmd.GetText(9);
                        if (!string.IsNullOrEmpty(exJson))
                        {
                            try
                            {
                                var exd = Json.Parse(exJson) as Dictionary<string, object>;
                                if (exd != null)
                                {
                                    foreach (var kv in exd)
                                    {
                                        ap.Set(kv.Key, kv.Value != null ? kv.Value.ToString() : "");
                                    }
                                }
                            }
                            catch { }
                        }
                        if (string.IsNullOrEmpty(ap.no_pengenalan)) ap.no_pengenalan = ap.id;
                        if (string.IsNullOrEmpty(ap.id_murid)) ap.id_murid = ap.id;
                        if (string.IsNullOrEmpty(ap.nama_kelas)) ap.nama_kelas = ap.cls;
                        if (string.IsNullOrEmpty(ap.tel_penjaga1)) ap.tel_penjaga1 = ap.contact;
                        db.ArchivedPatrons.Add(ap);
                    }
                }
            }

            return db;
        }

        public static void Save(string dbPath, Db db)
        {
            if (db == null) throw new ArgumentNullException("db");

            using (var sdb = new SqliteDb(dbPath))
            {
                InitSchema(sdb);

                sdb.BeginTransaction();
                try
                {
                    // Items
                    sdb.Execute("DELETE FROM items;");
                    using (var cmd = sdb.CreateCommand(@"INSERT INTO items
                        (id, ctl, acq, barcode, title, sub, author, isbn, publisher, place, year, pages, subject, call_num, weeded, weed_reason)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);"))
                    {
                        foreach (var it in db.Items)
                        {
                            cmd.Reset();
                            cmd.Bind(1, it.id);
                            cmd.Bind(2, it.ctl);
                            cmd.Bind(3, it.acq);
                            cmd.Bind(4, it.barcode);
                            cmd.Bind(5, it.title);
                            cmd.Bind(6, it.sub);
                            cmd.Bind(7, it.author);
                            cmd.Bind(8, it.isbn);
                            cmd.Bind(9, it.publisher);
                            cmd.Bind(10, it.place);
                            cmd.Bind(11, it.year);
                            cmd.Bind(12, it.pages);
                            cmd.Bind(13, it.subject);
                            cmd.Bind(14, it.call);
                            cmd.Bind(15, it.weeded);
                            cmd.Bind(16, it.weed_reason);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Patrons
                    sdb.Execute("DELETE FROM patrons;");
                    using (var cmd = sdb.CreateCommand(@"INSERT INTO patrons (
                        id, name, cls, contact, reg, last,
                        id_murid, no_pengenalan, dob, status_pengajian, tarikh_masuk_sekolah, tarikh_masuk_kelas,
                        tahun_tingkatan, nama_kelas, nama_guru_kelas, jantina, kaum, agama,
                        warganegara, status_oku, status_yatim, nama_penjaga1, tel_penjaga1, nama_penjaga2, tel_penjaga2,
                        alamat1, alamat2, alamat3, poskod, bandar, daerah, negeri
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);"))
                    {
                        foreach (var p in db.Patrons)
                        {
                            cmd.Reset();
                            cmd.Bind(1, p.id);
                            cmd.Bind(2, p.name);
                            cmd.Bind(3, p.cls);
                            cmd.Bind(4, p.contact);
                            cmd.Bind(5, p.reg);
                            cmd.Bind(6, p.last);
                            cmd.Bind(7, p.id_murid);
                            cmd.Bind(8, p.no_pengenalan);
                            cmd.Bind(9, p.dob);
                            cmd.Bind(10, p.status_pengajian);
                            cmd.Bind(11, p.tarikh_masuk_sekolah);
                            cmd.Bind(12, p.tarikh_masuk_kelas);
                            cmd.Bind(13, p.tahun_tingkatan);
                            cmd.Bind(14, p.nama_kelas);
                            cmd.Bind(15, p.nama_guru_kelas);
                            cmd.Bind(16, p.jantina);
                            cmd.Bind(17, p.kaum);
                            cmd.Bind(18, p.agama);
                            cmd.Bind(19, p.warganegara);
                            cmd.Bind(20, p.status_oku);
                            cmd.Bind(21, p.status_yatim);
                            cmd.Bind(22, p.nama_penjaga1);
                            cmd.Bind(23, p.tel_penjaga1);
                            cmd.Bind(24, p.nama_penjaga2);
                            cmd.Bind(25, p.tel_penjaga2);
                            cmd.Bind(26, p.alamat1);
                            cmd.Bind(27, p.alamat2);
                            cmd.Bind(28, p.alamat3);
                            cmd.Bind(29, p.poskod);
                            cmd.Bind(30, p.bandar);
                            cmd.Bind(31, p.daerah);
                            cmd.Bind(32, p.negeri);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Loans
                    sdb.Execute("DELETE FROM loans;");
                    using (var cmd = sdb.CreateCommand("INSERT INTO loans (id, iid, pid, out_date, due_date, ret_date, ren) VALUES (?, ?, ?, ?, ?, ?, ?);"))
                    {
                        foreach (var l in db.Loans)
                        {
                            cmd.Reset();
                            cmd.Bind(1, l.id);
                            cmd.Bind(2, l.iid);
                            cmd.Bind(3, l.pid);
                            cmd.Bind(4, l.@out);
                            cmd.Bind(5, l.due);
                            cmd.Bind(6, l.ret);
                            cmd.Bind(7, l.ren);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Holds
                    sdb.Execute("DELETE FROM holds;");
                    using (var cmd = sdb.CreateCommand("INSERT INTO holds (id, iid, pid, date, st) VALUES (?, ?, ?, ?, ?);"))
                    {
                        foreach (var h in db.Holds)
                        {
                            cmd.Reset();
                            cmd.Bind(1, h.id);
                            cmd.Bind(2, h.iid);
                            cmd.Bind(3, h.pid);
                            cmd.Bind(4, h.date);
                            cmd.Bind(5, h.st);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Fines
                    sdb.Execute("DELETE FROM fines;");
                    using (var cmd = sdb.CreateCommand("INSERT INTO fines (id, pid, iid, loan, days, amt, date, paid) VALUES (?, ?, ?, ?, ?, ?, ?, ?);"))
                    {
                        foreach (var f in db.Fines)
                        {
                            cmd.Reset();
                            cmd.Bind(1, f.id);
                            cmd.Bind(2, f.pid);
                            cmd.Bind(3, f.iid);
                            cmd.Bind(4, f.loan);
                            cmd.Bind(5, f.days);
                            cmd.Bind(6, f.amt);
                            cmd.Bind(7, f.date);
                            cmd.Bind(8, f.paid);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Settings
                    sdb.Execute("DELETE FROM settings;");
                    using (var cmd = sdb.CreateCommand("INSERT INTO settings (id, school, rate, max_loans, block, zt_json, patron_req_json, opac_login) VALUES (1, ?, ?, ?, ?, ?, ?, ?);"))
                    {
                        cmd.Bind(1, db.Set.school ?? "");
                        cmd.Bind(2, db.Set.rate);
                        cmd.Bind(3, db.Set.maxLoans);
                        cmd.Bind(4, db.Set.block);

                        string ztJson = "";
                        if (db.Set.zt != null && db.Set.zt.Count > 0)
                        {
                            var list = db.Set.zt.Select(z => (object)new Dictionary<string, object>
                            {
                                { "n", z.n }, { "host", z.host }, { "port", (double)z.port }, { "db", z.db }
                            }).ToList();
                            ztJson = Json.Write(list);
                        }
                        cmd.Bind(5, ztJson);
                        string reqJson = db.Set.requiredPatronFields != null ? Json.Write(db.Set.requiredPatronFields.Select(x => (object)x).ToList()) : "";
                        cmd.Bind(6, reqJson);
                        cmd.Bind(7, db.Set.opacLogin ? 1 : 0);
                        cmd.ExecuteNonQuery();
                    }

                    // Class history
                    sdb.Execute("DELETE FROM class_history;");
                    if (db.ClassHistory != null && db.ClassHistory.Count > 0)
                    {
                        using (var cmd = sdb.CreateCommand("INSERT INTO class_history (year, cls, student_count, total_loans, students_json) VALUES (?, ?, ?, ?, ?);"))
                        {
                            foreach (var ch in db.ClassHistory)
                            {
                                cmd.Reset();
                                cmd.Bind(1, ch.year);
                                cmd.Bind(2, ch.cls ?? "");
                                cmd.Bind(3, ch.studentCount);
                                cmd.Bind(4, ch.totalLoans);

                                string stJson = "";
                                if (ch.students != null && ch.students.Count > 0)
                                {
                                    var list = ch.students.Select(st => (object)new Dictionary<string, object>
                                    {
                                        { "id", st.id }, { "name", st.name }, { "loans", (double)st.loans }
                                    }).ToList();
                                    stJson = Json.Write(list);
                                }
                                cmd.Bind(5, stJson);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    // Archived patrons
                    sdb.Execute("DELETE FROM archived_patrons;");
                    if (db.ArchivedPatrons != null && db.ArchivedPatrons.Count > 0)
                    {
                        using (var cmd = sdb.CreateCommand(@"INSERT INTO archived_patrons
                            (id, name, cls, contact, reg, last, archived_date, loans_json, fines_json, extra_json)
                            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);"))
                        {
                            foreach (var ap in db.ArchivedPatrons)
                            {
                                cmd.Reset();
                                cmd.Bind(1, ap.id);
                                cmd.Bind(2, ap.name ?? "");
                                cmd.Bind(3, ap.cls ?? "");
                                cmd.Bind(4, ap.contact ?? "");
                                cmd.Bind(5, ap.reg ?? "");
                                cmd.Bind(6, ap.last ?? "");
                                cmd.Bind(7, ap.archivedDate ?? "");

                                string lJson = "";
                                if (ap.loans != null && ap.loans.Count > 0)
                                {
                                    var list = ap.loans.Select(l => (object)new Dictionary<string, object>
                                    {
                                        { "id", l.id }, { "iid", l.iid }, { "pid", l.pid }, { "out", l.@out }, { "due", l.due }, { "ret", l.ret }, { "ren", (double)l.ren }
                                    }).ToList();
                                    lJson = Json.Write(list);
                                }
                                cmd.Bind(8, lJson);

                                string fJson = "";
                                if (ap.fines != null && ap.fines.Count > 0)
                                {
                                    var list = ap.fines.Select(f => (object)new Dictionary<string, object>
                                    {
                                        { "id", f.id }, { "pid", f.pid }, { "iid", f.iid }, { "loan", f.loan }, { "days", (double)f.days }, { "amt", f.amt }, { "date", f.date }, { "paid", f.paid }
                                    }).ToList();
                                    fJson = Json.Write(list);
                                }
                                cmd.Bind(9, fJson);

                                var extra = new Dictionary<string, object>();
                                foreach (var k in Patron.Keys)
                                {
                                    if (k != "id" && k != "name" && k != "cls" && k != "contact" && k != "reg" && k != "last")
                                    {
                                        string v = ap.Get(k);
                                        if (!string.IsNullOrEmpty(v)) extra[k] = v;
                                    }
                                }
                                cmd.Bind(10, extra.Count > 0 ? Json.Write(extra) : "");

                                cmd.ExecuteNonQuery();
                            }
                        }
                    }

                    sdb.Commit();
                }
                catch
                {
                    sdb.Rollback();
                    throw;
                }
            }
        }

        public static List<Item> SnapshotItems(string dbPath)
        {
            var res = new List<Item>();
            if (!File.Exists(dbPath)) return res;

            try
            {
                using (var sdb = new SqliteDb(dbPath))
                {
                    using (var cmd = sdb.CreateCommand("SELECT id, ctl, acq, barcode, title, sub, author, isbn, publisher, place, year, pages, subject, call_num, weeded, weed_reason FROM items;"))
                    {
                        while (cmd.Step())
                        {
                            var it = new Item();
                            it.id = cmd.GetText(0);
                            it.ctl = cmd.GetText(1);
                            it.acq = cmd.GetText(2);
                            it.barcode = cmd.GetText(3);
                            it.title = cmd.GetText(4);
                            it.sub = cmd.GetText(5);
                            it.author = cmd.GetText(6);
                            it.isbn = cmd.GetText(7);
                            it.publisher = cmd.GetText(8);
                            it.place = cmd.GetText(9);
                            it.year = cmd.GetText(10);
                            it.pages = cmd.GetText(11);
                            it.subject = cmd.GetText(12);
                            it.call = cmd.GetText(13);
                            it.weeded = cmd.GetText(14);
                            it.weed_reason = cmd.GetText(15);
                            res.Add(it);
                        }
                    }
                }
            }
            catch { }

            return res;
        }
    }
}
