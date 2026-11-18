using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rakeeza.Commands.Bridge;

/// <summary>إعدادات JSON الوحيدة للجسر. مولّد أنواع TS يتبع القواعد نفسها (camelCase، والـenum نصوص).</summary>
public static class BridgeJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            // الإعداد Web يقبل الأرقام المكتوبة نصوصًا ("2")؛ الجسر صارم، فالنوع الخاطئ رفض.
            NumberHandling = JsonNumberHandling.Strict,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
