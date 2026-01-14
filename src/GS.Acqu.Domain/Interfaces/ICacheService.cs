namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 快取服務介面
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// 取得快取
    /// </summary>
    Task<T?> GetAsync<T>(string key) where T : class;

    /// <summary>
    /// 設定快取
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;

    /// <summary>
    /// 移除快取
    /// </summary>
    Task RemoveAsync(string key);

    /// <summary>
    /// 檢查快取是否存在
    /// </summary>
    Task<bool> ExistsAsync(string key);
}

