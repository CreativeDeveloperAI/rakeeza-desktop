using Microsoft.Extensions.DependencyInjection;

namespace Rakeeza.Commands.Bridge;

public static class BridgeServiceCollectionExtensions
{
    /// <summary>يسجّل السجلّ والموزّع وكلّ أوامر <c>Rakeeza.Commands</c> (بنطاق الطلب).</summary>
    public static IServiceCollection AddRakeezaBridge(this IServiceCollection services)
    {
        var registry = CommandRegistry.FromCommandsAssembly();
        services.AddSingleton(registry);
        services.AddSingleton<CommandDispatcher>();
        foreach (var descriptor in registry.All)
        {
            services.AddScoped(descriptor.HandlerType);
        }

        return services;
    }
}
