using System.Text.RegularExpressions;

namespace BudgetTracker.Api.Infrastructure.Extensions;

public static class StringExtensions
{
    public static string ExtractJsonFromCodeBlock(this string input)
    {
        if (!input.Contains("```json"))
            return input;

        var match = Regex.Match(input, @"```json\s*([\s\S]*?)\s*```");

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        throw new FormatException("Could not extract JSON from the input string");
    }
}
