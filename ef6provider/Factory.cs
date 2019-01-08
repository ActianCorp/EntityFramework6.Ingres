/*
** Copyright (c) 2018 Actian Corporation. All Rights Reserved.
*/

/*
** Name: Factory.cs
**
** Description:
**	Implements the .NET Entity Framework DbProviderManifest class
**	using an XML definition that provides a symmetrical type
**	mapping to the Entity Data Model.
**	The manifest describes the types and functions supported
**	by the provider but in turms of the generic Entity Data Model (EDM).
**	The provider manifest must be loadable by tools at design time
**	without having to open a connection to the data
store.
**	Documentation for writing an EF Provider Manifest is at
**	https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/ef/provider-manifest-specification
**
**
** Classes:
**	IngresConnectionFactory
**
** History:
**	17-Jan-18 (thoda04)
**	    Created.
*/


using System.Data.Common;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Core.EntityClient;
using Ingres.Client;

namespace Ingres
{
    public sealed class IngresConnectionFactory : IDbConnectionFactory
    {
        public DbConnection CreateConnection(string nameOrConnectionString)
        {
            return new IngresConnection(nameOrConnectionString);
        }
    }

    public sealed class IngresProviderFactoryResolver : IDbProviderFactoryResolver
    {
        public DbProviderFactory ResolveProviderFactory(DbConnection connection)
        {
            if (connection is IngresConnection)
                return IngresFactory.Instance;
            if (connection is EntityConnection)
                return EntityProviderFactory.Instance;
            return null;
        }
    }

    public sealed class IngresProviderInvariantName : IProviderInvariantName
    {
        public string Name { get; } = "Ingres.Client";
    }
}  // namespace
