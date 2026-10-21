using Microsoft.Data.Sqlite;
namespace QL_PhongTro.Data;
public static class RoomNotesSchema {
 public static void Upgrade(SqliteConnection connection) {
  using var tx=connection.BeginTransaction();using var command=connection.CreateCommand();command.Transaction=tx;
  command.CommandText="""
  CREATE TABLE ghi_chu_phong (
   id INTEGER PRIMARY KEY AUTOINCREMENT,
   phong_id INTEGER NOT NULL REFERENCES phong_tro(id) ON DELETE RESTRICT,
   nguoi_tao_id INTEGER NOT NULL REFERENCES tai_khoan(id) ON DELETE RESTRICT,
   tieu_de TEXT NOT NULL CHECK(length(tieu_de) BETWEEN 1 AND 100),
   noi_dung TEXT NOT NULL CHECK(length(noi_dung) BETWEEN 1 AND 1000),
   ghim INTEGER NOT NULL DEFAULT 0 CHECK(ghim IN (0,1)),
   ngay_tao TEXT NOT NULL,
   khoa_chong_trung TEXT NOT NULL UNIQUE,
   ten_tep TEXT,
   loai_tep TEXT,
   noi_dung_tep BLOB CHECK(noi_dung_tep IS NULL OR length(noi_dung_tep)<=10485760),
   CHECK((ten_tep IS NULL AND loai_tep IS NULL AND noi_dung_tep IS NULL) OR (ten_tep IS NOT NULL AND loai_tep IN ('application/pdf','image/png','image/jpeg') AND noi_dung_tep IS NOT NULL))
  );
  CREATE INDEX ix_ghi_chu_phong_ngay ON ghi_chu_phong(phong_id,ghim DESC,ngay_tao DESC);
  INSERT INTO app_schema_version(version,applied_at) VALUES(25,strftime('%Y-%m-%dT%H:%M:%fZ','now'));
  """;command.ExecuteNonQuery();tx.Commit();
 }
}
