using Microsoft.Extensions.Caching.Memory;

namespace QL_PhongTro.Services;

public class TokenBlacklistService(IMemoryCache cache)
{
    private const string BlacklistPrefix = "token_blacklist_";

    public void Blacklist(string jti, TimeSpan expiration)
    {
        var key = BlacklistPrefix + jti;
        cache.Set(key, true, expiration);
    }

    public bool IsBlacklisted(string jti)
    {
        var key = BlacklistPrefix + jti;
        return cache.TryGetValue(key, out _);
    }
}