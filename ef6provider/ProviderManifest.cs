/*
** Copyright (c) 2017 Actian Corporation. All Rights Reserved.
*/

/*
** Name: ProviderManifest.cs
**
** Description:
**	Implements the .NET Entity Framework DbProviderManifext class
**	using an XML definition that provides a symmetrical type
**	mapping to the Entity Data Model.
**	The manifest describes the types and functions supported
**	by the provider but in turms of the generic Entity Data Model (EDM).
**	The provider manifest must be loadable by tools at design time
**	without having to open a connection to the data store.
**	Documentation for writing an EF Provider Manifest is at
**	https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/ef/provider-manifest-specification
**
**
** Classes:
**	IngresProviderManifest
**
** History:
**	14-Dec-17 (thoda04)
**	    Created.
*/


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Metadata.Edm;
using System.Xml;
using System.IO;
using System.Reflection;
using Ingres.Client;

namespace Ingres
{
    /// <summary>
    /// The Manifest specifies how Entity Data Model data types, mappings, and queries
    /// interact with Ingres. The parameter and return type of the functions supported
    /// by Ingres are described in EDM terms. The description is specified in XML files
    /// of a Provider Manifest (.xml), a Store Schema Definition (.ssdl), and a
    /// Store Schema Mapping(.msl).
    /// </summary>
    internal sealed class IngresProviderManifest : DbXmlEnabledProviderManifest
    {
        internal DateTimeKind      _dateTimeKind = DateTimeKind.Unspecified;
        internal string            _dateTimeFormatString = null;
        internal bool              _binaryGuid = false;

        internal const DateTimeKind DefaultDateTimeKind = DateTimeKind.Unspecified;
        internal const string       DefaultDateTimeFormatString = null;

        public IngresProviderManifest(string versionToken) :
            base(GetProviderManifestXmlReader())
        {
        }

        protected override XmlReader GetDbInformation(string informationType)
        {
            switch(informationType)
            {
                case  DbProviderManifest.StoreSchemaDefinition:
                    return GetStoreSchemaDefinition();
                case  DbProviderManifest.StoreSchemaMapping:
                    return GetStoreSchemaMapping();
                case  DbProviderManifest.ConceptualSchemaDefinition:
                    return GetConceptualSchemaDefinition();
                default:
                    throw new ArgumentOutOfRangeException(nameof(informationType));
            }
        }

        internal IngresType GetIngresType(PrimitiveTypeKind primitiveTypeKind)
        {
            switch (primitiveTypeKind)
            {
                case PrimitiveTypeKind.Binary:
                     return IngresType.Binary;
                case PrimitiveTypeKind.Boolean:
                     return IngresType.Boolean;
                case PrimitiveTypeKind.Byte:
                     return IngresType.SmallInt;
                case PrimitiveTypeKind.DateTime:
                     return IngresType.DateTime;
//              case PrimitiveTypeKind.DateTimeOffset:
//                   return IngresType.DateTime;
                case PrimitiveTypeKind.Decimal:
                     return IngresType.Decimal;
                case PrimitiveTypeKind.Double:
                     return IngresType.Double;
//              case PrimitiveTypeKind.Guid:
//                   return IngresType.Guid;
                case PrimitiveTypeKind.Int16:
                     return IngresType.SmallInt;
                case PrimitiveTypeKind.Int32:
                     return IngresType.Int;
                case PrimitiveTypeKind.Int64:
                     return IngresType.BigInt;
                case PrimitiveTypeKind.SByte:
                     return IngresType.TinyInt;
                case PrimitiveTypeKind.Single:
                     return IngresType.Real;
                case PrimitiveTypeKind.String:
                     return IngresType.NVarChar;
                case PrimitiveTypeKind.Time:
                     return IngresType.IntervalDayToSecond;
                default:
                    throw new ArgumentException(
                        String.Format(
                            "Ingres Entity Framework provider does not support the type '{0}'.",
                                 primitiveTypeKind));
            }  // end switch (primitiveTypeKind)
        }

        /// <summary>
        /// This method takes a type and a set of facets and returns the best mapped equivalent type 
        /// in EDM.
        /// </summary>
        /// <param name="storeType">A TypeUsage encapsulating a store type and a set of facets</param>
        /// <returns>A TypeUsage encapsulating an EDM type and a set of facets</returns>
        public override TypeUsage GetEdmType(TypeUsage storeType)
        {
            if (storeType == null)
            {
                throw new ArgumentNullException("storeType");
            }

            string storeTypeName = storeType.EdmType.Name.ToLowerInvariant();
            if (!base.StoreTypeNameToEdmPrimitiveType.ContainsKey(storeTypeName))
            {
                throw new ArgumentException(String.Format("Ingres does not support the type '{0}'.", storeTypeName));
            }

            PrimitiveType edmPrimitiveType = base.StoreTypeNameToEdmPrimitiveType[storeTypeName];

            switch (storeTypeName)
            {
                case "tinyint":
                case "smallint":
                case "integer":
                case "bigint":
                case "integer1":
                case "integer2":
                case "integer4":
                case "integer8":
                case "int1":
                case "int2":
                case "int4":
                case "int8":
                case "bit":
                case "bool":
                case "uniqueidentifier":
                case "int":
                case "float":
                case "real":
                case "float4":
                case "float8":
                case "double":
                    return TypeUsage.CreateDefaultTypeUsage(edmPrimitiveType);
                case "decimal":
                    return CreateDecimalTypeUsage(edmPrimitiveType,
                        TypeHelpers.GetPrecision(storeType), TypeHelpers.GetScale(storeType));
                case "money":
                    return TypeUsage.CreateDecimalTypeUsage(edmPrimitiveType, 14, 2);
                case "char":
                    return CreateStringTypeUsage(edmPrimitiveType, false, true,
                        TypeHelpers.GetMaxLength(storeType));
                case "varchar":
                    return CreateStringTypeUsage(edmPrimitiveType, false, false,
                        TypeHelpers.GetMaxLength(storeType));
                case "long varchar":
                case "clob":
                    return CreateStringTypeUsage(edmPrimitiveType, false, false);
                case "nchar":
                    return CreateStringTypeUsage(edmPrimitiveType, true, true,
                        TypeHelpers.GetMaxLength(storeType));
                case "nvarchar":
                    return CreateStringTypeUsage(edmPrimitiveType, true, false,
                        TypeHelpers.GetMaxLength(storeType));
                case "long nvarchar":
                case "nclob":
                    return CreateStringTypeUsage(edmPrimitiveType, true, false);
                case "byte":
                    return CreateBinaryTypeUsage(edmPrimitiveType, true, TypeHelpers.GetMaxLength(storeType));
                case "byte varying":
                    return CreateBinaryTypeUsage(edmPrimitiveType, false, TypeHelpers.GetMaxLength(storeType));
                case "long byte":
                case "blob":
                    return CreateBinaryTypeUsage(edmPrimitiveType, false);
                case "datetime":
                case "date":
                case "ingresdate":
                case "ansidate":
                    return TypeUsage.CreateDateTimeTypeUsage(edmPrimitiveType, null);
                default:
                    throw new NotSupportedException(String.Format("Ingres does not support the type '{0}'.", storeTypeName));
            }  // end switch (storeTypeName)
        }

        private TypeUsage CreateDecimalTypeUsage(PrimitiveType primitiveType, byte? precision = null, byte? scale = null)
        {
            if ((precision != null) && (scale != null))
            {
                return TypeUsage.CreateDecimalTypeUsage(primitiveType, precision.Value, scale.Value);
            }
            return TypeUsage.CreateDecimalTypeUsage(primitiveType);
        }

        private TypeUsage CreateStringTypeUsage(PrimitiveType edmPrimitiveType, bool isUnicode, bool isFixedLen, int? maxLength = null)
        {
            if (maxLength == null)
            {
                return TypeUsage.CreateStringTypeUsage(edmPrimitiveType, isUnicode, isFixedLen);
            }
            return TypeUsage.CreateStringTypeUsage(edmPrimitiveType, isUnicode, isFixedLen, maxLength.Value);
        }

        private TypeUsage CreateBinaryTypeUsage(PrimitiveType edmPrimitiveType, bool isFixedLen, int? maxLength = null)
        {
            if (maxLength == null)
            {
                return TypeUsage.CreateBinaryTypeUsage(edmPrimitiveType, isFixedLen);
            }
            return TypeUsage.CreateBinaryTypeUsage(edmPrimitiveType, isFixedLen, maxLength.Value);
        }

        /// <summary>
        /// This method takes a type and a set of facets and returns the best mapped equivalent type 
        /// </summary>
        /// <param name="edmType">A TypeUsage encapsulating an EDM type and a set of facets</param>
        /// <returns>A TypeUsage encapsulating a store type and a set of facets</returns>
        public override TypeUsage GetStoreType(TypeUsage edmType)
        {
            if (edmType == null)
                throw new ArgumentNullException("edmType");

            PrimitiveType primitiveType = edmType.EdmType as PrimitiveType;
            if (primitiveType == null)
                throw new ArgumentException(String.Format("Ingres does not support the type '{0}'.", edmType));

            ReadOnlyMetadataCollection<Facet> facets = edmType.Facets;

            switch (primitiveType.PrimitiveTypeKind)
            {
                case PrimitiveTypeKind.Boolean:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["boolean"]);
                case PrimitiveTypeKind.Byte:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["smallint"]);
                case PrimitiveTypeKind.Int16:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["smallint"]);
                case PrimitiveTypeKind.Int32:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["int"]);
                case PrimitiveTypeKind.Int64:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["integer"]);
                case PrimitiveTypeKind.Guid:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["uniqueidentifier"]);
                case PrimitiveTypeKind.Double:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["float"]);
                case PrimitiveTypeKind.Single:
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["real"]);
                case PrimitiveTypeKind.Decimal: // decimal, numeric, smallmoney, money
                    {
                        byte precision = 18;
                        byte scale     = 0;
                        TypeHelpers.TryGetPrecision(edmType, out precision);
                        TypeHelpers.TryGetScale(edmType, out scale);
                        return TypeUsage.CreateDecimalTypeUsage(StoreTypeNameToStorePrimitiveType["decimal"], precision, scale);
                    }
                case PrimitiveTypeKind.Binary: // binary, varbinary, varbinary(max), image, timestamp, rowversion
                    {
                        bool isFixedLength = null != facets["FixedLength"].Value && (bool)facets["FixedLength"].Value;
                        Facet f = facets["MaxLength"];

                        bool isMaxLength = f.IsUnbounded || null == f.Value || (int)f.Value > Int32.MaxValue;
                        int maxLength = !isMaxLength ? (int)f.Value : Int32.MinValue;

                        TypeUsage tu;
                        if (isFixedLength)
                            tu = TypeUsage.CreateBinaryTypeUsage(StoreTypeNameToStorePrimitiveType["blob"], true, maxLength);
                        else
                        {
                            if (isMaxLength)
                                tu = TypeUsage.CreateBinaryTypeUsage(StoreTypeNameToStorePrimitiveType["blob"], false);
                            else
                                tu = TypeUsage.CreateBinaryTypeUsage(StoreTypeNameToStorePrimitiveType["blob"], false, maxLength);
                        }
                        return tu;
                    }
                case PrimitiveTypeKind.String: // char, nchar, varchar, nvarchar, varchar(max), nvarchar(max), ntext, text
                    {
                        bool isUnicode = null == facets["Unicode"].Value || (bool)facets["Unicode"].Value;
                        bool isFixedLength = null != facets["FixedLength"].Value && (bool)facets["FixedLength"].Value;
                        Facet f = facets["MaxLength"];
                        // maxlen is true if facet value is unbounded, the value is bigger than the limited string sizes *or* the facet
                        // value is null. this is needed since functions still have maxlength facet value as null
                        bool isMaxLength = f.IsUnbounded || null == f.Value || (int)f.Value > (isUnicode ? Int32.MaxValue : Int32.MaxValue);
                        int maxLength = !isMaxLength ? (int)f.Value : Int32.MinValue;

                        TypeUsage tu;

                        if (isUnicode)
                        {
                            if (isFixedLength)
                                tu = TypeUsage.CreateStringTypeUsage(StoreTypeNameToStorePrimitiveType["nchar"], true, true, maxLength);
                            else
                            {
                                if (isMaxLength)
                                    tu = TypeUsage.CreateStringTypeUsage(StoreTypeNameToStorePrimitiveType["nvarchar"], true, false);
                                else
                                    tu = TypeUsage.CreateStringTypeUsage(StoreTypeNameToStorePrimitiveType["nvarchar"], true, false, maxLength);
                            }
                        }
                        else
                        {
                            if (isFixedLength)
                                tu = TypeUsage.CreateStringTypeUsage(StoreTypeNameToStorePrimitiveType["char"], false, true, maxLength);
                            else
                            {
                                if (isMaxLength)
                                    tu = TypeUsage.CreateStringTypeUsage(StoreTypeNameToStorePrimitiveType["varchar"], false, false);
                                else
                                    tu = TypeUsage.CreateStringTypeUsage(StoreTypeNameToStorePrimitiveType["varchar"], false, false, maxLength);
                            }
                        }
                        return tu;
                    }
                case PrimitiveTypeKind.DateTime: // datetime, smalldatetime
                    return TypeUsage.CreateDefaultTypeUsage(StoreTypeNameToStorePrimitiveType["datetime"]);
                default:
                    throw new NotSupportedException(String.Format("There is no store type corresponding to the EDM type '{0}' of primitive type '{1}'.", edmType, primitiveType.PrimitiveTypeKind));
            }
        }

        public string CastParameterName(TypeUsage edmType, string name)
        {
            if (edmType == null)
                throw new ArgumentNullException("edmType");

            PrimitiveType primitiveType = edmType.EdmType as PrimitiveType;
            if (primitiveType == null)
                throw new ArgumentException(String.Format("Ingres does not support the type '{0}'.", edmType));
            if (edmType == null)
                throw new ArgumentNullException("edmType");

            StringBuilder sb = new StringBuilder();

            ReadOnlyMetadataCollection<Facet> facets = edmType.Facets;

            switch (primitiveType.PrimitiveTypeKind)
            {
                case PrimitiveTypeKind.Boolean:
                    sb.Append("boolean("+name);
                    break;
                case PrimitiveTypeKind.SByte:
                    sb.Append("int1(" + name);
                    break;
                case PrimitiveTypeKind.Byte:
                    sb.Append("int2(" + name);
                    break;
                case PrimitiveTypeKind.Int16:
                    sb.Append("int2(" + name);
                    break;
                case PrimitiveTypeKind.Int32:
                    sb.Append("int4(" + name);
                    break;
                case PrimitiveTypeKind.Int64:
                    sb.Append("int8(" + name);
                    break;
                case PrimitiveTypeKind.Guid:
                    sb.Append("varchar(" + name);
                    break;
                case PrimitiveTypeKind.Double:
                    sb.Append("float8(" + name);
                    break;
                case PrimitiveTypeKind.Single:
                    sb.Append("float4(" + name);
                    break;
                case PrimitiveTypeKind.Decimal: // decimal, numeric, smallmoney, money
                    {
                        byte precision;
                        if (!TypeHelpers.TryGetPrecision(edmType, out precision))
                        {
                            precision = 18;
                        }

                        byte scale;
                        if (!TypeHelpers.TryGetScale(edmType, out scale))
                        {
                            scale = 0;
                        }

                        sb.Append(Format("decimal({0}, {1}, {2})", name, precision, scale));
                        break;
                    }
                case PrimitiveTypeKind.Binary: // binary, varbinary, varbinary(max), image, timestamp, rowversion
                    {
                        bool isFixedLength = (null != facets["FixedLength"].Value)  &&
                            (bool)facets["FixedLength"].Value;
                        Facet f = facets["MaxLength"];

                        bool isMaxLength = f.IsUnbounded ||
                            null == f.Value ||
                            (int)f.Value > Int32.MaxValue;
                        int maxLength = !isMaxLength ? (int)f.Value : Int32.MinValue;

                        if (isFixedLength)
                        {
                            sb.Append("binary(" + name +", ");
                            sb.Append(maxLength.ToString());
                        }
                        else
                        {
                            sb.Append("binary(" + name);
                        }
                        break;
                    }
                case PrimitiveTypeKind.String: // char, nchar, varchar, nvarchar, varchar(max), nvarchar(max), ntext, text
                    {
                        bool isUnicode = (null == facets["Unicode"].Value)  ||
                            (bool)facets["Unicode"].Value;
                        bool isFixedLength = (null != facets["FixedLength"].Value)  &&
                            (bool)facets["FixedLength"].Value;
                        Facet f = facets["MaxLength"];
                        // maxlen is true if facet value is unbounded, the value is bigger than the limited string sizes *or* the facet
                        // value is null. this is needed since functions still have maxlength facet value as null
                        bool isMaxLength = f.IsUnbounded  ||
                            (null == f.Value)  ||
                            (int)f.Value > (isUnicode ? Int32.MaxValue : Int32.MaxValue);
                        int maxLength = !isMaxLength ? (int)f.Value : Int32.MinValue;

                        if (isUnicode)
                        {
                            if (isFixedLength)
                            {
                                sb.Append("nchar(" + name +", ");
                                sb.Append(maxLength.ToString());
                            }
                            else
                            {
                                sb.Append("nvarchar(" + name);
                            }
                        }
                        else
                        {
                            if (isFixedLength)
                            {
                                sb.Append("char(" + name + ", ");
                                sb.Append(maxLength.ToString());
                            }
                            else
                            {
                                sb.Append("varchar(" + name);
                            }
                        }
                        break;
                    }
                case PrimitiveTypeKind.DateTime: // datetime
                    {
                        sb.Append("timestamp(" + name);
                        break;
                    }
                case PrimitiveTypeKind.Time: // time
                    {
                        sb.Append("interval_dtos(" + name);
                        break;
                    }
                default:
                    {
                        break;
                    }

            }  // end switch

            if (sb.Length > 0)
            {
                sb.Append(")");
                return sb.ToString();
            }
            else
                return (name);
        }

        public static string Format(string format, params object[] arguments)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture, format, arguments);
        }



        private static XmlReader GetProviderManifestXmlReader()
        {
            return GetXmlReader("Ingres.ProviderManifest.xml");
        }

        /// <summary>
        /// Return the Store Schema Definition Language XML-based
        /// specification that is used to define the entity types,
        /// associations, entity containers, entity sets, and
        /// association sets of a storage model that frequently
        /// corresponds to a database schema.
        /// </summary>
        /// <returns></returns>
        private static XmlReader GetStoreSchemaDefinition()
        {
            return GetXmlReader("Ingres.StoreSchemaDefinition.ssdl");
        }

        /// <summary>
        /// Return the Mapping Specification Language XML-based
        /// specification that is used to map items defined in
        /// a conceptual model to items in a storage model.
        /// </summary>
        /// <returns></returns>
        private static XmlReader GetStoreSchemaMapping()
        {
            return GetXmlReader("Ingres.StoreSchemaMapping.msl");
        }

        private static XmlReader GetConceptualSchemaDefinition()
        {
            return null;
        }

        internal static XmlReader GetXmlReader(string resourceName)
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            Stream stream = executingAssembly.GetManifestResourceStream(resourceName);
            return XmlReader.Create(stream);
        }



    } // class  IngresProviderManifest

        internal static class TypeHelpers
        {
            public static T GetValue<T>(this Facet facet, T defaultValue = default(T))
            {
                if (facet.Value != null)
                {
                    return (T)facet.Value;
                }
                return defaultValue;
            }

            public static byte GetPrecision(TypeUsage tu, byte defaultPrecision)
            {
                return GetPrecision(tu, (byte?)defaultPrecision).Value;
            }

            public static byte? GetPrecision(TypeUsage tu, byte? defaultPrecision = null)
            {
                byte precision;
                if (TryGetPrecision(tu, out precision))
                {
                    return precision;
                }
                return defaultPrecision;
            }

            public static bool TryGetPrecision(TypeUsage tu, out byte precision)
            {
                Facet f;

                precision = 0;
                if (tu.Facets.TryGetValue("Precision", false, out f))
                {
                    if (!f.IsUnbounded && f.Value != null)
                    {
                        precision = (byte)f.Value;
                        return true;
                    }
                }
                return false;
            }

        public static int? GetMaxLength(TypeUsage tu)
        {
            int maxLength;
            if (TryGetMaxLength(tu, out maxLength))
            {
                return maxLength;
            }
            return default(int?);
        }

        public static bool TryGetMaxLength(TypeUsage tu, out int maxLength)
            {
                Facet f;

                maxLength = 0;
                if (tu.Facets.TryGetValue("MaxLength", false, out f))
                {
                    if (!f.IsUnbounded && f.Value != null)
                    {
                        maxLength = (int)f.Value;
                        return true;
                    }
                }
                return false;
            }

        public static byte GetScale(TypeUsage tu, byte defaultScale)
        {
            return GetScale(tu, (byte?)defaultScale).Value;
        }

        public static byte? GetScale(TypeUsage tu, byte? defaultScale = null)
        {
            byte scale;
            if (TryGetScale(tu, out scale))
            {
                return scale;
            }
            return defaultScale;
        }

        public static bool TryGetScale(TypeUsage tu, out byte scale)
            {
                Facet f;

                scale = 0;
                if (tu.Facets.TryGetValue("Scale", false, out f))
                {
                    if (!f.IsUnbounded && f.Value != null)
                    {
                        scale = (byte)f.Value;
                        return true;
                    }
                }
                return false;
            }
        }  // class TypeHelpers
}
