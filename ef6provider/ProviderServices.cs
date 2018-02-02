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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;
using System.Data.Entity.Core;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Data.Entity.Core.Metadata.Edm;

namespace Ingres
{
    public class IngresProviderServices : DbProviderServices
    {
        const string nameofProviderServices =
            nameof(IngresProviderServices);
        const string nameofCreateDbCommandDefinition =
            nameofProviderServices + ".CreateDbCommandDefinition";
        const string nameofGetDbProviderManifestToken =
            nameofProviderServices + ".GetDbProviderManifestToken";

        public  static  IngresProviderServices Instance
            { get; } = new IngresProviderServices();


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
        protected override DbCommandDefinition   CreateDbCommandDefinition(
            DbProviderManifest providerManifest,
            DbCommandTree      commandTree)
        {
            if (providerManifest == null)
                throw new ArgumentNullException(
                    nameofCreateDbCommandDefinition + ".providerManifest");
            if (commandTree      == null)
                throw new ArgumentNullException(
                    nameofCreateDbCommandDefinition + ".commandTree");

            throw new NotImplementedException();  // TODO
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
            if (connection == null)
                throw new ArgumentNullException(
                    nameofCreateDbCommandDefinition + ".connection");
            if (String.IsNullOrEmpty(connection.ConnectionString))
                throw new ArgumentNullException(
                    nameofCreateDbCommandDefinition + ".ConnectionString");

            return connection.ConnectionString;
        }

        protected override bool DbDatabaseExists(
            DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");
            if (connection == null)
                throw new ArgumentNullException("storeItemCollection");
            throw new ProviderIncompatibleException(
                "DatabaseExists is not supported by the Ingres EF Provider.");
        }

        protected override void DbCreateDatabase(
            DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");
            if (connection == null)
                throw new ArgumentNullException("storeItemCollection");
            throw new ProviderIncompatibleException(
                "CreateDatabase is not supported by the Ingres EF Provider.");
        }

        protected override void DbDeleteDatabase(
            DbConnection connection, int? commandTimeout, StoreItemCollection storeItemCollection)
        {
            if (connection == null)
                throw new ArgumentNullException("connection");
            if (connection == null)
                throw new ArgumentNullException("storeItemCollection");
            throw new ProviderIncompatibleException(
                "DeleteDatabase is not supported by the Ingres EF Provider.");
        }

    }  // class
}  // namespace
