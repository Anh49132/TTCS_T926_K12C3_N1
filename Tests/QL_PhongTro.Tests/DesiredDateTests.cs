using QL_PhongTro.Services;
using QL_PhongTro.ViewModels;
using Xunit;

namespace QL_PhongTro.Tests;

public class DesiredDateTests
{
    private sealed class Clock(DateTime utcNow) : ITimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    [Theory]
    [InlineData(16, 59, 1)]
    [InlineData(17, 0, 2)]
    public void RangeUsesVietnamCalendarAcrossUtcMidnightBoundary(int hour, int minute, int day)
    {
        var service = new YeuCauThueService(null!, new Clock(new DateTime(2026, 10, 1, hour, minute, 0, DateTimeKind.Utc)));
        var today = new DateOnly(2026, 10, day);
        Assert.Equal(today, service.Today);
        Assert.Null(service.ValidateDesiredDate(today));
        Assert.Null(service.ValidateDesiredDate(today.AddDays(60)));
        Assert.NotNull(service.ValidateDesiredDate(today.AddDays(-1)));
        Assert.NotNull(service.ValidateDesiredDate(today.AddDays(61)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    public async Task SendRejectsOutOfRangeBeforeAccessingDatabase(int offset)
    {
        var service = new YeuCauThueService(null!, new Clock(new DateTime(2026, 10, 1, 17, 0, 0, DateTimeKind.Utc)));
        await Assert.ThrowsAsync<DesiredDateException>(() => service.Send(1, 1,
            new GuiYeuCauViewModel { NgayMongMuon = service.Today.AddDays(offset) }));
    }
}
