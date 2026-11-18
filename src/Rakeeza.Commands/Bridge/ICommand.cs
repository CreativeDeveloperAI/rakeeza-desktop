namespace Rakeeza.Commands.Bridge;

/// <summary>
/// أمر واحد في الجسر: طلب <typeparamref name="TRequest"/> يُنتج ردًّا <typeparamref name="TResponse"/>.
/// كلّ تطبيق يحمل <see cref="CommandAttribute"/> باسمه الثابت (مثل <c>system.ping</c>).
/// </summary>
public interface ICommand<in TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    Task<TResponse> ExecuteAsync(TRequest request, CancellationToken cancellationToken);
}
