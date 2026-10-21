param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$runDirectory = Join-Path $repository ('data/S1-09-verification/schema-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
$schemaSource = @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class S109SchemaCheck {
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_exec(IntPtr db, byte[] sql, Callback callback, IntPtr state, out IntPtr error);
    [DllImport("winsqlite3", CallingConvention=CallingConvention.Cdecl)] static extern void sqlite3_free(IntPtr pointer);
    delegate int Callback(IntPtr state, int count, IntPtr values, IntPtr names);
    static byte[] Utf8(string value) { return Encoding.UTF8.GetBytes(value + "\0"); }
    static int Execute(IntPtr db, string sql, Callback callback = null) {
        IntPtr error; var result = sqlite3_exec(db, Utf8(sql), callback, IntPtr.Zero, out error);
        if (error != IntPtr.Zero) sqlite3_free(error);
        return result;
    }
    static void Require(IntPtr db, string sql) { if (Execute(db, sql) != 0) throw new Exception("SQL check failed: " + sql); }
    public static void Run(string path, string schema) {
        IntPtr db; if(sqlite3_open_v2(Utf8(path), out db, 6, IntPtr.Zero) != 0) throw new Exception("Cannot open disposable DB");
        try {
            Require(db, "PRAGMA foreign_keys=ON; CREATE TABLE tai_khoan(id INTEGER PRIMARY KEY); CREATE TABLE toa_nha(id INTEGER PRIMARY KEY); CREATE TABLE phong_tro(id INTEGER PRIMARY KEY); INSERT INTO tai_khoan VALUES(1); INSERT INTO toa_nha VALUES(1);");
            Require(db, "BEGIN;" + schema + "COMMIT;");
            Require(db, "INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu) VALUES('CUSTOM','Custom service');");
            var methods = new[] { "THEO_CHI_SO", "THEO_NGUOI", "CO_DINH" };
            for(var i=0; i<methods.Length; i++) {
                Require(db, "INSERT INTO cau_hinh_dich_vu(toa_nha_id,dich_vu_id,cach_tinh,don_vi_tinh,don_gia,tu_ngay,den_ngay,nguoi_tao_id,ngay_tao) VALUES(1,1,'"+methods[i]+"','unit',0,'2026-09-"+(i+1).ToString("00")+"','2026-09-"+(i+1).ToString("00")+"',1,'2026-09-28');");
            }
            Console.WriteLine("PASS: all three calculation methods; zero price accepted");
            foreach(var bad in new[] { "-1", "1.5", "NULL", "'abc'" }) {
                if(Execute(db, "UPDATE cau_hinh_dich_vu SET don_gia="+bad) == 0) throw new Exception("Invalid price accepted: " + bad);
            }
            Console.WriteLine("PASS: negative, fractional, missing and nonnumeric prices rejected");
            foreach(var assignment in new[] { "cach_tinh='INVALID'", "don_vi_tinh='  '", "toa_nha_id=999", "nguoi_tao_id=999" }) {
                if(Execute(db, "UPDATE cau_hinh_dich_vu SET " + assignment) == 0) throw new Exception("Invalid configuration accepted: " + assignment);
            }
            Console.WriteLine("PASS: method, unit and foreign-key constraints");
            if(Execute(db, "INSERT INTO dich_vu(ma_dich_vu,ten_dich_vu) VALUES('EMPTY','  ')") == 0) throw new Exception("Blank name accepted");
            if(Execute(db, "DELETE FROM dich_vu WHERE id=1") == 0) throw new Exception("Referenced service deleted");
            if(Execute(db, "UPDATE cau_hinh_dich_vu SET tu_ngay='2026-09-01'") == 0) throw new Exception("Duplicate building price accepted");
            Console.WriteLine("PASS: blank name, referenced deletion, duplicate scope/date rejected");
            string result = null;
            Require(db, "UPDATE cau_hinh_dich_vu SET don_gia=3500 WHERE id=1");
            Callback value = (s,n,v,k) => { result=Marshal.PtrToStringAnsi(Marshal.ReadIntPtr(v)); return 0; };
            if(Execute(db, "SELECT don_gia FROM cau_hinh_dich_vu WHERE id=1", value) != 0 || result != "3500") throw new Exception("Stored price mismatch");
            if(Execute(db, "PRAGMA integrity_check", value) != 0 || result != "ok") throw new Exception("Integrity check failed");
            Console.WriteLine("PASS: stored price=3500; integrity_check=ok");
        } finally { sqlite3_close(db); }
    }
}
'@
Add-Type -TypeDefinition $schemaSource
[S109SchemaCheck]::Run((Join-Path $runDirectory 'schema.sqlite'), (Get-Content (Join-Path $repository 'docs/sql/S1-09-dich-vu.sql') -Raw -Encoding UTF8))
Write-Output "Schema-only verification complete: $runDirectory"
