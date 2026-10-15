using System.Net;
using Xunit;
namespace QL_PhongTro.Tests;
public sealed partial class PermissionTests
{
    void HomeInvoice(int id,int contract,string code,int month,long amount,string state="DA_PHAT_HANH")
    {
        if (Convert.ToInt64(Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name='hoa_don'")) == 0)
            QL_PhongTro.Data.DichVuSchemaInitializer.InitializeInvoices(database);
        Execute("""
            INSERT INTO hoa_don(id,ma_hoa_don,hop_dong_id,thang,nam,tu_ngay,den_ngay,ngay_chot,so_nguoi_tinh_phi,loai_hoa_don,ngay_lap,han_thanh_toan,tong_tien,trang_thai,nguoi_lap_id)
            VALUES($id,$code,$contract,$month,2026,'2026-01-01','2026-01-31','2026-01-31',1,'DINH_KY','2026-01-01','2090-01-01',$amount,'NHAP',$owner);
            INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,loai_khoan,ten_khoan,so_luong,don_gia,thanh_tien)
            VALUES($id,1,'TIEN_PHONG','Tiền phòng',1,$amount,$amount);
            """,("$id",id),("$code",code),("$contract",contract),("$month",month),("$amount",amount),("$owner",accounts["CHU_NHA"]));
        if(state!="NHAP")Execute("UPDATE hoa_don SET trang_thai=$state,ngay_phat_hanh='2026-01-02',ngay_phat_hanh_nghiep_vu='2026-01-02',nguoi_phat_hanh_id=$owner WHERE id=$id",("$state",state),("$owner",accounts["CHU_NHA"]),("$id",id));
    }
    [Fact] public async Task TenantHomeInvoicesUseLinesDiscountsAndOnlyConfirmedPayments()
    {
        TenantHomeFixture();HomeInvoice(80,80,"BILL-HOME-PARTIAL",10,1000000,"NHAP");
        Execute("""
            INSERT INTO chi_tiet_hoa_don(hoa_don_id,so_thu_tu,loai_khoan,ten_khoan,so_luong,don_gia,thanh_tien,ghi_chu) VALUES(80,2,'GIAM_TRU','Giảm trừ',1,200000,200000,'Giảm trừ kiểm thử');
            UPDATE hoa_don SET tong_tien=800000,trang_thai='DA_PHAT_HANH',ngay_phat_hanh='2026-01-02',ngay_phat_hanh_nghiep_vu='2026-01-02',nguoi_phat_hanh_id=$owner WHERE id=80;
            INSERT INTO thanh_toan(hoa_don_id,so_tien,ngay_thanh_toan,trang_thai) VALUES(80,300000,'2026-01-03','DA_XAC_NHAN'),(80,500000,'2026-01-03','CHO_XAC_NHAN');
            """,("$owner",accounts["CHU_NHA"]));
        HomeInvoice(81,80,"BILL-HOME-PAID",9,100000);Execute("INSERT INTO thanh_toan(hoa_don_id,so_tien,ngay_thanh_toan,trang_thai) VALUES(81,100000,'2026-01-03','DA_XAC_NHAN')");
        HomeInvoice(82,80,"BILL-HOME-DRAFT",8,900000,"NHAP");HomeInvoice(83,81,"BILL-HOME-FOREIGN",10,900000);
        using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));
        Assert.Contains("Hóa đơn cần thanh toán</small><strong>1</strong>",html);Assert.Contains("500.000 đ",html);Assert.Contains("800.000 đ",html);Assert.Contains("Thanh toán một phần",html);Assert.Contains("Đã thanh toán",html);Assert.Contains("BILL-HOME-PARTIAL",html);Assert.Contains("BILL-HOME-PAID",html);
        Assert.DoesNotContain("BILL-HOME-DRAFT",html);Assert.DoesNotContain("BILL-HOME-FOREIGN",html);Assert.Contains("/ThongBao/ChiTiet?maHoaDon=BILL-HOME-PARTIAL",html);
    }
    [Fact] public async Task TenantHomeInvoicesExcludeCancelledButKeepDebtAfterContractEnds()
    {
        // Build invoice history before closing the contract, as required by v33.
        TenantHomeFixture(start:"2000-01-01",end:"2001-01-01");HomeInvoice(80,80,"BILL-OLD-DEBT",10,100000);HomeInvoice(81,80,"BILL-CANCELLED",9,800000);
        Execute("UPDATE hoa_don SET trang_thai='DA_HUY',ly_do_huy='Kiểm thử',nguoi_huy_id=$owner,ngay_huy='2026-01-03',phien_ban=phien_ban+1 WHERE id=81",("$owner",accounts["CHU_NHA"]));
        Execute("UPDATE hop_dong SET trang_thai='DA_KET_THUC' WHERE id=80");
        using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.Contains("BILL-OLD-DEBT",html);Assert.Contains("100.000 đ",html);Assert.DoesNotContain("BILL-CANCELLED",html);
    }
    [Fact] public async Task TenantHomeInvoicesDoNotChargeRoommate()
    {
        TenantHomeFixture(roommate:true);HomeInvoice(80,80,"BILL-MAIN-TENANT",10,100000);using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.DoesNotContain("BILL-MAIN-TENANT",html);Assert.Contains("0 đ",html);Assert.Contains("Chưa có hóa đơn đã phát hành",html);
    }
    [Fact] public async Task TenantHomeInvoicesRevokedFinancePermissionDoesNotShowBillOrZeroDebt()
    {
        TenantHomeFixture();HomeInvoice(80,80,"BILL-PRIVATE",10,100000);Execute("UPDATE role_permission SET AccessLevel='NONE' WHERE RoleCode='KHACH_THUE' AND ModuleCode='TAI_CHINH'");using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.DoesNotContain("BILL-PRIVATE",html);Assert.Contains("chưa được cấp quyền xem hóa đơn",html);Assert.Contains("Tổng cần trả</small><strong>—</strong>",html);
    }
    [Fact] public async Task TenantHomeInvoiceTotalsCoverAllBillsWhenLatestListIsLimited()
    {
        TenantHomeFixture();for(var month=1;month<=6;month++)HomeInvoice(100+month,80,"BILL-LIMIT-"+month,month,100000);
        using var tenant=await Login("KHACH_THUE");var html=WebUtility.HtmlDecode(await tenant.GetStringAsync("/"));Assert.Contains("Hóa đơn cần thanh toán</small><strong>6</strong>",html);Assert.Contains("600.000 đ",html);Assert.Contains("BILL-LIMIT-6",html);Assert.DoesNotContain("BILL-LIMIT-1<",html);
    }
}
