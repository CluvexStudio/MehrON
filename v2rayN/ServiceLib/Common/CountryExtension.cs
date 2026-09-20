namespace ServiceLib.Common;

/// <summary>
/// Extension methods for country code utilities
/// </summary>
public static class CountryExtension
{
    /// <summary>
    /// Country code to emoji flag mapping for common countries
    /// </summary>
    private static readonly Dictionary<string, string> CountryEmojiMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Asia
        { "CN", "🇨🇳" }, // China
        { "HK", "🇭🇰" }, // Hong Kong
        { "TW", "🇹🇼" }, // Taiwan
        { "JP", "🇯🇵" }, // Japan
        { "SG", "🇸🇬" }, // Singapore
        { "KR", "🇰🇷" }, // South Korea
        { "TH", "🇹🇭" }, // Thailand
        { "VN", "🇻🇳" }, // Vietnam
        { "ID", "🇮🇩" }, // Indonesia
        { "PH", "🇵🇭" }, // Philippines
        { "MY", "🇲🇾" }, // Malaysia
        { "IN", "🇮🇳" }, // India
        { "PK", "🇵🇰" }, // Pakistan
        { "BD", "🇧🇩" }, // Bangladesh
        { "LK", "🇱🇰" }, // Sri Lanka
        { "KH", "🇰🇭" }, // Cambodia
        { "LA", "🇱🇦" }, // Laos
        { "MM", "🇲🇲" }, // Myanmar

        // Americas
        { "US", "🇺🇸" }, // United States
        { "CA", "🇨🇦" }, // Canada
        { "MX", "🇲🇽" }, // Mexico
        { "BR", "🇧🇷" }, // Brazil
        { "AR", "🇦🇷" }, // Argentina
        { "CL", "🇨🇱" }, // Chile
        { "CO", "🇨🇴" }, // Colombia

        // Europe
        { "GB", "🇬🇧" }, // United Kingdom
        { "DE", "🇩🇪" }, // Germany
        { "FR", "🇫🇷" }, // France
        { "IT", "🇮🇹" }, // Italy
        { "ES", "🇪🇸" }, // Spain
        { "RU", "🇷🇺" }, // Russia
        { "NL", "🇳🇱" }, // Netherlands
        { "CH", "🇨🇭" }, // Switzerland
        { "SE", "🇸🇪" }, // Sweden
        { "NO", "🇳🇴" }, // Norway
        { "DK", "🇩🇰" }, // Denmark
        { "FI", "🇫🇮" }, // Finland
        { "PL", "🇵🇱" }, // Poland
        { "CZ", "🇨🇿" }, // Czech Republic
        { "AT", "🇦🇹" }, // Austria
        { "GR", "🇬🇷" }, // Greece
        { "PT", "🇵🇹" }, // Portugal
        { "TR", "🇹🇷" }, // Turkey
        { "UA", "🇺🇦" }, // Ukraine
        { "RO", "🇷🇴" }, // Romania

        // Middle East & Central Asia
        { "AE", "🇦🇪" }, // United Arab Emirates
        { "SA", "🇸🇦" }, // Saudi Arabia
        { "IL", "🇮🇱" }, // Israel
        { "KZ", "🇰🇿" }, // Kazakhstan

        // Africa
        { "ZA", "🇿🇦" }, // South Africa
        { "EG", "🇪🇬" }, // Egypt
        { "IR", "🇮🇷" }, // Iran
    };

    private static readonly Dictionary<string, string> CountryFaNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "DE", "آلمان" },
        { "US", "آمریکا" },
        { "NL", "هلند" },
        { "FR", "فرانسه" },
        { "GB", "انگلستان" },
        { "UK", "انگلستان" },
        { "FI", "فنلاند" },
        { "TR", "ترکیه" },
        { "IR", "ایران" },
        { "CA", "کانادا" },
        { "IT", "ایتالیا" },
        { "ES", "اسپانیا" },
        { "RU", "روسیه" },
        { "CH", "سوئیس" },
        { "SE", "سوئد" },
        { "NO", "نروژ" },
        { "PL", "لهستان" },
        { "AT", "اتریش" },
        { "SG", "سنگاپور" },
        { "JP", "ژاپن" },
        { "KR", "کره جنوبی" },
        { "AE", "امارات" },
        { "AZ", "آذربایجان" },
        { "AM", "ارمنستان" },
        { "GE", "گرجستان" },
        { "AU", "استرالیا" },
        { "UA", "اوکراین" },
        { "RO", "رومانی" },
        { "BG", "بلغارستان" },
        { "GR", "یونان" },
        { "BE", "بلژیک" },
        { "DK", "دانمارک" },
        { "IE", "ایرلند" },
        { "PT", "پرتغال" },
        { "CZ", "جمهوری چک" },
        { "HU", "مجارستان" },
        { "IN", "هند" },
        { "HK", "هنگ کنگ" },
        { "TW", "تایوان" },
    };

    /// <summary>
    /// Converts country code to flag emoji using predefined mapping or Unicode regional indicator symbols.
    /// Example: "US" -> "🇺🇸", "DE" -> "🇩🇪"
    /// </summary>
    public static string? CountryToEmoji(this string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            return null;
        }

        countryCode = countryCode.ToUpperInvariant();
        if (CountryEmojiMap.TryGetValue(countryCode, out var emoji))
        {
            return emoji;
        }

        if (countryCode[0] >= 'A' && countryCode[0] <= 'Z' && countryCode[1] >= 'A' && countryCode[1] <= 'Z')
        {
            return char.ConvertFromUtf32(0x1F1E6 + (countryCode[0] - 'A')) +
                   char.ConvertFromUtf32(0x1F1E6 + (countryCode[1] - 'A'));
        }

        return null;
    }

    /// <summary>
    /// Converts country code to localized or English country name.
    /// Example: "DE" -> "Germany" (or "آلمان" if isFa=true)
    /// </summary>
    public static string? CountryToName(this string? countryCode, bool isFa = false)
    {
        if (countryCode.IsNullOrEmpty())
        {
            return null;
        }

        if (isFa && CountryFaNameMap.TryGetValue(countryCode, out var faName))
        {
            return faName;
        }

        try
        {
            if (countryCode.Length == 2)
            {
                var region = new System.Globalization.RegionInfo(countryCode);
                return region.EnglishName;
            }
        }
        catch
        {
        }

        return countryCode;
    }
}
