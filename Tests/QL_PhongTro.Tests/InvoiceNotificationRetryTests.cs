using Xunit;
using QL_PhongTro.Services;
namespace QL_PhongTro.Tests;

public class InvoiceNotificationRetryTests
{
    private static readonly DateTime BaseUtc = new(2026, 10, 16, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetNextRetry_Returns5MinAfterFirstAttempt()
    {
        var result = InvoiceNotificationDispatcher.GetNextRetry(1, BaseUtc);
        Assert.Equal(BaseUtc.AddMinutes(5), result);
    }

    [Fact]
    public void GetNextRetry_Returns10MinAfterSecondAttempt()
    {
        var result = InvoiceNotificationDispatcher.GetNextRetry(2, BaseUtc);
        Assert.Equal(BaseUtc.AddMinutes(10), result);
    }

    [Fact]
    public void GetNextRetry_Returns15MinAfterThirdAttempt()
    {
        var result = InvoiceNotificationDispatcher.GetNextRetry(3, BaseUtc);
        Assert.Equal(BaseUtc.AddMinutes(15), result);
    }

    [Fact]
    public void GetNextRetry_ReturnsNullAfterMaxAttempts()
    {
        Assert.Null(InvoiceNotificationDispatcher.GetNextRetry(4, BaseUtc));
        Assert.Null(InvoiceNotificationDispatcher.GetNextRetry(10, BaseUtc));
    }

    [Fact]
    public void GetNextRetry_ReturnsNullForAttemptZero()
    {
        Assert.Null(InvoiceNotificationDispatcher.GetNextRetry(0, BaseUtc));
    }
}
