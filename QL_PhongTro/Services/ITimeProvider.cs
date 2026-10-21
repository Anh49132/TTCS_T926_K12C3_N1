namespace QL_PhongTro.Services;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
}