using Microsoft.AspNetCore.Mvc.Rendering;
namespace QL_PhongTro.ViewModels;
public sealed record AdminMeterRow(string Building, string Room, string Contract, string Service,
    DateOnly From, DateOnly To, decimal First, decimal Last, bool Locked);
public sealed class AdminMeterViewModel
{
    public List<SelectListItem> Buildings { get; set; } = [];
    public int? ToaNhaId { get; set; }
    public DateOnly Period { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int Pages => Math.Max(1,(Total+49)/50);
    public List<AdminMeterRow> Rows { get; set; } = [];
}
