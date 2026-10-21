namespace QL_PhongTro.Models;

// yeu_cau_thue belongs to S2-06 and its trang_thai column carries no CHECK constraint.
// S2-08 therefore treats this list as the single definition of the values it accepts,
// and re-checks the current state in LichHenService before every write.
public static class LichHenTrangThai
{
    public const string Moi = "MOI";
    public const string DaHenLich = "DA_HEN_LICH";
    public const string DaDuyet = "DA_DUYET";
    public const string TuChoi = "TU_CHOI";
    public const string DaHuy = "DA_HUY";

    public const string LoaiXemPhong = "XEM_PHONG";
    public const string LoaiThueNgay = "THUE_NGAY";

    // phong_tro.trang_thai belongs to S1-03 and is already constrained by RoomSchemaInitializer.
    // Only the two room states S2-08 writes or checks are mirrored here, so LichHenService
    // does not have to reach into the ViewModels enum.
    public const string PhongTrong = "TRONG";
    public const string PhongDaDatCoc = "DA_DAT_COC";

    // tai_khoan.vai_tro, chép vào lịch sử để dòng đã ghi vẫn đọc đúng sau khi người đó đổi
    // vai trò hoặc tài khoản bị gỡ.
    public const string VaiTroChuNha = "CHU_NHA";
    public const string VaiTroKhachThue = "KHACH_THUE";

    public static string Label(string trangThai) => trangThai switch
    {
        Moi => "Chờ xác nhận",
        DaHenLich => "Đã xác nhận lịch hẹn",
        DaDuyet => "Đã duyệt",
        TuChoi => "Đã từ chối",
        DaHuy => "Đã huỷ",
        _ => trangThai
    };

    /// <summary>
    /// Nhãn hiển thị cho màn hình yêu cầu. Đổi lịch cố ý giữ trang_thai ở DA_HEN_LICH: bộ lọc
    /// danh sách của S2-07 chỉ nhận đúng Mới, Đã hẹn lịch, Đã duyệt, Từ chối, Đã huỷ, nên nếu ghi
    /// một giá trị DA_DOI_LICH mới thì yêu cầu sẽ rơi khỏi mọi bộ lọc đó. Vì vậy chữ "đã đổi lịch"
    /// suy ra từ cờ đã đổi lịch chứ không lưu thành trạng thái riêng.
    /// </summary>
    public static string LabelHienThi(string trangThai, bool daDoiLich) =>
        daDoiLich && trangThai == DaHenLich ? "Đã đổi lịch hẹn" : Label(trangThai);

    public static string BadgeClass(string trangThai) => trangThai switch
    {
        Moi => "text-bg-success",
        DaHenLich => "text-bg-primary",
        DaDuyet => "text-bg-info",
        TuChoi or DaHuy => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public static string LoaiLabel(string loai) => loai switch
    {
        LoaiXemPhong => "Xem phòng",
        LoaiThueNgay => "Thuê ngay",
        _ => loai
    };
}

// yeu_cau_thue.ly_do_tu_choi is a free TEXT column under S2-06, so S2-08 fixes the list of
// reasons it accepts here and LichHenService rejects anything else before writing.
public static class LyDoTuChoi
{
    public const string DaCoKhachThue = "DA_CO_KHACH_THUE";
    public const string KhongPhuHop = "KHONG_PHU_HOP_SO_NGUOI";
    public const string KhachKhongLienLacDuoc = "KHACH_KHONG_LIEN_LAC_DUOC";
    public const string Khac = "LY_DO_KHAC";

    public static readonly string[] All =
        [DaCoKhachThue, KhongPhuHop, KhachKhongLienLacDuoc, Khac];
}
