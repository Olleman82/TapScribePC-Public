using System.Text;
using System.Text.RegularExpressions;

namespace WsprPc.Services;

public sealed class TextPolisher
{
    private static readonly string[] FillerWords =
    [
        "eh",
        "öh",
        "mm",
        "liksom",
        "typ",
        "alltså"
    ];

    private static readonly Dictionary<string, string> SpokenPunctuation = new(StringComparer.OrdinalIgnoreCase)
    {
        { "punkt", "." },
        { "komma", "," },
        { "frågetecken", "?" },
        { "utropstecken", "!" },
        { "kolon", ":" },
        { "semicolon", ";" },
        { "semikolon", ";" }
    };

    public string Polish(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        string text = input.Trim();
        text = ApplyVoiceCommands(text);
        text = ReplaceSpokenPunctuation(text);
        text = RemoveFillers(text);
        text = NormalizeSpaces(text);
        text = EnsureCapitalization(text);
        text = EnsureEndsWithPunctuation(text);

        return text;
    }

    private static string ApplyVoiceCommands(string text)
    {
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "ny rad", "\n" },
            { "nytt rad", "\n" },
            { "ny rad.", "\n" },
            { "nytt stycke", "\n\n" },
            { "ny paragraf", "\n\n" },
            { "nytt stycke.", "\n\n" },
            { "ny punkt", "\n- " },
            { "nästa punkt", "\n- " }
        };

        foreach ((string word, string symbol) in replacements)
        {
            string pattern = $@"\b{Regex.Escape(word)}\b";
            text = Regex.Replace(text, pattern, symbol, RegexOptions.IgnoreCase);
        }

        string[] listStarters =
        [
            "för det första",
            "för det andra",
            "för det tredje",
            "för det fjärde",
            "för det femte"
        ];

        foreach (string starter in listStarters)
        {
            string pattern = $@"\b{Regex.Escape(starter)}\b";
            string replacement = "\n- " + char.ToUpperInvariant(starter[0]) + starter[1..];
            text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
        }

        text = Regex.Replace(
            text,
            @"\bpunkt (ett|två|tre|fyra|fem|sex|sju|åtta|nio|tio)\b",
            "\n-",
            RegexOptions.IgnoreCase);

        return text;
    }

    private static string ReplaceSpokenPunctuation(string text)
    {
        foreach ((string word, string symbol) in SpokenPunctuation)
        {
            string pattern = $@"\b{Regex.Escape(word)}\b";
            text = Regex.Replace(text, pattern, symbol, RegexOptions.IgnoreCase);
        }

        text = Regex.Replace(text, @"\s+([,.:;!?])", "$1");
        text = Regex.Replace(text, @"([,.:;!?])(\w)", "$1 $2");
        return text;
    }

    private static string RemoveFillers(string text)
    {
        foreach (string word in FillerWords)
        {
            string pattern = $@"\b{Regex.Escape(word)}\b";
            text = Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);
        }

        return text;
    }

    private static string NormalizeSpaces(string text)
    {
        string normalized = Regex.Replace(text, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @" *\n *", "\n");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        return normalized.Trim();
    }

    private static string EnsureCapitalization(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        foreach (char c in text)
        {
            if (capitalizeNext && char.IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
                continue;
            }

            sb.Append(c);

            if (c is '.' or '!' or '?' or '\n')
                capitalizeNext = true;
        }

        return sb.ToString();
    }

    private static string EnsureEndsWithPunctuation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        char last = text[^1];
        if (last is '.' or '!' or '?')
            return text;

        string trimmed = text.TrimStart();
        if (IsQuestion(trimmed))
            return text + "?";

        return text + ".";
    }

    private static bool IsQuestion(string text)
    {
        string lower = text.ToLowerInvariant();
        return lower.StartsWith("vad ")
               || lower.StartsWith("varför ")
               || lower.StartsWith("hur ")
               || lower.StartsWith("när ")
               || lower.StartsWith("var ")
               || lower.StartsWith("vem ")
               || lower.StartsWith("vilka ")
               || lower.StartsWith("vilket ")
               || lower.StartsWith("vilken ");
    }
}
