using GS.Acqu.Domain.Enums;

namespace GS.Acqu.Application.Interfaces;

/// <summary>
/// 訊息處理器介面
/// </summary>
/// <typeparam name="TMessage">訊息類型</typeparam>
public interface IMessageHandler<TMessage> where TMessage : class
{
    /// <summary>
    /// 訊息類型
    /// </summary>
    MessageType MessageType { get; }

    /// <summary>
    /// 處理訊息
    /// </summary>
    Task<ProcessResult> HandleAsync(TMessage message, CancellationToken cancellationToken = default);
}

