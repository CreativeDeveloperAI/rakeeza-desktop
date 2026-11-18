using System.Text.Json;

namespace Rakeeza.Commands.Bridge;

/// <summary>طلب يعبر الجسر: <c>{ id, command, payload }</c>. الشكل نفسه في HTTP وفي postMessage.</summary>
public sealed record BridgeRequest(string Id, string Command, JsonElement Payload);

/// <summary>ردّ يعبر الجسر: <c>{ id, ok, result }</c> أو <c>{ id, ok: false, error }</c>.</summary>
public sealed record BridgeResponse(string Id, bool Ok, JsonElement? Result, BridgeError? Error)
{
    public static BridgeResponse Success(string id, JsonElement result) => new(id, true, result, null);

    public static BridgeResponse Failure(string id, string code, IReadOnlyDictionary<string, string>? details = null) =>
        new(id, false, null, new BridgeError(code, details));
}

public sealed record BridgeError(string Code, IReadOnlyDictionary<string, string>? Details);
