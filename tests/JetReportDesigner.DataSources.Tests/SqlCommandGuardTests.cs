using JetReportDesigner.DataSources.Sql;

namespace JetReportDesigner.DataSources.Tests;

public class SqlCommandGuardTests
{
    [Theory]
    [InlineData("SELECT * FROM orders")]
    [InlineData("  select id from t where d >= :from")]
    [InlineData("WITH x AS (SELECT 1 AS n) SELECT n FROM x")]
    [InlineData("SELECT 1;")] // a single trailing semicolon is tolerated
    public void Accepts_Read_Only_Queries(string sql)
    {
        SqlCommandGuard.EnsureSelectOnly(sql); // does not throw
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DELETE FROM orders")]
    [InlineData("UPDATE orders SET total = 0")]
    [InlineData("DROP TABLE orders")]
    [InlineData("INSERT INTO orders VALUES (1)")]
    [InlineData("EXEC sp_who")]
    [InlineData("SELECT 1; DROP TABLE orders")]
    [InlineData("SELECT 1; DELETE FROM orders;")]
    public void Rejects_Writes_And_Batches(string sql)
    {
        Assert.Throws<UnsafeSqlCommandException>(() => SqlCommandGuard.EnsureSelectOnly(sql));
    }
}
