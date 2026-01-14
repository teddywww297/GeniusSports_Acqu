using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Domain.Interfaces;

/// <summary>
/// 結果儲存庫介面
/// </summary>
public interface IResultRepository
{
    /// <summary>
    /// 更新比賽結果
    /// </summary>
    Task UpdateResultAsync(int eventId, MatchResult result);
}

