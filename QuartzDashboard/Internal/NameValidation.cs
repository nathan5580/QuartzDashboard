namespace QuartzDashboard.Internal;

/// <summary>
/// Validates user-supplied identifiers (job / group / trigger / calendar names).
/// Defence-in-depth against XSS: even after sinks are corrected, never let names
/// containing HTML metacharacters or quotes through the API boundary.
/// </summary>
internal static class NameValidation
{
    // Hard upper bound. Quartz itself does not cap key length; this just keeps
    // pathological payloads from making it into logs / SignalR fan-out.
    private const int MaxLength = 200;

    /// <summary>
    /// Returns null if the name is acceptable, or a short human-readable reason
    /// suitable for a 400 response (does not leak server internals).
    /// </summary>
    public static string? Validate(string? name, string fieldLabel = "name")
    {
        if (string.IsNullOrWhiteSpace(name))
            return $"{fieldLabel} is required";
        if (name.Length > MaxLength)
            return $"{fieldLabel} is too long (max {MaxLength} characters)";

        foreach (var ch in name)
        {
            // Control chars (incl. tab/newline/CR — they break log lines + headers)
            if (ch < 0x20 || ch == 0x7F)
                return $"{fieldLabel} contains a disallowed control character";
            // HTML / JS metacharacters. The dashboard SPA renders names in many
            // contexts (attributes, x-text, log lines); blocking these here means
            // every render path stays safe even if a downstream sink regresses.
            if (ch is '\'' or '"' or '<' or '>' or '\\' or '`')
                return $"{fieldLabel} contains a disallowed character ({ch})";
        }

        return null;
    }
}
