using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace class_schedule.Infrastructure.Data;

public static class DatabaseFailureClassifier
{
    public static bool IsLockConflict(Exception error)
    {
        for (Exception? ex = error; ex != null; ex = ex.InnerException)
        {
            if (ex is SqlException sql && sql.Number is 1205 or 1222) return true;
            if (ex is DbException && ex.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" &&
                ex.GetType().GetProperty("SqliteErrorCode")?.GetValue(ex) is int code && code is 5 or 6) return true;
        }
        return false;
    }

    public static bool IsForeignKeyConflict(Exception error)
    {
        for (Exception? ex = error; ex != null; ex = ex.InnerException)
            if (ex is SqlException sql && sql.Number == 547) return true;
        return false;
    }

    public static bool IsConflict(Exception error)
    {
        if (IsLockConflict(error)) return true;
        for (Exception? ex = error; ex != null; ex = ex.InnerException)
        {
            if (ex is SqlException sql && sql.Number is 2601 or 2627) return true;
            if (ex is DbException && ex.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException" &&
                ex.GetType().GetProperty("SqliteErrorCode")?.GetValue(ex) is int code && code == 19 &&
                ex.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(ex) is int extended && extended is 1555 or 2067) return true;
        }
        return false;
    }
}
