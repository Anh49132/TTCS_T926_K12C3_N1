namespace QL_PhongTro.ViewModels;
public sealed class PhatHanhNhapViewModel
{
    public int Id { get; set; }
    public int PhienBan { get; set; }
    public DateOnly NgayPhatHanh { get; set; }
    public DateOnly HanThanhToan { get; set; }
    public bool XacNhan { get; set; }
}
