using System.Globalization;

namespace Hospital;

/// <summary>Konsolės įvesties pagalbinės funkcijos su patikrinimu.</summary>
public static class Input
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public static string Text(string prompt, string? defaultValue = null, bool optional = false)
    {
        while (true)
        {
            Console.Write(string.IsNullOrEmpty(defaultValue) ? $"{prompt}: " : $"{prompt} [{defaultValue}]: ");
            var line = Console.ReadLine()?.Trim() ?? "";
            if (line.Length > 0) return line;
            if (!string.IsNullOrEmpty(defaultValue)) return defaultValue;
            if (optional) return "";
            Console.WriteLine("  Reikšmė negali būti tuščia.");
        }
    }

    public static int Int(string prompt, int min, int max, int? defaultValue = null)
    {
        while (true)
        {
            if (int.TryParse(Text(prompt, defaultValue?.ToString()), out var value) && value >= min && value <= max)
                return value;
            Console.WriteLine($"  Įveskite skaičių nuo {min} iki {max}.");
        }
    }

    public static DateTime Date(string prompt, DateTime? defaultValue = null) =>
        ParseDate(prompt, DateFormat, defaultValue);

    public static DateTime DateAndTime(string prompt, DateTime? defaultValue = null) =>
        ParseDate(prompt, DateTimeFormat, defaultValue);

    private static DateTime ParseDate(string prompt, string format, DateTime? defaultValue)
    {
        while (true)
        {
            var text = Text($"{prompt} ({format})", defaultValue?.ToString(format, CultureInfo.InvariantCulture));
            if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var value))
                return value;
            Console.WriteLine($"  Neteisingas formatas, pvz.: {DateTime.Now.ToString(format, CultureInfo.InvariantCulture)}");
        }
    }

    public static bool Confirm(string prompt)
    {
        while (true)
        {
            switch (Text($"{prompt} (t/n)").ToLowerInvariant())
            {
                case "t": return true;
                case "n": return false;
            }
        }
    }

    /// <summary>Skaito eilutes, kol įvedama tuščia eilutė.</summary>
    public static List<string> Lines(string prompt)
    {
        var lines = new List<string>();
        while (Text($"{prompt} (Enter – baigti)", optional: true) is { Length: > 0 } line)
            lines.Add(line);
        return lines;
    }

    public static T? Choose<T>(string title, IReadOnlyList<T> items, Func<T, string> describe) where T : class
    {
        if (items.Count == 0)
        {
            Console.WriteLine("  Sąrašas tuščias.");
            return null;
        }

        Console.WriteLine(title);
        for (var i = 0; i < items.Count; i++)
            Console.WriteLine($"  {i + 1}. {describe(items[i])}");
        Console.WriteLine("  0. Atšaukti");

        var choice = Int("Pasirinkimas", 0, items.Count);
        return choice == 0 ? null : items[choice - 1];
    }
}
