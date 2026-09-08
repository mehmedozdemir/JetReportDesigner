using System.Text.RegularExpressions;

namespace JetReportDesigner.DataSources.Sql;

/// <summary>Thrown when a SQL data source command fails the read-only heuristic.</summary>
public sealed class UnsafeSqlCommandException(string message) : Exception(message);

/// <summary>
/// A best-effort guard that a SQL data source command only reads: it must be a
/// single statement beginning with SELECT or WITH. This is defence in depth — the
/// documented requirement is still a read-only database account.
/// </summary>
public static partial class SqlCommandGuard
{
    [GeneratedRegex(@"^\s*(SELECT|WITH)\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectStart();

    public static void EnsureSelectOnly(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            throw new UnsafeSqlCommandException("The SQL command is empty.");
        }

        if (!SelectStart().IsMatch(commandText))
        {
            throw new UnsafeSqlCommandException("Only SELECT / WITH queries are allowed for SQL data sources.");
        }

        // Disallow statement batching (block trailing DML/DDL after a ';').
        var trimmed = commandText.TrimEnd().TrimEnd(';');
        if (trimmed.Contains(';', StringComparison.Ordinal))
        {
            throw new UnsafeSqlCommandException("Multiple SQL statements are not allowed.");
        }
    }
}
