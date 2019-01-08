/*
** Copyright (c) 2018 Actian Corporation. All Rights Reserved.
*/

/*
** Name: ProviderServices.cs
**
** Description:
**	Implements the .NET Entity Framework DbProviderServices class for Ingres.
**	This classes gives additional functions on top of the functionality
**	provided by the Ingres .NET Data Provider.
**	Documnetation for writing an EF Provider is at
**	https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/ef/writing-an-ef-data-provider
**
**
** Classes:
**	IngresProviderServices	Returns this EF provider's manifest.
**
** History:
**	17-Jan-18 (thoda04)
**	    Created.
*/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Core;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure.Interception;
using System.Data.Entity.Migrations.Sql;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Text.RegularExpressions;
using Ingres.Client;

namespace Ingres
{
    public class IngresProviderServices : DbProviderServices
    {
        public const string ProviderInvariantName = "Ingres.Client";

        private Nullable<bool> DbDatabaseExistsState = null;

        private const string nameofProviderServices =
            nameof(IngresProviderServices);
        private const string nameofCreateDbCommandDefinition =
            nameofProviderServices + ".CreateDbCommandDefinition";
        private const string nameofGetDbProviderManifestToken =
            nameofProviderServices + ".GetDbProviderManifestToken";

        private static readonly IngresProviderServices _providerInstance =
            new IngresProviderServices();

        public static  IngresProviderServices Instance
        {
            get { return _providerInstance; }
        }


    private IngresProviderServices()
        {
            AddDependencyResolver(
                new SingletonDependencyResolver<IDbConnectionFactory>(
                    new IngresConnectionFactory()));

            AddDependencyResolver(
                new SingletonDependencyResolver<IDbProviderFactoryResolver>(
                    new IngresProviderFactoryResolver()));

            AddDependencyResolver(
                new SingletonDependencyResolver<IProviderInvariantName>(
                    new IngresProviderInvariantName()));

            AddDependencyResolver(
                new SingletonDependencyResolver<Func<MigrationSqlGenerator>>(
                    () => new IngresMigrationSqlGenerator(), ProviderInvariantName));

            DbInterception.Add(
                new IngresDbCommandInterceptor()); // Intercept DbCommand execution
        }


        /// <summary>
        /// Get the Ingres DbProviderManifest using the version specified.
        /// </summary>
        /// <param name="version">
        /// Version as passed as the manifest token.</param>
        /// <returns></returns>
        protected override DbProviderManifest   GetDbProviderManifest(
            string version)
        {
            return new IngresProviderManifest(version);
        }

        /// <summary>
        /// Get the Ingres provider manifest token for
        /// the specified connection to the database.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        protected override string   GetDbProviderManifestToken(
            DbConnection connection)
        {
            CheckNotNull(connection, 
                nameofCreateDbCommandDefinition + ".connection");
            CheckNotNull(connection.ConnectionString, 
                nameofCreateDbCommandDefinition + ".ConnectionString");

            string token = "";

            IngresConnection connIngres = connection as IngresConnection;
            if (connIngres == null)
                return token;

            try
            {
                UsingIngresConnection(connIngres, conn =>
                {
                    Match match = null;
                    String version = conn.ServerVersion;
                    // ServerVersion is like "11.00.0000 II 11.0.0 (a64.win/100)"
                    match = Regex.Match(version, @"((II)|(VW)) (\w+)\.(\w+)");
                    if (match != null)
                        token = match.Groups[0].Value;
                    // return token like "II 11.0" or "VW 5.0"
                });
            }
            catch
            { }

            return token;
        }


        /// Return True if the provider can deduce that the
        /// database exists only based on the connection.
        protected override bool DbDatabaseExists(
            DbConnection           connection,
            int?                   commandTimeout,
            StoreItemCollection    storeItemCollection)
        {
            CheckNotNull(connection,          "connection");
            CheckNotNull(storeItemCollection, "storeItemCollection");

            // if database existence state is known then return it
            if (DbDatabaseExistsState != null)
                return (bool)DbDatabaseExistsState;

            // let's find the real database existence state
            if (connection.State == ConnectionState.Open)
            {
                DbDatabaseExistsState = true;
                return true;  // database is open so it exists
            }

            try
            {
                using (IngresConnection conn = new IngresConnection())
                {
                    conn.ConnectionString = connection.ConnectionString;
                    conn.Open();
                    conn.Close();
                    DbDatabaseExistsState = true;
                    return true;  // database opens OK so it exists
                }
            }
            catch
            {
                return false;     // database does not open
                                  // leave DbDatabaseExistsState as unknown
            }
        }  // DbDatabaseExists

        protected override void DbCreateDatabase(
            DbConnection           connection,
            int?                   commandTimeout,
            StoreItemCollection    storeItemCollection)
        {
            CheckNotNull(connection,          "connection");
            CheckNotNull(storeItemCollection, "storeItemCollection");

            // reset and recheck for a good database existence
            DbDatabaseExistsState = null;  // force existence state as unknown
            DbDatabaseExists(              // go find real existence state
                connection, commandTimeout, storeItemCollection);
        }

        protected override void DbDeleteDatabase(
            DbConnection           connection,
            int?                   commandTimeout,
            StoreItemCollection    storeItemCollection)
        {
            CheckNotNull(connection,          "connection");
            CheckNotNull(storeItemCollection, "storeItemCollection");

            DbDatabaseExistsState = false;  // behave as no database now
        }

        /// <summary>
        /// Create a command definition object using the
        /// Ingres provider manifest (metadata interface for all CLR types)
        /// and the command tree that represents queries, DML operations,
        /// and calls to functions/procedures.
        /// </summary>
        /// <param name="providerManifest"></param>
        /// <param name="commandTree"></param>
        /// <returns></returns>
        protected override DbCommandDefinition CreateDbCommandDefinition(
            DbProviderManifest providerManifest,
            DbCommandTree commandTree)
        {
            CheckNotNull(providerManifest, nameofCreateDbCommandDefinition + ".providerManifest");
            CheckNotNull(commandTree, nameofCreateDbCommandDefinition + ".commandTree");

            DbCommand prototype = CreateCommand(providerManifest, commandTree);
            DbCommandDefinition result = this.CreateCommandDefinition(prototype);
            return result;
        }

        internal DbCommand CreateCommand(
            DbProviderManifest manifest, DbCommandTree commandTree)
        {
            return CreateCommand(manifest, commandTree, true);
        }
        internal DbCommand CreateCommand(
            DbProviderManifest manifest,
            DbCommandTree      commandTree,
            bool               wantParameters)
        {
            CheckNotNull(manifest,    "manifest");
            CheckNotNull(commandTree, "commandTree");

            IngresCommand command = new IngresCommand();
            try
            {
                List<DbParameter> parameters;
                CommandType       commandType;

                command.CommandText = SqlGenerator.GenerateSql((IngresProviderManifest)manifest, commandTree, out parameters, out commandType);
                command.CommandType = commandType;

                if (wantParameters==false)
                    return command;

                // Get the function (if any) implemented by the command tree since this influences our interpretation of parameters
                EdmFunction function = null;
                if (commandTree is DbFunctionCommandTree)
                {
                    function = ((DbFunctionCommandTree)commandTree).EdmFunction;
                }

                // Now make sure we populate the command's parameters
                // from the Canonical Query Trees's (CQT) parameters:
                foreach (KeyValuePair<string, TypeUsage> queryParameter in commandTree.Parameters)
                {
                    IngresParameter parameter;

                    // Use the corresponding function parameter TypeUsage where available (currently, the SSDL facets and 
                    // type trump user-defined facets and type in the EntityCommand).
                    FunctionParameter functionParameter;
                    if (null != function && function.Parameters.TryGetValue(queryParameter.Key, false, out functionParameter))
                    {
                        parameter = CreateSqlParameter((IngresProviderManifest)manifest, functionParameter.Name, functionParameter.TypeUsage, functionParameter.Mode, DBNull.Value);
                    }
                    else
                    {
                        parameter = CreateSqlParameter((IngresProviderManifest)manifest, queryParameter.Key, queryParameter.Value, ParameterMode.In, DBNull.Value);
                    }

                    command.Parameters.Add(parameter);
                }

                // Now add parameters added as part of SQL gen (note: this feature is only safe for DML SQL gen which
                // does not support user parameters, where there is no risk of name collision)
                if (null != parameters && 0 < parameters.Count)
                {
                    if (!(commandTree is DbInsertCommandTree) &&
                        !(commandTree is DbUpdateCommandTree) &&
                        !(commandTree is DbDeleteCommandTree))
                    {
                        throw new InvalidOperationException("SqlGenParametersNotPermitted");
                    }

                    foreach (DbParameter parameter in parameters)
                    {
                        command.Parameters.Add(parameter);
                    }
                }

                return command;
            }
            catch
            {
                command.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Creates a IngresParameter given a name, type, and direction
        /// </summary>
        internal static IngresParameter CreateSqlParameter(IngresProviderManifest manifest, string name, TypeUsage type, ParameterMode mode, object value)
        {
            int? size;

            //
            // NOTE: Adjust the parameter type so that it will work with textual
            //       GUIDs.  (Commented out because Ingres does not support GUID.)
            //
            //if ((manifest != null) && !manifest._binaryGuid &&
            //    (MetadataHelpers.GetPrimitiveTypeKind(type) == PrimitiveTypeKind.Guid))
            //{
            //    type = TypeUsage.CreateStringTypeUsage(
            //        PrimitiveType.GetEdmPrimitiveType(PrimitiveTypeKind.String),
            //        false, true);
            //}

            IngresParameter result = new IngresParameter(name, value);

            // .Direction
            ParameterDirection direction = MetadataHelpers.ParameterModeToParameterDirection(mode);
            if (result.Direction != direction)
            {
                result.Direction = direction;
            }

            // .Size and .DbType
            // output parameters are handled differently (we need to ensure there is space for return
            // values where the user has not given a specific Size/MaxLength)
            bool isOutParam = mode != ParameterMode.In;
            DbType sqlDbType = GetSqlDbType(type, isOutParam, out size);
            if (result.DbType != sqlDbType)
            {
                PrimitiveTypeKind kind = MetadataHelpers.GetPrimitiveTypeKind(type);
                if (kind == PrimitiveTypeKind.Time ||           // get time subtype
                    kind == PrimitiveTypeKind.DateTimeOffset || // dateTimeOffset not supported
                    kind == PrimitiveTypeKind.Guid)
                {
                    result.IngresType = manifest.GetIngresType(kind);
                }
                else
                    result.DbType = sqlDbType;
            }

            // Note that we overwrite 'facet' parameters where either the value is different or
            // there is an output parameter.
            if (size.HasValue && (isOutParam || result.Size != size.Value))
            {
                result.Size = size.Value;
            }

            // .IsNullable
            bool isNullable = MetadataHelpers.IsNullable(type);
            if (isOutParam || isNullable != result.IsNullable)
            {
                result.IsNullable = isNullable;
            }

            return result;
        }


        /// <summary>
        /// Determines DbType for the given primitive type. Extracts facet
        /// information as well.
        /// </summary>
        private static DbType GetSqlDbType(TypeUsage type, bool isOutParam, out int? size)
        {
            // only supported for primitive type
            PrimitiveTypeKind primitiveTypeKind = MetadataHelpers.GetPrimitiveTypeKind(type);

            size = default(int?);

            switch (primitiveTypeKind)
            {
                case PrimitiveTypeKind.Binary:
                    // for output parameters, ensure there is space...
                    size = GetParameterSize(type, isOutParam);
                    return GetBinaryDbType(type);

                case PrimitiveTypeKind.Boolean:
                    return DbType.Boolean;

                case PrimitiveTypeKind.Byte:
                    return DbType.Int16;

                case PrimitiveTypeKind.Time:
                    return DbType.Time;

                case PrimitiveTypeKind.DateTimeOffset:
                    return DbType.DateTimeOffset;

                case PrimitiveTypeKind.DateTime:
                    return DbType.DateTime;

                case PrimitiveTypeKind.Decimal:
                    return DbType.Decimal;

                case PrimitiveTypeKind.Double:
                    return DbType.Double;

                case PrimitiveTypeKind.Guid:
                    return DbType.Guid;

                case PrimitiveTypeKind.Int16:
                    return DbType.Int16;

                case PrimitiveTypeKind.Int32:
                    return DbType.Int32;

                case PrimitiveTypeKind.Int64:
                    return DbType.Int64;

                case PrimitiveTypeKind.SByte:
                    return DbType.SByte;

                case PrimitiveTypeKind.Single:
                    return DbType.Single;

                case PrimitiveTypeKind.String:
                    size = GetParameterSize(type, isOutParam);
                    return GetStringDbType(type);

                default:
                    Debug.Fail("unknown PrimitiveTypeKind " + primitiveTypeKind);
                    return DbType.Object;
            }
        }

        /// <summary>
        /// Determines preferred value for SqlParameter.Size. Returns null
        /// where there is no preference.
        /// </summary>
        private static int? GetParameterSize(TypeUsage type, bool isOutParam)
        {
            int maxLength;
            if (MetadataHelpers.TryGetMaxLength(type, out maxLength))
            {
                // if the MaxLength facet has a specific value use it
                return maxLength;
            }
            else if (isOutParam)
            {
                // if the parameter is a return/out/inout parameter, ensure there 
                // is space for any value
                return int.MaxValue;
            }
            else
            {
                // no value
                return default(int?);
            }
        }

        /// <summary>
        /// Chooses the appropriate DbType for the given string type.
        /// </summary>
        private static DbType GetStringDbType(TypeUsage type)
        {
            Debug.Assert(type.EdmType.BuiltInTypeKind == BuiltInTypeKind.PrimitiveType &&
              PrimitiveTypeKind.String == ((PrimitiveType)type.EdmType).PrimitiveTypeKind, "only valid for string type");

            DbType dbType;

            // Specific type depends on whether the string is a unicode string and whether it is a fixed length string.
            // By default, assume widest type (unicode) and most common type (variable length)
            bool unicode;
            bool fixedLength;
            if (!MetadataHelpers.TryGetIsFixedLength(type, out fixedLength))
            {
                fixedLength = false;
            }

            if (!MetadataHelpers.TryGetIsUnicode(type, out unicode))
            {
                unicode = true;
            }

            if (fixedLength)
            {
                dbType = (unicode ? DbType.StringFixedLength : DbType.AnsiStringFixedLength);
            }
            else
            {
                dbType = (unicode ? DbType.String : DbType.AnsiString);
            }
            return dbType;
        }

        /// <summary>
        /// Chooses the appropriate DbType for the given binary type.
        /// </summary>
        private static DbType GetBinaryDbType(TypeUsage type)
        {
            Debug.Assert(type.EdmType.BuiltInTypeKind == BuiltInTypeKind.PrimitiveType &&
              PrimitiveTypeKind.Binary == ((PrimitiveType)type.EdmType).PrimitiveTypeKind, "only valid for binary type");

            // Specific type depends on whether the binary value is fixed length. By default, assume variable length.
            bool fixedLength;
            if (!MetadataHelpers.TryGetIsFixedLength(type, out fixedLength))
            {
                fixedLength = false;
            }

            return DbType.Binary;
            //            return fixedLength ? DbType.Binary : DbType.VarBinary;
        }

        static void UsingIngresConnection(IngresConnection connection, Action<DbConnection> action)
        {
            using (IngresConnection conn = (IngresConnection)connection.Clone())
            {
                conn.Open();
                action(conn);
                // using's Dispose will close conn
            } // end using clone of IngresConnection
        }

    private void CheckNotNull(object obj, String name)
        {
            if (obj == null)
                throw new ArgumentNullException(name);
        }

    }  // class
}  // namespace
