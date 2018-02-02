using System;
using System.Data.Entity;

namespace EntityFramework6.Ingres.Tests
{
    /// <summary>
    /// Use this class in LINQ queries to emit type manipulation SQL fragments.
    /// </summary>
    public static class IngresTypeFunctions
    {
        /// <summary>
        /// Emits an explicit cast for unknown types sent as strings to their correct postgresql type.
        /// </summary>
        [DbFunction("Npgsql", "cast")]
        public static string Cast(string unknownTypeValue, string postgresTypeName)
        {
            throw new NotSupportedException();
        }
    }
}