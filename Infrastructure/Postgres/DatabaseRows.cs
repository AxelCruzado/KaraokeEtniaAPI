using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
namespace KaraokeEtniaApi.Infrastructure.Postgres;

public static class DatabaseRows
{
    // Identifiers come exclusively from EF metadata. The caller's value remains a SQL parameter.
    public static IQueryable<T> Locked<T>(DbContext db, object id) where T : class
    {
        var entity = db.Model.FindEntityType(typeof(T))!;
        var name = entity.GetTableName()!;
        var schema = entity.GetSchema();
        var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(name, schema);
        var key = entity.FindPrimaryKey()!.Properties.Single().GetColumnName(store)!;
        var sql = db.GetService<ISqlGenerationHelper>();
        var statement = "SELECT * FROM " + sql.DelimitIdentifier(name,schema) + " WHERE " + sql.DelimitIdentifier(key) + " = {0} FOR UPDATE";
        return db.Set<T>().FromSqlRaw(statement,id);
    }
}
