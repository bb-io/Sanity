using Apps.Sanity.Models;

namespace Apps.Sanity.Converters;

public static class ConverterFactory
{
    public static IJsonToHtmlConverter CreateJsonToHtmlConverter(LocalizationStrategy strategy)
    {
        return strategy switch
        {
            LocalizationStrategy.FieldLevel => new FieldLevelJsonToHtmlConverter(),
            LocalizationStrategy.DocumentLevel => new DocumentLevelJsonToHtmlConverter(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported localization strategy")
        };
    }

    public static IHtmlToJsonConverter CreateHtmlToJsonConverter(LocalizationStrategy strategy)
    {
        return strategy switch
        {
            LocalizationStrategy.FieldLevel => new FieldLevelHtmlToJsonConverter(),
            LocalizationStrategy.DocumentLevel => new DocumentLevelHtmlToJsonConverter(),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported localization strategy")
        };
    }
}
