using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Application.Interfaces;

/// <summary>
/// 賽事資訊處理器介面
/// </summary>
public interface IMatchInfoHandler : IMessageHandler<MatchInfo>
{
}

