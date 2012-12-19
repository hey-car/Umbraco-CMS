using System.Collections.Generic;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;

namespace Umbraco.Core.Persistence.SqlSyntax
{
    /// <summary>
    /// Defines an SqlSyntaxProvider
    /// </summary>
    public interface ISqlSyntaxProvider
    {
        string GetQuotedTableName(string tableName);
        string GetQuotedColumnName(string columnName);
        string GetQuotedName(string name);
        bool DoesTableExist(Database db, string tableName);
        string GetIndexType(IndexTypes indexTypes);
        string GetSpecialDbType(SpecialDbTypes dbTypes);
        string CreateTable { get; }
        string DropTable { get; }
        string AddColumn { get; }
        string DropColumn { get; }
        string AlterColumn { get; }
        string RenameColumn { get; }
        string RenameTable { get; }
        string CreateSchema { get; }
        string AlterSchema { get; }
        string DropSchema { get; }
        string CreateIndex { get; }
        string DropIndex { get; }
        string InsertData { get; }
        string UpdateData { get; }
        string DeleteData { get; }
        string CreateConstraint { get; }
        string DeleteConstraint { get; }
        string CreateForeignKeyConstraint { get; }
        string Format(TableDefinition table);
        string Format(IEnumerable<ColumnDefinition> columns);
        List<string> Format(IEnumerable<IndexDefinition> indexes);
        List<string> Format(IEnumerable<ForeignKeyDefinition> foreignKeys);
        string FormatPrimaryKey(TableDefinition table);
        string GetQuotedValue(string value);
        string Format(ColumnDefinition column);
    }
}