using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Rakeeza.Commands.Bridge;

/// <summary>
/// ينفّذ طلبًا عابرًا للجسر: يجد الأمر، ويفكّ الحمولة، وينفّذ داخل نطاق DI خاصّ بالطلب،
/// ويعيد ردًّا مغلّفًا دائمًا. لا يرمي استثناءً إلى طبقة النقل.
/// </summary>
public sealed partial class CommandDispatcher
{
    private static readonly MethodInfo ExecuteTypedMethod =
        typeof(CommandDispatcher).GetMethod(nameof(ExecuteTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly CommandRegistry _registry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(CommandRegistry registry, IServiceScopeFactory scopeFactory, ILogger<CommandDispatcher> logger)
    {
        _registry = registry;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>يفكّ مغلّفًا خامًا (كما يصل من postMessage أو HTTP) ثمّ ينفّذه.</summary>
    public async Task<BridgeResponse> DispatchJsonAsync(string json, CancellationToken cancellationToken)
    {
        BridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<BridgeRequest>(json, BridgeJson.Options);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Command))
        {
            return BridgeResponse.Failure(request?.Id ?? string.Empty, BridgeErrorCodes.InvalidEnvelope);
        }

        return await DispatchAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<BridgeResponse> DispatchAsync(BridgeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_registry.TryGet(request.Command, out var descriptor))
        {
            return BridgeResponse.Failure(request.Id, BridgeErrorCodes.UnknownCommand);
        }

        object payload;
        try
        {
            payload = request.Payload.Deserialize(descriptor.RequestType, BridgeJson.Options)
                ?? throw new JsonException("Payload is null.");
        }
        catch (JsonException)
        {
            return BridgeResponse.Failure(request.Id, BridgeErrorCodes.InvalidPayload);
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService(descriptor.HandlerType);
            var execute = ExecuteTypedMethod.MakeGenericMethod(descriptor.RequestType, descriptor.ResponseType);
            var task = (Task<object>)execute.Invoke(null, [handler, payload, cancellationToken])!;
            var result = await task.ConfigureAwait(false);
            return BridgeResponse.Success(request.Id, JsonSerializer.SerializeToElement(result, descriptor.ResponseType, BridgeJson.Options));
        }
        catch (CommandException ex)
        {
            return BridgeResponse.Failure(request.Id, ex.Code, ex.Details);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BridgeResponse.Failure(request.Id, BridgeErrorCodes.Cancelled);
        }
#pragma warning disable CA1031 // الجسر يجب ألّا ينهار: كلّ خطأ غير متوقَّع يُسجَّل ويُعاد كرمز "internal".
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUnhandled(ex, request.Command);
            return BridgeResponse.Failure(request.Id, BridgeErrorCodes.Internal);
        }
    }

    private static async Task<object> ExecuteTypedAsync<TRequest, TResponse>(
        ICommand<TRequest, TResponse> handler, TRequest request, CancellationToken cancellationToken)
        where TRequest : notnull
        where TResponse : notnull =>
        await handler.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

    [LoggerMessage(Level = LogLevel.Error, Message = "Command {Command} failed with an unhandled exception")]
    private partial void LogUnhandled(Exception exception, string command);
}
