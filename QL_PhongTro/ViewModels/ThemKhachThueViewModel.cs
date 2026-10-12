using Microsoft.AspNetCore.Mvc.Rendering;
namespace QL_PhongTro.ViewModels;

public sealed class ThemKhachThueViewModel
{
    public int? HopDongId { get; set; }
    public List<SelectListItem> HopDongs { get; set; } = [];
    public HopDongDetailsViewModel? Selected { get; set; }
}
