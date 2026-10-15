using QL_PhongTro.Services;
using Xunit;

namespace QL_PhongTro.Tests;

public sealed class NhacHanHostedServiceScheduleTests
{
    [Fact]
    public void GetNext08h00Vn_ReturnsToday08WhenUtcBefore01()
    {
        var utc = new DateTime(2026, 10, 16, 0, 30, 0, DateTimeKind.Utc);
        var result = NhacHanHostedService.GetNext08h00Vn(utc);
        Assert.Equal(new DateTime(2026, 10, 16, 1, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void GetNext08h00Vn_ReturnsTomorrow08WhenUtcAfter01()
    {
        var utc = new DateTime(2026, 10, 16, 2, 0, 0, DateTimeKind.Utc);
        var result = NhacHanHostedService.GetNext08h00Vn(utc);
        Assert.Equal(new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void GetNext08h00Vn_ReturnsTomorrow08WhenUtcExactly01()
    {
        var utc = new DateTime(2026, 10, 16, 1, 0, 0, DateTimeKind.Utc);
        var result = NhacHanHostedService.GetNext08h00Vn(utc);
        Assert.Equal(new DateTime(2026, 10, 17, 1, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void GetNext08h00Vn_ReturnsNextMonthWhenEndOfMonth()
    {
        var utc = new DateTime(2026, 10, 31, 2, 0, 0, DateTimeKind.Utc);
        var result = NhacHanHostedService.GetNext08h00Vn(utc);
        Assert.Equal(new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void GetNext08h00Vn_ReturnsNextYearWhenEndOfYear()
    {
        var utc = new DateTime(2026, 12, 31, 2, 0, 0, DateTimeKind.Utc);
        var result = NhacHanHostedService.GetNext08h00Vn(utc);
        Assert.Equal(new DateTime(2027, 1, 1, 1, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void GetNext08h00Vn_ReturnsSameDayWhenUtcBefore01OnMonthBoundary()
    {
        var utc = new DateTime(2026, 10, 31, 0, 30, 0, DateTimeKind.Utc);
        var result = NhacHanHostedService.GetNext08h00Vn(utc);
        Assert.Equal(new DateTime(2026, 10, 31, 1, 0, 0, DateTimeKind.Utc), result);
    }
}
