namespace QL_PhongTro.ViewModels;

public sealed class HomeViewModel
{
    public bool RegistrationEnabled { get; set; }
    public List<string> QuanHuyens { get; set; } = [];
    public List<HomeTinDangItemViewModel> TinMoiNhat { get; set; } = [];
    public HomeDashboardViewModel Dashboard { get; set; } = new();
}

public sealed class HomeDashboardViewModel
{
    public int? SelectedBuildingId { get; set; }
    public DateOnly SelectedPeriod { get; set; }
    public List<HomeBuildingOption> BuildingOptions { get; set; } = [];
    public List<HomeRoomItem> Rooms { get; set; } = [];
    public List<HomeRequestItem> Requests { get; set; } = [];
    public List<HomeRoleItem> Roles { get; set; } = [];
    public List<QL_PhongTro.Models.NhatKyHoatDong> Activities { get; set; } = [];
    public int TongTaiKhoan { get; set; }
    public int TongChuNha { get; set; }
    public int TongToaNha { get; set; }
    public int TongPhong { get; set; }
    public int PhongTrong { get; set; }
    public int PhongDangThue { get; set; }
    public int YeuCauChoXuLy { get; set; }
    public int YeuCauCuaToi { get; set; }
    public int PhongDatCoc { get; set; }
    public List<HomeBuildingItem> Buildings { get; set; } = [];
    public List<HomeMeterAlert> MeterAlerts { get; set; } = [];
    public List<HomeMeterRoom> MeterRooms { get; set; } = [];
    public List<HomeRequestItem> PendingRequests { get; set; } = [];
    public int OverdueRequestCount { get; set; }
    public bool PaymentsAvailable { get; set; }
    public int PendingPaymentCount { get; set; }
    public long PendingPaymentTotal { get; set; }
    public long CollectedTotal { get; set; }
    public int CollectedPaymentCount { get; set; }
    public List<HomePaymentItem> PendingPayments { get; set; } = [];
    public List<HomeVacantRoom> UnlistedRooms { get; set; } = [];
    public List<string> InterestedVacantRoomCodes { get; set; } = [];
    public int UnlistedRoomCount { get; set; }
}

public sealed record HomeRoomItem(int Id, string Code, string Building, string Status, long Rent);
public sealed record HomeVacantRoom(int Id, string Code, string Building, long Rent, decimal Area);
public sealed record HomeRequestItem(int Id, string Code, string Room, string Name, string Status, DateTime Created);
public sealed record HomeRoleItem(string Name, int Count);
public sealed record HomeBuildingItem(int Id, string Name, string Address, int Rooms, int Occupied);
public sealed record HomeBuildingOption(int Id, string Name);
public sealed record HomePaymentItem(int InvoiceId, string Room, string Name, long Amount, DateOnly Date);
public sealed record HomeMeterAlert(int ToaNhaId, string TenToaNha, int Nam, int Thang, int SoPhongConThieu);
public sealed record HomeMeterRoom(int BuildingId, string Code, string TenantName, bool MissingElectricity, bool MissingWater, bool NeedsConfiguration);

public sealed class HomeTinDangItemViewModel
{
    public int Id { get; init; }
    public string TieuDe { get; init; } = string.Empty;
    public long GiaThue { get; init; }
    public decimal DienTich { get; init; }
    public int SoNguoiToiDa { get; init; }
    public string? TenToaNha { get; init; }
    public string? PhuongXa { get; init; }
    public string? QuanHuyen { get; init; }
    public string? DiaChi { get; init; }
    public string? AnhDaiDien { get; init; }
}
