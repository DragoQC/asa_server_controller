using System.Text;

namespace managerwebapp.Services;

public sealed class MapNameService
{
    public string Format(string? mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return "Unknown";
        }

        string normalized = mapName.Trim();
        if (normalized.EndsWith("_WP", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3];
        }

        StringBuilder builder = new();
        for (int index = 0; index < normalized.Length; index++)
        {
            char current = normalized[index];
            if (index > 0 &&
                char.IsUpper(current) &&
                !char.IsWhiteSpace(normalized[index - 1]) &&
                !char.IsUpper(normalized[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        if (builder.Length == 0)
        {
            return "Unknown";
        }

        string text = builder.ToString();
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
