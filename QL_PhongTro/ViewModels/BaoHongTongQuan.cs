namespace QL_PhongTro.ViewModels;

public sealed class BaoHongTongQuan
{
    public int Tong { get; init; }
    public int Moi { get; init; }
    public int DangXuLy { get; init; }
    public int HoanThanhThang { get; init; }
    public int HoanThanh { get; init; }
    public int QuaHan { get; init; }
    public int SoToaNha { get; init; }
    public int KetQua { get; init; }
    public Dictionary<string, int> TheoTrangThai { get; init; } = [];
    public BaoHongChiTiet? CanhBao { get; init; }
}
