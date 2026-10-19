using System.ComponentModel.DataAnnotations;

namespace QL_PhongTro.ViewModels;

public class TimTinViewModel
{
    public const int KichThuocTrang = 6;
    public string SapXep { get; set; } = "moi-nhat";
    public int Trang { get; set; } = 1;
    public int TongKetQua { get; set; }
    public bool CoGoiYGia { get; set; }
    public long? GiaGoiYToiThieu { get; set; }
    public long? GiaGoiYToiDa { get; set; }
    public int TongTrang => (int)Math.Ceiling(TongKetQua / (double)KichThuocTrang);
    [StringLength(200, ErrorMessage = "Từ khóa không vượt quá 200 ký tự.")]
    public string? TuKhoa { get; set; }
    public string? PhuongXa { get; set; }
    public string? QuanHuyen { get; set; }
    [Range(0, long.MaxValue, ErrorMessage = "Giá thuê phải là số không âm.")]
    public long? GiaToiThieu { get; set; }
    [Range(0, long.MaxValue, ErrorMessage = "Giá thuê phải là số không âm.")]
    public long? GiaToiDa { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Diện tích phải là số không âm.")]
    public decimal? DienTichToiThieu { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "Diện tích phải là số không âm.")]
    public decimal? DienTichToiDa { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Số người ở tối đa phải là số nguyên dương.")]
    public int? SoNguoiToiDa { get; set; }
    public List<int> SoNguoiOptions { get; set; } = [];
    public List<string> PhuongXas { get; set; } = [];
    public List<string> QuanHuyens { get; set; } = [];
    public List<TinTimKiem> TinDangs { get; set; } = [];
    public bool SchemaReady { get; set; }
}

public class TinTimKiem
{
    public string? TenChuNha { get; set; }
    public string? DienThoaiChuNha { get; set; }
    public string? AnhDaiDien { get; set; }
    public int Id { get; set; }
    public DateTime? NgayDang { get; set; }
    public string TieuDe { get; set; } = "";
    public string? NoiDung { get; set; }
    public string MaPhong { get; set; } = "";
    public string DiaChi { get; set; } = "";
    public string? PhuongXa { get; set; }
    public string? QuanHuyen { get; set; }
    public long GiaThue { get; set; }
    public decimal DienTich { get; set; }
    public int SoNguoiToiDa { get; set; }
}
