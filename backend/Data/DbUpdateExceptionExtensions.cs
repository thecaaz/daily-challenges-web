using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DailyChallenges.Data
{
    public static class DbUpdateExceptionExtensions
    {
        private const int SqliteConstraint = 19;           // SQLITE_CONSTRAINT
        private const int SqliteConstraintPrimaryKey = 1555; // SQLITE_CONSTRAINT_PRIMARYKEY
        private const int SqliteConstraintUnique = 2067;     // SQLITE_CONSTRAINT_UNIQUE

        /// <summary>True when the save failed because of a UNIQUE or PRIMARY KEY constraint.</summary>
        public static bool IsUniqueViolation(this DbUpdateException ex)
        {
            for (Exception? inner = ex.InnerException; inner != null; inner = inner.InnerException)
            {
                if (inner is SqliteException sqlite
                    && sqlite.SqliteErrorCode == SqliteConstraint
                    && (sqlite.SqliteExtendedErrorCode == SqliteConstraintUnique
                        || sqlite.SqliteExtendedErrorCode == SqliteConstraintPrimaryKey))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
