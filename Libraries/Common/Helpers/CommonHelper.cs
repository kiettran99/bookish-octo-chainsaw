using System.ComponentModel;
using System.Globalization;
using System.Text;
using Common.Enums;

namespace Common.Helpers;

public static class CommonHelper
{
    public static string GetDescription(Enum en)
    {
        var memberInfo = en.GetType().GetMember(nameof(en)).FirstOrDefault();

        var descriptionAttribute = memberInfo
            ?.GetCustomAttributes(typeof(DescriptionAttribute), false)
            .FirstOrDefault() as DescriptionAttribute;

        return descriptionAttribute?.Description ?? en.ToString();
    }

    public static string? JoinSeparator(this IEnumerable<string>? list, string separator = ",", bool isSpace = false)
    {
        if (list == null)
            return null;

        if (isSpace)
        {
            return string.Join(separator + " ", list);
        }
        else
        {
            return string.Join(separator, list);
        }
    }

    public static string RemoveVietnameseCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        string[] vietnameseChars = { "à", "á", "ạ", "ả", "ã", "â", "ầ", "ấ", "ậ", "ẩ", "ẫ", "ă", "ằ", "ắ", "ặ", "ẳ", "ẵ",
                                 "è", "é", "ẹ", "ẻ", "ẽ", "ê", "ề", "ế", "ệ", "ể", "ễ",
                                 "ì", "í", "ị", "ỉ", "ĩ",
                                 "ò", "ó", "ọ", "ỏ", "õ", "ô", "ồ", "ố", "ộ", "ổ", "ỗ", "ơ", "ờ", "ớ", "ợ", "ở", "ỡ",
                                 "ù", "ú", "ụ", "ủ", "ũ", "ư", "ừ", "ứ", "ự", "ử", "ữ",
                                 "ỳ", "ý", "ỵ", "ỷ", "ỹ",
                                 "đ",
                                 "À", "Á", "Ạ", "Ả", "Ã", "Â", "Ầ", "Ấ", "Ậ", "Ẩ", "Ẫ", "Ă", "Ằ", "Ắ", "Ặ", "Ẳ", "Ẵ",
                                 "È", "É", "Ẹ", "Ẻ", "Ẽ", "Ê", "Ề", "Ế", "Ệ", "Ể", "Ễ",
                                 "Ì", "Í", "Ị", "Ỉ", "Ĩ",
                                 "Ò", "Ó", "Ọ", "Ỏ", "Õ", "Ô", "Ồ", "Ố", "Ộ", "Ổ", "Ỗ", "Ơ", "Ờ", "Ớ", "Ợ", "Ở", "Ỡ",
                                 "Ù", "Ú", "Ụ", "Ủ", "Ũ", "Ư", "Ừ", "Ứ", "Ự", "Ử", "Ữ",
                                 "Ỳ", "Ý", "Ỵ", "Ỷ", "Ỹ",
                                 "Đ" };

        string[] latinChars = { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
                            "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e",
                            "i", "i", "i", "i", "i",
                            "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o",
                            "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u",
                            "y", "y", "y", "y", "y",
                            "d",
                            "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A", "A",
                            "E", "E", "E", "E", "E", "E", "E", "E", "E", "E", "E",
                            "I", "I", "I", "I", "I",
                            "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O", "O",
                            "U", "U", "U", "U", "U", "U", "U", "U", "U", "U", "U",
                            "Y", "Y", "Y", "Y", "Y",
                            "D" };

        for (int i = 0; i < vietnameseChars.Length; i++)
        {
            text = text.Replace(vietnameseChars[i], latinChars[i]);
        }

        return text;
    }

    public static string? GenerateFriendlyName(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // Remove all accents and make the string lower case
        var normalizedName = RemoveVietnameseCharacters(name);
        return normalizedName.ToLower().Replace(" ", "-");
    }

    public static ERegion GetRegionByName(string? name)
    {
        return name switch
        {
            "vi" => ERegion.VietNam,
            _ => ERegion.English,
        };
    }
}
