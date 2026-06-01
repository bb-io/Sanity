using Apps.Sanity.Models;
using Blackbird.Applications.Sdk.Common.Exceptions;

namespace Apps.Sanity.Utils;

public static class LocalizationStrategyExtensions
{
    public static LocalizationStrategy ParseLocalizationStrategy(this string strategy)
    {
        LocalizationStrategy enumStrategy;
        try
        {
            enumStrategy = Enum.Parse<LocalizationStrategy>(strategy);
        }
        catch (Exception)
        {
            var supportedStrategies = string.Join(", ", Enum.GetNames<LocalizationStrategy>());
            throw new PluginMisconfigurationException($"Could not parse localization strategy '{strategy}', supported values are: {supportedStrategies}.");
        }
        
        return enumStrategy;
    }
}