using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QL_PhongTro.Data;

namespace QL_PhongTro.Services;

// Uses exactly the public visibility rules; never reads tenant/contract/account tables.
public sealed class PublicListingChatTool(AppDbContext db, ITimeProvider clock)
{
    public static object Parameters => new {
        type = "OBJECT", properties = new Dictionary<string, object> {
            ["search"] = new { type = "STRING", description = "Tên đường, khu vực hoặc tiêu đề tin; không tự thêm từ phòng/trọ, tối đa100ký tự" },
            ["district"] = new { type = "STRING", description = "Quận/huyện nếu người dùng nêu rõ, tối đa100ký tự" },
            ["minPrice"] = new { type = "INTEGER", description = "Giá thuê tối thiểu bằng đồng/tháng, ví dụ2000000" },
            ["maxPrice"] = new { type = "INTEGER", description = "Giá thuê tối đa bằng đồng/tháng, ví dụ3000000" },
            ["people"] = new { type = "INTEGER", description = "Số người cần ở,1..50; sức chứa phòng phải ít nhất số này" },
            ["minArea"] = new { type = "NUMBER", description = "Diện tích tối thiểu m2" },
            ["maxArea"] = new { type = "NUMBER", description = "Diện tích tối đa m2" }
        }
    };

    public async Task<ChatToolResult> SearchAsync(JsonElement args, CancellationToken ct)
    {
        if (args.ValueKind != JsonValueKind.Object) throw new ArgumentException();
        string? Text(string key)
        {
            if (!args.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind != JsonValueKind.String || v.GetString()!.Length > 100) throw new ArgumentException();
            var text = v.GetString()!.Trim();return text.Length == 0 ? null : text.ToLowerInvariant();
        }
        long? Whole(string key, long max)
        {
            if (!args.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (!v.TryGetInt64(out var value) || value < 0 || value > max) throw new ArgumentException();return value;
        }
        decimal? Area(string key)
        {
            if (!args.TryGetProperty(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (!v.TryGetDecimal(out var value) || value < 0 || value > 10000) throw new ArgumentException();return value;
        }
        var search = Text("search");var district = Text("district");
        var min = Whole("minPrice",1000000000);var max = Whole("maxPrice",1000000000);var people = Whole("people",50);
        var minArea = Area("minArea");var maxArea = Area("maxArea");
        if (min > max || minArea > maxArea || people == 0) throw new ArgumentException("Bộ lọc tìm phòng không hợp lệ.");
        var now = clock.UtcNow;
        var query = from listing in db.TinDangs.AsNoTracking()
                    join room in db.PhongTros.AsNoTracking() on listing.PhongId equals room.Id
                    join building in db.ToaNhas.AsNoTracking() on room.ToaNhaId equals building.Id
                    where listing.TrangThai == "DANG_HIEN_THI" && listing.NgayHetHan.HasValue && listing.NgayHetHan >= now
                    && room.TrangThai == "TRONG" && building.DangHoatDong
                    && (!min.HasValue || room.GiaThue >= min) && (!max.HasValue || room.GiaThue <= max)
                    && (!people.HasValue || room.SoNguoiToiDa >= people)
                    && (!minArea.HasValue || room.DienTich >= minArea) && (!maxArea.HasValue || room.DienTich <= maxArea)
                    && (district == null || building.QuanHuyen != null && building.QuanHuyen.ToLower().Contains(district))
                    && (search == null || listing.TieuDe.ToLower().Contains(search) || building.DiaChi.ToLower().Contains(search)
                        || building.QuanHuyen != null && building.QuanHuyen.ToLower().Contains(search)
                        || building.PhuongXa != null && building.PhuongXa.ToLower().Contains(search))
                    select new { listing.Id, title = listing.TieuDe, price = room.GiaThue, area = room.DienTich,
                        capacity = room.SoNguoiToiDa, address = building.DiaChi, district = building.QuanHuyen, ward = building.PhuongXa };
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.price).ThenByDescending(x => x.Id).Take(6).ToListAsync(ct);
        List<ChatLink> links = rows.Select(x => new ChatLink($"Xem phòng: {x.title}", $"/TinDang/ChiTiet/{x.Id}")).ToList();
        links.Add(new("Tìm và lọc tất cả phòng", "/TimTin"));
        return new(new { count, filters = new { search, district, minPrice = min, maxPrice = max, people, minArea, maxArea },
            items = rows.Select(x => new { listingId = x.Id, x.title, x.price, x.area, x.capacity, x.address, x.district, x.ward }),
            note = "Chỉ tin công khai còn hạn, phòng trống, tòa hoạt động. Giá là tiền thuê phòng/tháng, chưa cộng điện nước/dịch vụ/cọc. Tối đa6tin giá tăng dần; count là tổng khớp bộ lọc. Không có kết quả thì hỏi nới tiêu chí, không tự bịa phòng. Muốn gửi yêu cầu xem/thuê phòng cần đăng ký/đăng nhập." }, links);
    }
}
