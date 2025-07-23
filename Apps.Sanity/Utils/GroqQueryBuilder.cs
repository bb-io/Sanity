namespace Apps.Sanity.Utils;

public static class GroqQueryBuilder
{
    public static string AddParameter(string groq, string parameter)
    {
        if (!string.IsNullOrEmpty(groq))
        {
            return groq + $" && {parameter}";
        }

        return parameter;
    }
}