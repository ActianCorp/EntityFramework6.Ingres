/*
** Copyright (c) 2018 Actian Corporation. All Rights Reserved.
*/

/*
** Name: CommandInterceptor.cs
**
** Description:
**	Implements the .NET Entity Framework DbCommandInterceptor class for Ingres.
**	This classes alllows interception of the DbCommand execution just before
**	the command is sent to the Ingres .NET Data Provider. This "hook" allows
**	us to modify the CommandText in the ...Executing methods and modify the
**	results in the ...Executed methods. This interception allows processing
**	of batch SQL statements sent by EF. These batches are broken apart
**	into single statements and sent to the Ingres .NET DP.
**	Batch SQL execution is thus simulated.
**
**
** Classes:
**	IngresDbCommandInterceptor	Trap and handle batch SQL execution.
**
** History:
**	 7-Mar-18 (thoda04)
**	    Created.
*/


using System;
using System.Collections.Specialized;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using Ingres.Client;
using System.Collections;

namespace Ingres
{
    public class IngresDbCommandInterceptor : DbCommandInterceptor
    {
        private StringCollection statements;

        public override void NonQueryExecuting(
            DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            base.NonQueryExecuting(command, interceptionContext);
        }

        public override void NonQueryExecuted(
            DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
            base.NonQueryExecuted(command, interceptionContext);
        }

        public override void ReaderExecuting(
            DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            int i = 0;
            int recordsAffected = 0;

            statements = GetBatchStatements(command.CommandText);
            if (statements != null)
            {
                foreach (String statement in statements)
                {
                    if (++i >= statements.Count)  // break if last
                    {   // probably a SELECT command
                        command.CommandText = statement
                            .Replace("__ingres_@@ROWCOUNT",
                                     recordsAffected.ToString());;
                        break;
                    }

                    using (IngresCommand command2 = (command as IngresCommand).Clone())
                    {   // probably an INSERT command
                        command2.CommandText = statement;

                        // INSERT/UPDATE/DELETE using the command.Parameters
                        recordsAffected = command2.ExecuteNonQuery();

                        // clear parms on the SELECT since they were just consumed
                        command.Parameters.Clear();
                    }
                }  // end foreach (String statement in statements)
            }

            base.ReaderExecuting(command, interceptionContext);  // SELECT
        }

        public override void ReaderExecuted(
            DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            if (interceptionContext.Result != null &&
                interceptionContext.Result is IngresDataReader)
            {  // intercept the IngresDataReader
                interceptionContext.Result =
                        new IngresEFDbDataReader(interceptionContext.Result);

                // Build a dictionary for <select_column_name_list>
                // to map the lower-case column names returned by Ingres
                // to the original camel-case names expected by EF.
                // We care because EF is searching on the camel-case names,
                // not the lower-case names returned by the DBMS.
                ((IngresEFDbDataReader)interceptionContext.Result)
                    .BuildNameDictionary(command.CommandText);
            }
            base.ReaderExecuted(command, interceptionContext);
        }

        public override void ScalarExecuting(
            DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            base.ScalarExecuting(command, interceptionContext);
        }
        public override void ScalarExecuted(
            DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            base.ScalarExecuted(command, interceptionContext);
        }

        /// <summary>
        /// Break the batch statement into individual statements.
        /// </summary>
        /// <param name="commandText">Batch statements delimieted by ';'.</param>
        /// <returns>A StringCollection of individual statements.</returns>
        private StringCollection GetBatchStatements(string commandText)
        {
            char[] whiteSpace = {' ', '\t', '\r', '\n'};
            if (commandText        == null ||
                commandText.Length == 0    ||
                commandText.TrimEnd(whiteSpace).EndsWith(";") == false)
                    return null;  // not a batched set of statements

            StringCollection statements = new StringCollection();
            StringBuilder    sb         = new StringBuilder();
            bool             inQuotes   = false;
            char             quoteChar  = '\0';

            foreach (char c in commandText)
            {
                sb.Append(c);  // accumulate all chars up to ';'

                if (c == '\'' ||  c == '\"')
                {
                    if(!inQuotes)  // starting quoted literal
                    {
                        quoteChar = c;   // remember starting quote char
                        inQuotes = true; // we are inside a quoted string
                    }
                    else           // ending quoted literal?
                    {
                        if (c == quoteChar) // end quote == start quote?
                            inQuotes = false;  // not inside quoted string
                        // else stay in inQuotes mode
                    }
                    continue;  // move on past the quote
                }

                if (inQuotes)
                    continue;

                if (c == ';')
                {
                    if (sb.Length > 0)
                        sb.Length--;  // remove the ';' from the statement

                    statements.Add(sb.ToString());  // add the statement
                    sb.Clear();                     // clear for next statement
                }  // end if (';')
            }  // end foreach (char c in commandText)
            return statements;
        }  // end GetBatchStatements()

    }  // end class IngresDbCommandInterceptor

    /// <summary>
    /// Wrapper for IngresDbDataReader so that some methods can be
    /// intercepted on the Result of the ReaderExecuted() method.
    /// GetName(i) is intercepted to return the name of the SELECT
    /// column in the application's case rather than the lower
    /// case of the identifier that Ingres shifts down to.
    /// E.g. Return "BlogId" instead of "blogid".
    /// EF expects casting is relaxed on Getxxx() methods.
    /// </summary>
    public class IngresEFDbDataReader : DbDataReader
    {
        DbDataReader rdr;
        /// <summary>
        /// Dictionary to map the lower-case select
        /// column name returned by Ingres back to the
        /// original camel-case name used by EF.
        /// </summary>
        StringDictionary nameDictionary;
        public bool TrimChars { get; set; }


        public IngresEFDbDataReader(DbDataReader rdr)
        {
            this.rdr = rdr;
            nameDictionary = new StringDictionary();
            TrimChars = false;
        }

        public override object this[int ordinal] => rdr[ordinal];

        public override object this[string name] => rdr[name];

        public override int Depth => rdr.Depth;

        public override int FieldCount => rdr.FieldCount;

        public override bool HasRows => rdr.HasRows;

        public override bool IsClosed => rdr.IsClosed;

        public override int RecordsAffected => rdr.RecordsAffected;

        public override void Close()
        {
            rdr.Close();
        }

        public override bool GetBoolean(int ordinal)
        {
            if (GetInt64(ordinal) == 0)
                return false;
            else
                return true;
        }

        public override byte GetByte(int ordinal)
        {
            return rdr.GetByte(ordinal);
        }

        public override long GetBytes(
            int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            return rdr.GetBytes(
                ordinal, dataOffset, buffer, bufferOffset, length);
        }

        public override char GetChar(int ordinal)
        {
            throw new NotImplementedException();
        }

        public override long GetChars(
            int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            return rdr.GetChars(
                ordinal, dataOffset, buffer, bufferOffset, length);
        }

        public override string GetDataTypeName(int ordinal)
        {
            return rdr.GetDataTypeName(ordinal);
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return rdr.GetDateTime(ordinal);
        }

        public override decimal GetDecimal(int ordinal)
        {
            return rdr.GetDecimal(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            return Convert.ToDouble(rdr.GetValue(ordinal));
        }

        public override IEnumerator GetEnumerator()
        {
           return rdr.GetEnumerator();
        }

        public override Type GetFieldType(int ordinal)
        {
            return rdr.GetFieldType(ordinal);
        }

        public override float GetFloat(int ordinal)
        {
            return Convert.ToSingle(rdr.GetValue(ordinal));
        }

        public override Guid GetGuid(int ordinal)
        {
            Guid guid;
            Guid.TryParse(rdr.GetString(ordinal), out guid);
            return guid;
        }

        public override short GetInt16(int ordinal)
        {
            return Convert.ToInt16(rdr.GetValue(ordinal));
        }

        public override int GetInt32(int ordinal)
        {
            return Convert.ToInt32(rdr.GetValue(ordinal));
        }

        public override long GetInt64(int ordinal)
        {
            return Convert.ToInt64(rdr.GetValue(ordinal));
        }

        public override string GetName(int ordinal)
        {
            String name = rdr.GetName(ordinal);
            if (nameDictionary.ContainsKey(name) == false)
                return name;
            else
                return nameDictionary[name];
        }

        public override int GetOrdinal(string name)
        {
            return rdr.GetOrdinal(name);
        }

        public override DataTable GetSchemaTable()
        {
            return rdr.GetSchemaTable();
        }

        public override string GetString(int ordinal)
        {
            return rdr.GetString(ordinal);
        }

        private object GetValueInternal(int ordinal)
        {
            String ingresTypeName = rdr.GetDataTypeName(ordinal);

            switch (ingresTypeName)
            {
                case "char":
                case "nchar":
                    if (TrimChars)
                    {
                        var value = (string)rdr.GetValue(ordinal);
                        if (value != null)
                        {
                            return value.TrimEnd();
                        }
                        return value;
                    }
                    break;
                case "integer1":
                    var byteValue = rdr.GetValue(ordinal);
                    return Convert.ToSByte(byteValue);
            }
            return rdr.GetValue(ordinal);
        }

        public override object GetValue(int ordinal)
        {
            return GetValueInternal(ordinal);
        }

        public override int GetValues(object[] values)
        {
            return rdr.GetValues(values);
        }

        public override bool IsDBNull(int ordinal)
        {
            return rdr.IsDBNull(ordinal);
        }

        public override bool NextResult()
        {
            return rdr.NextResult();
        }

        public override bool Read()
        {
            return rdr.Read();
        }

        /// <summary>
        /// Build a dictionary for <select_column_name_list>
        /// to map the lower-case column names returned by Ingres
        /// to the original camel-case names expected by EF.
        /// We care because EF is searching on the camel-case names,
        /// not the lower-case names returned by the DBMS.
        /// </summary>
        /// <param name="commandText"></param>
        public void BuildNameDictionary(string commandText)
        {
            string tokenPrior = null;

            nameDictionary.Clear();

            if (commandText == null ||  // safety check for no text
                commandText.Length == 0)
                return;

            string[] tokens = commandText.Split(
                (char[])null,    // split the string using whitespace delimiters
                StringSplitOptions.RemoveEmptyEntries);

            if (tokens[0].Equals("SELECT") != true)
                return;

            // A typical CommandText might look like:
            //   SELECT  FIRST 1
            //     "Project1"."C1" AS "C1",
            //     "Project1"."MigrationId" AS "MigrationId",
            //     "Project1"."Model" AS "Model",
            //     "Project1"."ProductVersion" AS "ProductVersion"
            //   FROM ( SELECT ...

            // Each token is split off from CommandText using a whitespace delim.
            // The token might be a qualifier like "mytable"."mycolumn" but we
            // don't care since EF will add an alias AS "myalias" to such a
            // qualified identifier and we will add that alias name to the dict.
            // The token might contain a comma at the the end, but we will
            // use that comma as a signal to add the identifier to the dict.
            // Commas at the end of identifiers and the FROM keyword are used
            // to signal adding the preceding identifier to the dictionary.
            foreach (string token in tokens)
            {
                if (token.Equals("FROM"))  // stop scan at first FROM keyword
                {
                    BuildNameDictionaryItem(tokenPrior);
                    return;
                }
                if (token.Equals(","))  // just in case of standalone comma
                {
                    BuildNameDictionaryItem(tokenPrior);
                    continue;
                }
                if (token.EndsWith(","))
                {
                    BuildNameDictionaryItem(token);
                    continue;
                }
                tokenPrior = token;  // remember prior token
            }
        }  // end BuildNameDictionary()

        private void BuildNameDictionaryItem(string token)
        {
            if (token == null  ||   // safety check
                token.Length == 0)
                return;

            int tokenStart  = 0;
            int tokenLength = token.Length;
            string ident, identLower;

            if (token.EndsWith(","))     // strip trailing comma
                tokenLength--;
            if (token.StartsWith("\""))  // strip quotes
            {
                tokenStart++;     // skip past start quote
                tokenLength -= 2; // drop start and ending quotes
            }

            // extract identifier from its quotes and add to dict
            ident = token.Substring(tokenStart, tokenLength);
            identLower = ident.ToLowerInvariant();
            // safety check for ArgumentException of duplicate key
            if (nameDictionary.ContainsKey(identLower))
                return;
            nameDictionary.Add(identLower, ident);
        }  // BuildNameDictionaryItem

    }  // end class IngresEFDbDataReader
}
