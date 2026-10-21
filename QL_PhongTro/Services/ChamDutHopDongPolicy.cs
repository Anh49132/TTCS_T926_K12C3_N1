using QL_PhongTro.Models;
namespace QL_PhongTro.Services;

public class ChamDutInput
{
    public string? LyDo { get; set; }
    public string? LyDoKhac { get; set; }
    public bool ApDungPhat { get; set; }
    public decimal? TienPhat { get; set; }
}

public static class ChamDutHopDongPolicy
{
    public static readonly IReadOnlyDictionary<string,string> LyDos = new Dictionary<string,string>
    {
        ["KHACH_CHUYEN_DI"]="Khách chủ động chuyển đi",
        ["THOA_THUAN"]="Hai bên thỏa thuận",
        ["VI_PHAM"]="Khách vi phạm hợp đồng",
        ["KHAC"]="Lý do khác"
    };
    public static string MoTa(ChamDutHopDong info) => info.LyDo=="KHAC" ? info.LyDoKhac??"" : LyDos.GetValueOrDefault(info.LyDo??"")??"";
    public static void DienInput(ChamDutInput input, ChamDutHopDong? saved)
    {
        if(saved is null)return;
        input.LyDo=saved.LyDo;input.LyDoKhac=saved.LyDoKhac;input.ApDungPhat=saved.ApDungPhat;input.TienPhat=saved.TienPhat;
    }
    // Dates and cap come exclusively from the contract, never from posted form fields.
    public static ChamDutHopDong KiemTra(ChamDutInput input,DateOnly day,DateOnly end,long deposit)
    {
        var info=new ChamDutHopDong { NgayTraPhong=day,NgayKetThuc=end,CocThoaThuan=deposit,TruocHan=day<end };
        if(!info.TruocHan)
        {
            if(input.ApDungPhat)throw new InvalidOperationException("Chỉ áp dụng phạt cọc khi trả phòng trước hạn.");
            return info;
        }
        if(input.LyDo is null || !LyDos.ContainsKey(input.LyDo))throw new InvalidOperationException("Chọn lý do chấm dứt trước hạn hợp lệ.");
        info.LyDo=input.LyDo;
        if(input.LyDo=="KHAC")
        {
            if(string.IsNullOrWhiteSpace(input.LyDoKhac) || input.LyDoKhac.Trim().Length>500)
                throw new InvalidOperationException("Ghi rõ lý do khác, tối đa 500 ký tự.");
            info.LyDoKhac=input.LyDoKhac.Trim();
        }
        info.ApDungPhat=input.ApDungPhat;
        if(input.ApDungPhat)
        {
            if(input.TienPhat is null || input.TienPhat<=0 || input.TienPhat>deposit || decimal.Truncate(input.TienPhat.Value)!=input.TienPhat)
                throw new InvalidOperationException("Phạt cọc phải là số nguyên đồng, lớn hơn 0 và không vượt tiền cọc thỏa thuận.");
            info.TienPhat=checked((long)input.TienPhat.Value);
        }
        return info;
    }
}
