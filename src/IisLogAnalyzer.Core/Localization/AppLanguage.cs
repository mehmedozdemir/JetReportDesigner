namespace IisLogAnalyzer.Core.Localization;

public enum AppLanguage
{
    Turkish,
    English,
}

public static class AppLanguageExtensions
{
    public static System.Globalization.CultureInfo ToCultureInfo(this AppLanguage language) =>
        new(language == AppLanguage.Turkish ? "tr-TR" : "en-US");
}
