/*
** Copyright (c) 2018 Actian Corporation. All Rights Reserved.
*/

/*
** Name: Migration.cs
**
** Description:
**	Implements SQL generation of the metadata (tables, columns, primary keys, etc)
**	in the creation, alteration, and migration of the Ingres objects.
**
**
** Classes:
**	IngresMigrationSqlGenerator   Convert provider agnostic migration
**	                              operations into Ingres specific SQL commands.
**
** History:
**	05-Feb-18 (thoda04)
**	    Created.
*/


using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Migrations.Model;
using System.Data.Entity.Migrations.Sql;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ingres.Client;

namespace Ingres
{
    /// <summary>
    /// This class is a service that allows EF Migrations to be used for
    /// the generation of SQL in creating and modifying database schemas
    /// by Code First. It is also used when databases are created
    /// using database initializers or the Database.Create method.
    /// </summary>
    class IngresMigrationSqlGenerator : MigrationSqlGenerator
    {
        private SqlGenerator _sqlGenerator;
        private List<MigrationStatement> Statements {get; set;}
        private String ProviderManifestToken { get; set; }
        internal const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.ffffff";


        private void Initialize(string providerManifestToken)
        {
            ProviderManifestToken = providerManifestToken;
            ProviderManifest = IngresProviderServices.Instance
                    .GetProviderManifest(providerManifestToken);
            _sqlGenerator = new SqlGenerator(
                ProviderManifest as IngresProviderManifest);

        }

        public override IEnumerable<MigrationStatement>
            Generate(IEnumerable<MigrationOperation> migrationOperations, string providerManifestToken)
        {
            CheckNotNull(migrationOperations,   "migrationOperations");
            CheckNotNull(providerManifestToken, "providerManifestToken");

            Initialize(providerManifestToken);

            Statements = new List<MigrationStatement>();

            Generate(migrationOperations);

            return Statements;
        }

        void AddStatement(StringBuilder sql, bool suppressTransaction=false)
        {
            AddStatement(sql.ToString(), suppressTransaction);
        }

        void AddStatement(String sql, bool suppressTransaction=false)
        {
            Statements.Add(
                new MigrationStatement
                {
                    Sql                 = sql,
                    SuppressTransaction = suppressTransaction,
                    BatchTerminator     = ";"
                });
        }


        protected virtual void Generate(IEnumerable<MigrationOperation> migrationOperations)
        {
            foreach (MigrationOperation operation in migrationOperations)
            {
                //   migrationOperations.Each<dynamic>(operation => Generate(operation));
                // operation can be
                if (operation is AddColumnOperation)
                    Generate(operation as AddColumnOperation);
                else if (operation is AlterColumnOperation)
                    Generate(operation as AlterColumnOperation);
                else if (operation is AlterTableOperation)
                    Generate(operation as AlterTableOperation);
                else if (operation is CreateTableOperation)
                    Generate(operation as CreateTableOperation);
                else if (operation is DropColumnOperation)
                    Generate(operation as DropColumnOperation);
                else if (operation is DropProcedureOperation)
                    Generate(operation as DropProcedureOperation);
                else if (operation is DropTableOperation)
                    Generate(operation as DropTableOperation);
                else if (operation is ForeignKeyOperation)
                    Generate(operation as ForeignKeyOperation);
                else if (operation is HistoryOperation)
                    Generate(operation as HistoryOperation);
                else if (operation is CreateIndexOperation)
                    Generate(operation as CreateIndexOperation);
                else if (operation is DropIndexOperation)
                    Generate(operation as DropIndexOperation);
                else if (operation is MoveProcedureOperation)
                    Generate(operation as MoveProcedureOperation);
                else if (operation is MoveTableOperation)
                    Generate(operation as MoveTableOperation);
                else if (operation is NotSupportedOperation)
                    Generate(operation as NotSupportedOperation);
                else if (operation is PrimaryKeyOperation)
                    Generate(operation as PrimaryKeyOperation);
                else if (operation is ProcedureOperation)
                    Generate(operation as ProcedureOperation);
                else if (operation is RenameColumnOperation)
                    Generate(operation as RenameColumnOperation);
                else if (operation is RenameIndexOperation)
                    Generate(operation as RenameIndexOperation);
                else if (operation is RenameProcedureOperation)
                    Generate(operation as RenameProcedureOperation);
                else if (operation is RenameTableOperation)
                    Generate(operation as RenameTableOperation);
                else if (operation is SqlOperation)
                    Generate(operation as SqlOperation);
                else if (operation is UpdateDatabaseOperation)
                    Generate(operation as UpdateDatabaseOperation);
                else throw new NotImplementedException(
                    "Unknown MigrationOperation '" +
                    operation.GetType().Name + "' in " + GetType().Name);
            }

        }

        private void Generate(AddColumnOperation addColumnOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(AlterColumnOperation alterColumnOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(AlterTableOperation alterTableOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(CreateTableOperation createTableOperation)
        {
            StringBuilder sql = new StringBuilder();
            String schemaName = GetSchemaName(createTableOperation.Name);
            String tableName  = GetTableName( createTableOperation.Name);
            int i = 0;     // count of columns processed so far

            sql.Append("CREATE TABLE ");
            sql.Append(QuotedIdentifier(schemaName, tableName));
            sql.Append(" (");
            foreach (ColumnModel column in createTableOperation.Columns)
            {
                AppendColumn(sql, column);
                if (++i < createTableOperation.Columns.Count)
                    sql.Append(", ");
            }

            if (createTableOperation.PrimaryKey != null)
                AppendPrimaryKeyConstraint(sql, createTableOperation);

            sql.Append(")");

            AddStatement(sql);
        }

        private void AppendPrimaryKeyConstraint(StringBuilder sql, CreateTableOperation createTableOperation)
        {
            int i = 0;

            sql.Append(", CONSTRAINT ");
            sql.Append(QuotedIdentifier(createTableOperation.PrimaryKey.Name));
            sql.Append(" PRIMARY KEY (");
            foreach (String key in createTableOperation.PrimaryKey.Columns)
            {
                sql.Append(QuotedIdentifier(key));
                if (++i < createTableOperation.PrimaryKey.Columns.Count)
                    sql.Append(", ");
            }
            sql.Append(")");
        }

        private void AppendColumn(StringBuilder sql, ColumnModel column)
        {
            sql.Append(QuotedIdentifier(column.Name));
            sql.Append(' ');
            AppendColumnType(sql, column);

            if (column.IsNullable != null &&
                column.IsNullable.Value == false)
                sql.Append(" NOT NULL");

            if (column.DefaultValue != null)
            {
                sql.Append(" DEFAULT ");
                AppendValue(sql, column.DefaultValue);
            }
            else if (!String.IsNullOrWhiteSpace(column.DefaultValueSql))
            {
                sql.Append(" DEFAULT ");
                sql.Append(column.DefaultValueSql);
            }
            else if (column.IsIdentity)
            {
                if (column.Type == PrimitiveTypeKind.Guid)
                    sql.Append(" WITH DEFAULT");
                else
                {
                    sql.Append(" GENERATED BY DEFAULT AS IDENTITY");
                }
            }
        }  // end AppendColumn()

        private void AppendValue(StringBuilder sql, object val)
        {
            if (val is String  ||
                val is Guid)
            {
                sql.Append(QuoteLiteral(val.ToString()));
            }
            else if (val is Boolean)
                sql.Append((Boolean)val ? "TRUE" : "FALSE");
            else if (val is Byte[])
            {
                sql.Append("X'");
                foreach (Byte b in (Byte[])val)
                {
                    sql.Append(b.ToString("X2",
                        CultureInfo.InvariantCulture));
                }
                sql.Append("'");
            }
            else if (val is DateTime)
            {
                sql.Append("TIMESTAMP '");
                sql.Append(((DateTime)val)
                    .ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                sql.Append("'");
            }
            else if (val is DateTimeOffset)
            {
                sql.Append("TIMESTAMP '");
                sql.Append(((DateTimeOffset)val)
                    .ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                sql.Append("'");
            }
            else if (val is TimeSpan)
            {
                sql.Append("INTERVAL '");
                sql.Append(((TimeSpan)val)
                    .ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                sql.Append("'");
            }
            else  // everything else (like numbers)
            {
                sql.Append(String.Format(
                    CultureInfo.InvariantCulture, "{0}", val));
            }
        }

        private void AppendColumnType(StringBuilder sql, ColumnModel column)
        {
            if (column.StoreType != null)  // if provider specified data type
            {
                sql.Append(column.StoreType);  // then use it
                return;
            }

            int columnMaxLengthValue = column.MaxLength.HasValue ? 
                Math.Min(column.MaxLength.Value, 2000) : 1;

            switch (column.Type)
            {
                case PrimitiveTypeKind.Binary:
                    if (column.IsFixedLength.HasValue &&
                        column.IsFixedLength.Value &&
                        column.MaxLength.HasValue)
                    {
                        sql.Append($"BYTE({columnMaxLengthValue})");      // fixed len
                    }
                    else if (column.MaxLength.HasValue)
                        sql.Append($"VARBINARY({columnMaxLengthValue})"); // not fixed len
                    else
                        sql.Append($"VARBINARY(30000)");
                    break;
                case PrimitiveTypeKind.Boolean:
                    sql.Append("BOOLEAN");
                    break;
                case PrimitiveTypeKind.Byte:
                    sql.Append("SMALLINT");
                    break;
                case PrimitiveTypeKind.DateTime:
                    sql.Append("TIMESTAMP WITHOUT TIME ZONE");
                    break;
                case PrimitiveTypeKind.DateTimeOffset:
                    sql.Append("TIMESTAMP WITH TIME ZONE");
                    break;
                case PrimitiveTypeKind.Decimal:
                    sql.Append("Decimal");
                    if (column.Precision == null &&
                        column.Scale     == null)
                            break;
                    sql.Append('(');
                    sql.Append(column.Precision ?? 5);
                    sql.Append(',');
                    sql.Append(column.Scale     ?? 0);
                    sql.Append(')');
                    break;
                case PrimitiveTypeKind.Double:
                    sql.Append("FLOAT8");
                    break;
                case PrimitiveTypeKind.Guid:
                    sql.Append("UUID");
                    break;
                case PrimitiveTypeKind.Int16:
                    sql.Append("INTEGER2");
                    break;
                case PrimitiveTypeKind.Int32:
                    sql.Append("INTEGER4");
                    break;
                case PrimitiveTypeKind.Int64:
                    sql.Append("INTEGER8");
                    break;
                case PrimitiveTypeKind.SByte:
                    sql.Append("INTEGER1");
                    break;
                case PrimitiveTypeKind.Single:
                    sql.Append("FLOAT4");
                    break;
                case PrimitiveTypeKind.String:
                    if (column.IsFixedLength.HasValue &&
                        column.IsFixedLength.Value &&
                        column.MaxLength.HasValue)
                    {
                        sql.Append($"NCHAR({columnMaxLengthValue})");    // fixed len
                    }
                    else if (column.MaxLength.HasValue)
                        sql.Append($"NVARCHAR({columnMaxLengthValue})"); // not fixed len
                    else
                        sql.Append($"NVARCHAR(16000)");
                    break;
                case PrimitiveTypeKind.Time:
                    sql.Append("TIME WITHOUT TIME ZONE");
                    break;
              //case PrimitiveTypeKind.Geography:
              //case PrimitiveTypeKind.GeographyCollection:
              //case PrimitiveTypeKind.GeographyLineString:
              //case PrimitiveTypeKind.GeographyMultiLineString:
              //case PrimitiveTypeKind.GeographyMultiPoint:
              //case PrimitiveTypeKind.GeographyMultiPolygon:
              //case PrimitiveTypeKind.GeographyPoint:
              //case PrimitiveTypeKind.GeographyPolygon:
              //case PrimitiveTypeKind.Geometry:
              //case PrimitiveTypeKind.GeometryCollection:
              //case PrimitiveTypeKind.GeometryLineString:
              //case PrimitiveTypeKind.GeometryMultiLineString:
              //case PrimitiveTypeKind.GeometryMultiPoint:
              //case PrimitiveTypeKind.GeometryMultiPolygon:
              //case PrimitiveTypeKind.GeometryPoint:
              //case PrimitiveTypeKind.GeometryPolygon:
                default:
                    throw new ArgumentException(
                        "Unsupported column type: " + column.Type.ToString());
            }
        }

        private void Generate(DropColumnOperation dropColumnOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(DropProcedureOperation dropProcedureOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(DropTableOperation dropTableOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(ForeignKeyOperation foreignKeyOperation)
        {
            return; // TODO // throw new NotImplementedException();
        }

        private void Generate(HistoryOperation historyOperation)
        {
            CheckNotNull(historyOperation, "historyOperation");

            String sql;

            foreach (DbModificationCommandTree commandTree
                in historyOperation.CommandTrees)
            {
                List<DbParameter> parameters = null;

                switch (commandTree.CommandTreeKind)
                {
                    case DbCommandTreeKind.Insert:
                        sql = DmlSqlGenerator.GenerateInsertSql(
                            (DbInsertCommandTree)commandTree,
                            _sqlGenerator,
                            out parameters,
                            createParameters: false);
                        AddStatement(sql);
                        break;
                    case DbCommandTreeKind.Delete:
                        sql = DmlSqlGenerator.GenerateDeleteSql(
                            (DbDeleteCommandTree)commandTree,
                            _sqlGenerator,
                            out parameters,
                            createParameters: false);
                        AddStatement(sql);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Generate(HistoryOperation) has unknown CommandTreeKind: " + 
                            commandTree.CommandTreeKind.ToString() + ".");
                }
                //DbCommand command = IngresProviderServices.Instance
                //    .CreateCommand(ProviderManifest, commandTree, true);
            }
            return;
        }

        private void Generate(CreateIndexOperation createIndexOperation)
        {
            return; // TODO //  throw new NotImplementedException();
        }

        private void Generate(DropIndexOperation dropIndexOperation)
        {
            return; // TODO //  throw new NotImplementedException();
        }

        private void Generate(MoveProcedureOperation moveProcedureOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(MoveTableOperation moveTableOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(NotSupportedOperation notSupportedOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(PrimaryKeyOperation primaryKeyOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(ProcedureOperation procedureOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(RenameColumnOperation renameColumnOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(RenameIndexOperation renameIndexOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(RenameProcedureOperation renameProcedureOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(RenameTableOperation renameTableOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(SqlOperation sqlOperation)
        {
            throw new NotImplementedException();
        }

        private void Generate(UpdateDatabaseOperation updateDatabaseOperation)
        {
            CheckNotNull(updateDatabaseOperation, "updateDatabaseOperation");

            if (updateDatabaseOperation.Migrations.Any() == false)
                return;

            // call Generate(IEnumerable<MigrationOperation>) recursively
            Generate(updateDatabaseOperation.Migrations
                as IEnumerable<MigrationOperation>);
        }


        private string GetSchemaName(string name)
        {
            string schemaName = null;

            int i = name.IndexOf('.'); // return if no schemaName
            if (i == -1)
                return null;
            schemaName = name.Remove(i);
            if (schemaName == "dbo")
                return null;
            return schemaName;
        }

        private string GetTableName(string name)
        {
            int i = name.IndexOf('.'); // return if no schemaName
            if (i == -1)
                return name;
            return (name.Remove(0, i + 1));
        }

        private string QuotedIdentifier(string schemaName, string tableName)
        {
            if (schemaName != null)
            {
                return QuotedIdentifier(schemaName) + "."
                     + QuotedIdentifier(tableName);
            }
            else
                return QuotedIdentifier(tableName);
        }

        private string QuotedIdentifier(string name)
        {
            return "\"" + name.Replace("\"", "\"\"") + "\"";  // myidentifier --> "myidentifier"
        }

        private string QuoteLiteral(string val)
        {
            return "\'" + val.Replace("\'", "\'\'") + "\'";  // literalValue --> 'literalValue'
        }

        private void CheckNotNull(object obj, String name)
        {
            if (obj == null)
                throw new ArgumentNullException(name);
        }
    }  // class

    internal static class IEnumerableExtensions
    {
       public static void Each<T>(this IEnumerable<T> list, Action<T> action)
        {
            foreach (T item in list)
            {
                action(item);
            }
        }


    }
}
