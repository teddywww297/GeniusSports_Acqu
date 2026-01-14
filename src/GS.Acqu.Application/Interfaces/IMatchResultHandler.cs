using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Application.Interfaces;

/// <summary>
/// 比賽結果處理器介面
/// </summary>
public interface IMatchResultHandler : IMessageHandler<MatchResult>
{
}

