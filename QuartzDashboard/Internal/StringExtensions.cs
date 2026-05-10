namespace QuartzDashboard.Internal;

internal static class StringExtensions
{
    public static string Truncate(this string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? "" : (value.Length <= maxLength ? value : value[..maxLength] + "...");
}
