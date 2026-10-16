using QL_PhongTro.ViewModels;
using Xunit;
namespace QL_PhongTro.Tests;
public class BuildingPaymentSummaryTests
{
    [Theory]
    [InlineData(0, false, false, "CHUA_TRA", 1000)]
    [InlineData(400, false, false, "MOT_PHAN", 600)]
    [InlineData(1000, true, true, "DA_TRA", 0)]
    [InlineData(1200, true, false, "DA_TRA", 0)]
    [InlineData(400, true, true, "QUA_HAN", 600)]
    [InlineData(400, false, true, "CHO_XAC_NHAN", 600)]
    public void ClassifiesRemainingBalanceAndPriority(long paid, bool overdue, bool pending, string code, long remaining)
    {
        var summary = ThanhToanPhongSummary.Create(1000, paid, overdue, pending);
        Assert.Equal(code, summary.Ma);
        Assert.Equal(remaining, summary.ConNo);
    }
}
