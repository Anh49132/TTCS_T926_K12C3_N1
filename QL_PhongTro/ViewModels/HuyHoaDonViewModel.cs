using System.ComponentModel.DataAnnotations;
namespace QL_PhongTro.ViewModels;
public sealed class HuyHoaDonViewModel
{
    public int Id { get; set; }
    public int PhienBan { get; set; }
    [Required, StringLength(1000)] public string LyDo { get; set; } = "";
    public bool XacNhan { get; set; }
}
