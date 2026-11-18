namespace Rakeeza.Commands.Bridge;

/// <summary>وصف أمر مسجَّل: اسمه، ونوعا الطلب والردّ، والصنف المنفِّذ.</summary>
public sealed record CommandDescriptor(string Name, Type RequestType, Type ResponseType, Type HandlerType);
