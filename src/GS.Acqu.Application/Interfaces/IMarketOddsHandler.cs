using GS.Acqu.Domain.Entities;

namespace GS.Acqu.Application.Interfaces;

/// <summary>
/// 盤口賠率處理器介面
/// </summary>
public interface IMarketOddsHandler : IMessageHandler<IEnumerable<MarketInfo>>
{
}

