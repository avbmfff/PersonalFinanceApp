using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure
{
    public class FinanceDbContextFactory : IDesignTimeDbContextFactory<FinanceDbContext>
    {
        public FinanceDbContext CreateDbContext(string[] args)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../"));
            var dbFile = Path.Combine(repoRoot, "Database", "finance.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbFile)!);

            var optionsBuilder = new DbContextOptionsBuilder<FinanceDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbFile}");

            return new FinanceDbContext(optionsBuilder.Options);
        }
    }
}