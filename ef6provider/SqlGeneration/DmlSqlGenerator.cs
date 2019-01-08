//---------------------------------------------------------------------
// <copyright file="DmlSqlGenerator.cs" company="Microsoft">
//      Portions of this file copyright (c) Microsoft Corporation
//      and are released under the Microsoft Public License.  See
//      https://opensource.org/licenses/MS-PL for details.
//      All rights reserved.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Core.Common.CommandTrees;
using Ingres.Client;

namespace Ingres
{
  /// <summary>
  /// Class generating SQL for a DML command tree.
  /// </summary>
  internal static class DmlSqlGenerator
  {
    private static readonly int s_commandTextBuilderInitialCapacity = 256;

    internal static string GenerateUpdateSql(
        DbUpdateCommandTree tree,
        SqlGenerator sqlGenerator,
        out List<DbParameter> parameters)
    {
      StringBuilder commandText = new StringBuilder(s_commandTextBuilderInitialCapacity);
      ExpressionTranslator translator =
          new ExpressionTranslator(
              commandText,
              tree,
              null != tree.Returning,
              sqlGenerator,
              "UpdateFunction");

      // update [schemaName].[tableName]
      commandText.Append("UPDATE ");
      tree.Target.Expression.Accept(translator);
      commandText.AppendLine();

      // set c1 = ..., c2 = ..., ...
      bool first = true;
      commandText.Append("SET ");
      foreach (DbSetClause setClause in tree.SetClauses)
      {
        if (first) { first = false; }
        else { commandText.Append(", "); }
        setClause.Property.Accept(translator);
        commandText.Append(" = ");
        setClause.Value.Accept(translator);
      }

      if (first)
      {
        // If first is still true, it indicates there were no set
        // clauses. Introduce a fake set clause so that:
        // - we acquire the appropriate locks
        // - server-gen columns (e.g. timestamp) get recomputed
        //
        // We use the following pattern:
        //
        //  update Foo
        //  set @i = 0
        //  where ...
        DbParameter parameter = translator.CreateParameter(default(Int32), DbType.Int32);
        commandText.Append(parameter.ParameterName);
        commandText.Append(" = 0");
      }
      commandText.AppendLine();

      // where c1 = ..., c2 = ...
      commandText.Append("WHERE ");
      tree.Predicate.Accept(translator);
      commandText.AppendLine(" ");

      // generate returning sql
      GenerateReturningSql(commandText, tree, translator, tree.Returning, false);

      parameters = translator.Parameters;
      return commandText.ToString();
    }

    internal static string GenerateDeleteSql(
        DbDeleteCommandTree tree,
        SqlGenerator sqlGenerator,
        out List<DbParameter> parameters,
        bool createParameters = true)
    {
      StringBuilder commandText = new StringBuilder(s_commandTextBuilderInitialCapacity);
      ExpressionTranslator translator =
           new ExpressionTranslator(
               commandText,
               tree,
               false,
               sqlGenerator,
               "DeleteFunction",
               createParameters);

      // delete [schemaName].[tableName]
      commandText.Append("DELETE FROM ");
      tree.Target.Expression.Accept(translator);
      commandText.AppendLine();

      // where c1 = ... AND c2 = ...
      commandText.Append("WHERE ");
      tree.Predicate.Accept(translator);

      parameters = translator.Parameters;

      commandText.AppendLine(" ");
      return commandText.ToString();
    }

    internal static string GenerateInsertSql(
        DbInsertCommandTree tree,
        SqlGenerator sqlGenerator,
        out List<DbParameter> parameters,
        bool createParameters = true)
    {
      StringBuilder commandText = new StringBuilder(s_commandTextBuilderInitialCapacity);
      ExpressionTranslator translator =
          new ExpressionTranslator(
              commandText,
              tree,
              null != tree.Returning,
              sqlGenerator,
              "InsertFunction",
              createParameters);

      // insert [schemaName].[tableName]
      commandText.Append("INSERT INTO ");
      tree.Target.Expression.Accept(translator);

      if (tree.SetClauses.Count > 0)
      {
        // (c1, c2, c3, ...)
        commandText.Append("(");
        bool first = true;
        foreach (DbSetClause setClause in tree.SetClauses)
        {
          if (first) { first = false; }
          else { commandText.Append(", "); }
          setClause.Property.Accept(translator);
        }
        commandText.AppendLine(")");

        // values c1, c2, ...
        first = true;
        commandText.Append(" VALUES (");
        foreach (DbSetClause setClause in tree.SetClauses)
        {
          if (first) { first = false; }
          else { commandText.Append(", "); }
          setClause.Value.Accept(translator);

          translator.RegisterMemberValue(setClause.Property, setClause.Value);
        }
        commandText.AppendLine(") ");
      }
      else // No columns specified.  Insert an empty row containing default values by inserting null into the rowid
      {
        commandText.AppendLine(" DEFAULT VALUES ");
      }

      // generate returning sql
      GenerateReturningSql(commandText, tree, translator, tree.Returning, true);

      parameters = translator.Parameters;
      return commandText.ToString();
    }

    // Generates T-SQL describing a member
    // Requires: member must belong to an entity type (a safe requirement for DML
    // SQL gen, where we only access table columns)
    private static string GenerateMemberTSql(EdmMember member)
    {
      return SqlGenerator.QuoteIdentifier(member.Name);
    }

    /// <summary>
    /// This method attempts to determine if the specified table has an integer
    /// primary key (i.e. "rowid").  If so, it sets the
    /// <paramref name="primaryKeyMember" /> parameter to the right
    /// <see cref="EdmMember" />; otherwise, the
    /// <paramref name="primaryKeyMember" /> parameter is set to null.
    /// </summary>
    /// <param name="table">The table to check.</param>
    /// <param name="keyMembers">
    /// The collection of key members.  An attempt is always made to set this
    /// parameter to a valid value.
    /// </param>
    /// <param name="primaryKeyMember">
    /// The <see cref="EdmMember" /> that represents the integer primary key
    /// -OR- null if no such <see cref="EdmMember" /> exists.
    /// </param>
    /// <returns>
    /// Non-zero if the specified table has an integer primary key.
    /// </returns>
    private static bool IsIntegerPrimaryKey(
        EntitySetBase table,
        out ReadOnlyMetadataCollection<EdmMember> keyMembers,
        out EdmMember primaryKeyMember
        )
    {
        keyMembers = table.ElementType.KeyMembers;

        if (keyMembers.Count == 1) /* NOTE: The "rowid" only? */
        {
            EdmMember keyMember = keyMembers[0];
            PrimitiveTypeKind typeKind;

            if (MetadataHelpers.TryGetPrimitiveTypeKind(
                    keyMember.TypeUsage, out typeKind) &&
                (typeKind == PrimitiveTypeKind.Int64))
            {
                primaryKeyMember = keyMember;
                return true;
            }
        }

        primaryKeyMember = null;
        return false;
    }

    /// <summary>
    /// This method attempts to determine if all the specified key members have
    /// values available.
    /// </summary>
    /// <param name="translator">
    /// The <see cref="ExpressionTranslator" /> to use.
    /// </param>
    /// <param name="keyMembers">
    /// The collection of key members to check.
    /// </param>
    /// <param name="missingKeyMember">
    /// The first missing key member that is found.  This is only set to a valid
    /// value if the method is returning false.
    /// </param>
    /// <returns>
    /// Non-zero if all key members have values; otherwise, zero.
    /// </returns>
    private static bool DoAllKeyMembersHaveValues(
        ExpressionTranslator translator,
        ReadOnlyMetadataCollection<EdmMember> keyMembers,
        out EdmMember missingKeyMember
        )
    {
        foreach (EdmMember keyMember in keyMembers)
        {
            if (!translator.MemberValues.ContainsKey(keyMember))
            {
                missingKeyMember = keyMember;
                return false;
            }
        }

        missingKeyMember = null;
        return true;
    }

    /// <summary>
    /// Generates SQL fragment returning server-generated values.
    /// Requires: translator knows about member values so that we can figure out
    /// how to construct the key predicate.
    /// <code>
    /// Sample SQL:
    ///     
    ///     select IdentityValue
    ///     from myschema.MyTable
    ///     where __ingres_@@ROWCOUNT > 0 and IdentityValue = LAST_IDENTITY()
    /// 
    ///     The "__ingres_@@ROWCOUNT" will be replaced by
    ///     the IngresDbCommandInterceptor with the number
    ///     returned for recordsAffected in the batch execution.
    /// 
    /// Note that we filter on rowcount to ensure no rows are returned if no rows were modified.
    /// </code>
    /// </summary>
    /// <param name="commandText">Builder containing command text</param>
    /// <param name="tree">Modification command tree</param>
    /// <param name="translator">Translator used to produce DML SQL statement
    /// for the tree</param>
    /// <param name="returning">Returning expression. If null, the method returns
    /// immediately without producing a SELECT statement.</param>
    /// <param name="wasInsert">
    /// Non-zero if this method is being called as part of processing an INSERT;
    /// otherwise (e.g. UPDATE), zero.
    /// </param>
    private static void GenerateReturningSql(
        StringBuilder commandText,
        DbModificationCommandTree tree,
        ExpressionTranslator translator,
        DbExpression returning,
        bool wasInsert)
    {
      // Nothing to do if there is no Returning expression
      if (null == returning) { return; }

      commandText.Append(";");  // semicolon separated batch of SQL statements
                                // to be executed by IngresDbCommandInterceptor
      // select
      commandText.Append("SELECT ");
      returning.Accept(translator);  // e.g. "BlogId", "IntComputedValue"
      commandText.AppendLine();

      // from
      commandText.Append("FROM ");
      tree.Target.Expression.Accept(translator);  // e.g. "Blogs"
      commandText.AppendLine();

      // where
      // (Ensure prior INSERT statements completed OK using recordsAffected.
      // The IngresDbCommandInterceptor will cut/paste __ingres_@@ROWCOUNT
      // with the number returned for recordsAffected.)
      commandText.Append("WHERE __ingres_@@ROWCOUNT > 0");

      EntitySetBase table = ((DbScanExpression)tree.Target.Expression).Target;
      ReadOnlyMetadataCollection<EdmMember> keyMembers = 
          table.ElementType.KeyMembers;
      var hasIdentityBeenAlreadyProcessed = false;

      foreach (var keyMember in keyMembers)
      {
          commandText.Append(" AND ");
          commandText.Append(GenerateMemberTSql(keyMember));
          commandText.Append(" = ");

          // Retrieve member value sql. the translator remembers
          // member values as it constructs the INSERT/UPDATE
          // statement (which precedes the "returning" SQL SELECT)
          DbParameter value;
          if (translator.MemberValues.TryGetValue(keyMember, out value))
          {
              commandText.Append(value.ParameterName);
              continue;  // move on to the next keyMember in keyMembers
          }

          // We have no member value to retrieve the row. Argh!

          // If INSERT then might be able to use IDENTITY.
          // Check that the column is an exact numeric data
          // type in order to qualify as an Identity column.
          if (wasInsert  &&
              IsaIdentityColumnType(keyMember.TypeUsage))
          {
              // If no value is registered for the key member,
              // it means it is an Identity Column value which can
              // be retrieved using the LAST_IDENTITY() function
              if (hasIdentityBeenAlreadyProcessed)
              {
                  // there can be only one server generated key
                  throw new NotSupportedException(String.Format(
                      "Store-generated keys are only supported for " +
                      "identity columns. More than one key column '{0}' " +
                      "is marked as server generated in table '{1}'.",
                    (keyMember != null) ? keyMember.Name : "<unknown>",
                    table.Name));
              }
              // LAST_IDENTITY() is a sequence expression function that
              // returns the last value generated for an identity column
              // in any table for the current session. Since we had just done
              // a successfull INSERT (recordsAffected > 0) of a row into a
              // table with an identity column, we expect to retrieve that row.
              commandText.Append("LAST_IDENTITY()");
              hasIdentityBeenAlreadyProcessed = true;
          }  // end if (wasInsert and possible Identity column)
          else  // else UPDATE or not an Identity column
          {
              // We cannot even try to use LAST_IDENTITY() at this point
              // because the LAST_IDENTITY() function is only valid after
              // an INSERT and this was an UPDATE or not an Identity.
              throw new NotSupportedException(String.Format(
                  "Missing value for {0} key member '{1}' in table '{2}'.",
                  (wasInsert) ? "INSERT" : "UPDATE",
                  (keyMember != null) ? keyMember.Name : "<unknown>",
                  table.Name));
          }
      }  // end foreach (var keyMember in keyMembers)

      commandText.AppendLine(";");  // mark end of batch statements to be
                                    // executed by IngresDbCommandInterceptor
    }

    /// <summary>
    /// True if the type is an exact numeric suitable
    /// for an IDENTITY column.
    /// </summary>
    /// <param name="typeUsage"></param>
    /// <returns></returns>
    private static bool IsaIdentityColumnType(TypeUsage typeUsage)
    {
        // Ingres supports the following types for identity columns:
        // tinyint, smallint, int, bigint, decimal(p,0), or numeric(p,0)

        // make sure it's a primitive type
        if (typeUsage.EdmType.BuiltInTypeKind != BuiltInTypeKind.PrimitiveType)
        {
            return false;
        }

        // check if this is a supported primitive type (compare by name)
        var typeName = typeUsage.EdmType.Name;

        // integer types
        if (typeName == "tinyint"  ||
            typeName == "smallint" ||
            typeName == "int"      ||
            typeName == "bigint")
        {
            return true;
        }

        // variable scale types (require scale = 0)
        if (typeName == "decimal"  ||
            typeName == "numeric")
        {
            Facet scaleFacet;
            // return false if scale != 0
            return (typeUsage.Facets.TryGetValue(
                    "Scale", false, out scaleFacet)  &&
                Convert.ToInt32(scaleFacet.Value, CultureInfo.InvariantCulture) == 0);
        }

        // type not in supported list
        return false;
    }


   /// <summary>
   /// Lightweight expression translator for DML expression trees, which have constrained
   /// scope and support. DbExpression abstracts the implementation details for each DBMS
   /// into an abstract syntax tree and provide a fluent interface to help build this tree.
   /// A DbExpression "compiles" into the vendor (e.g. Ingres) specific SQL dialect.
   /// The query is represented by a set of nodes in a commandTree that when visited
   /// (Visitor pattern), translates each DbExpression to the expected syntax.
   /// </summary>
    private class ExpressionTranslator : DbExpressionVisitor
    {
      private readonly StringBuilder _commandText;
      private readonly DbModificationCommandTree _commandTree;
      private readonly List<DbParameter> _parameters;
      private readonly Dictionary<EdmMember, DbParameter> _memberValues;
      private readonly SqlGenerator _sqlGenerator;
      private int parameterNameCount = 0;
      private string _kind;
      private readonly bool _createParameters;

      /// <summary>
      /// Initialize a new expression translator populating the given string builder
      /// with command text. Command text builder and command tree must not be null.
      /// </summary>
      /// <param name="commandText">Command text with which to populate commands</param>
      /// <param name="commandTree">Command tree generating SQL</param>
      /// <param name="preserveMemberValues">Indicates whether the translator should preserve
      /// member values while compiling t-SQL (only needed for server generation)</param>
      /// <param name="kind"></param>
      /// <param name="createParameters">Create Parameters (true) or literals</param>
      internal ExpressionTranslator(
          StringBuilder commandText,
          DbModificationCommandTree commandTree,
          bool preserveMemberValues,
          SqlGenerator sqlGenerator,
          string kind,
          bool createParameters = true)
      {
        Debug.Assert(null != commandText);
        Debug.Assert(null != commandTree);
        _kind = kind;
        _commandText = commandText;
        _commandTree = commandTree;
        _sqlGenerator = sqlGenerator;
        _parameters = new List<DbParameter>();
        _memberValues = preserveMemberValues ? new Dictionary<EdmMember, DbParameter>() :
            null;

        _createParameters = createParameters;
      }

      internal List<DbParameter> Parameters { get { return _parameters; } }
      internal Dictionary<EdmMember, DbParameter> MemberValues { get { return _memberValues; } }

      // generate parameter (name based on parameter ordinal)
      internal IngresParameter CreateParameter(object value, TypeUsage type)
      {
        PrimitiveTypeKind primitiveType = MetadataHelpers.GetPrimitiveTypeKind(type);
        DbType dbType = MetadataHelpers.GetDbType(primitiveType);
        return CreateParameter(value, dbType);
      }

      // Creates a new parameter for a value in this expression translator
      internal IngresParameter CreateParameter(object value, DbType dbType)
      {
        string parameterName = string.Concat("@p", parameterNameCount.ToString(CultureInfo.InvariantCulture));
        parameterNameCount++;
        IngresParameter parameter = new IngresParameter(parameterName, value);
        parameter.DbType = dbType;
        _parameters.Add(parameter);
        return parameter;
      }

#region Basics

      public override void Visit(DbApplyExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");

        VisitExpressionBindingPre(expression.Input);
        if (expression.Apply != null)
        {
          VisitExpression(expression.Apply.Expression);
        }
        VisitExpressionBindingPost(expression.Input);
      }

      public override void Visit(DbArithmeticExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionList(expression.Arguments);
      }

      public override void Visit(DbCaseExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionList(expression.When);
        VisitExpressionList(expression.Then);
        VisitExpression(expression.Else);
      }

      public override void Visit(DbCastExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbCrossJoinExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        foreach (DbExpressionBinding binding in expression.Inputs)
        {
          VisitExpressionBindingPre(binding);
        }
        foreach (DbExpressionBinding binding2 in expression.Inputs)
        {
          VisitExpressionBindingPost(binding2);
        }
      }

      public override void Visit(DbDerefExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbDistinctExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbElementExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbEntityRefExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbExceptExpression expression)
      {
        VisitBinary(expression);
      }

      protected virtual void VisitBinary(DbBinaryExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        this.VisitExpression(expression.Left);
        this.VisitExpression(expression.Right);
      }

      public override void Visit(DbExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        throw new NotSupportedException("DbExpression");
      }

      public override void Visit(DbFilterExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionBindingPre(expression.Input);
        VisitExpression(expression.Predicate);
        VisitExpressionBindingPost(expression.Input);
      }

      public override void Visit(DbFunctionExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionList(expression.Arguments);
        //if (expression.IsLambda)
        //{
        //  VisitLambdaFunctionPre(expression.Function, expression.LambdaBody);
        //  VisitExpression(expression.LambdaBody);
        //  VisitLambdaFunctionPost(expression.Function, expression.LambdaBody);
        //}
      }

      public override void Visit(DbGroupByExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitGroupExpressionBindingPre(expression.Input);
        VisitExpressionList(expression.Keys);
        VisitGroupExpressionBindingMid(expression.Input);
        VisitAggregateList(expression.Aggregates);
        VisitGroupExpressionBindingPost(expression.Input);
      }

      public override void Visit(DbIntersectExpression expression)
      {
        VisitBinary(expression);
      }

      public override void Visit(DbIsEmptyExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbIsOfExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbJoinExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionBindingPre(expression.Left);
        VisitExpressionBindingPre(expression.Right);
        VisitExpression(expression.JoinCondition);
        VisitExpressionBindingPost(expression.Left);
        VisitExpressionBindingPost(expression.Right);
      }

      public override void Visit(DbLikeExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpression(expression.Argument);
        VisitExpression(expression.Pattern);
        VisitExpression(expression.Escape);
      }

      public override void Visit(DbLimitExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpression(expression.Argument);
        VisitExpression(expression.Limit);
      }

      public override void Visit(DbOfTypeExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbParameterReferenceExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
      }

      public override void Visit(DbProjectExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionBindingPre(expression.Input);
        VisitExpression(expression.Projection);
        VisitExpressionBindingPost(expression.Input);
      }

      public override void Visit(DbQuantifierExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionBindingPre(expression.Input);
        VisitExpression(expression.Predicate);
        VisitExpressionBindingPost(expression.Input);
      }

      public override void Visit(DbRefExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbRefKeyExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbRelationshipNavigationExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpression(expression.NavigationSource);
      }

      public override void Visit(DbSkipExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionBindingPre(expression.Input);
        foreach (DbSortClause clause in expression.SortOrder)
        {
          VisitExpression(clause.Expression);
        }
        VisitExpressionBindingPost(expression.Input);
        VisitExpression(expression.Count);
      }

      public override void Visit(DbSortExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpressionBindingPre(expression.Input);
        for (int i = 0; i < expression.SortOrder.Count; i++)
        {
          VisitExpression(expression.SortOrder[i].Expression);
        }
        VisitExpressionBindingPost(expression.Input);
      }

      public override void Visit(DbTreatExpression expression)
      {
        VisitUnaryExpression(expression);
      }

      public override void Visit(DbUnionAllExpression expression)
      {
        VisitBinary(expression);
      }

      public override void Visit(DbVariableReferenceExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
      }

      public virtual void VisitAggregate(DbAggregate aggregate)
      {
        if (aggregate == null) throw new ArgumentException("aggregate");
        VisitExpressionList(aggregate.Arguments);
      }

      public virtual void VisitAggregateList(IList<DbAggregate> aggregates)
      {
        if (aggregates == null) throw new ArgumentException("aggregates");
        for (int i = 0; i < aggregates.Count; i++)
        {
          VisitAggregate(aggregates[i]);
        }
      }

      public virtual void VisitExpression(DbExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        expression.Accept(this);
      }

      protected virtual void VisitExpressionBindingPost(DbExpressionBinding binding)
      {
      }

      protected virtual void VisitExpressionBindingPre(DbExpressionBinding binding)
      {
        if (binding == null) throw new ArgumentException("binding");
        VisitExpression(binding.Expression);
      }

      public virtual void VisitExpressionList(IList<DbExpression> expressionList)
      {
        if (expressionList == null) throw new ArgumentException("expressionList");
        for (int i = 0; i < expressionList.Count; i++)
        {
          VisitExpression(expressionList[i]);
        }
      }

      protected virtual void VisitGroupExpressionBindingMid(DbGroupExpressionBinding binding)
      {
      }

      protected virtual void VisitGroupExpressionBindingPost(DbGroupExpressionBinding binding)
      {
      }

      protected virtual void VisitGroupExpressionBindingPre(DbGroupExpressionBinding binding)
      {
        if (binding == null) throw new ArgumentException("binding");
        VisitExpression(binding.Expression);
      }

      protected virtual void VisitLambdaFunctionPost(EdmFunction function, DbExpression body)
      {
      }

      protected virtual void VisitLambdaFunctionPre(EdmFunction function, DbExpression body)
      {
        if (function == null) throw new ArgumentException("function");
        if (body == null) throw new ArgumentException("body");
      }

      //internal virtual void VisitRelatedEntityReference(DbRelatedEntityRef relatedEntityRef)
      //{
      //  VisitExpression(relatedEntityRef.TargetEntityReference);
      //}

      //internal virtual void VisitRelatedEntityReferenceList(IList<DbRelatedEntityRef> relatedEntityReferences)
      //{
      //  for (int i = 0; i < relatedEntityReferences.Count; i++)
      //  {
      //    VisitRelatedEntityReference(relatedEntityReferences[i]);
      //  }
      //}

      protected virtual void VisitUnaryExpression(DbUnaryExpression expression)
      {
        if (expression == null) throw new ArgumentException("expression");
        VisitExpression(expression.Argument);
      }
#endregion

      public override void Visit(DbAndExpression expression)
      {
        VisitBinary(expression, " AND ");
      }

      public override void Visit(DbOrExpression expression)
      {
        VisitBinary(expression, " OR ");
      }

      public override void Visit(DbComparisonExpression expression)
      {
        Debug.Assert(expression.ExpressionKind == DbExpressionKind.Equals,
            "only equals comparison expressions are produced in DML command trees in V1");

        VisitBinary(expression, " = ");

        RegisterMemberValue(expression.Left, expression.Right);
      }

      /// <summary>
      /// Call this method to register a property value pair so the translator "remembers"
      /// the values for members of the row being modified. These values can then be used
      /// to form a predicate for server-generation (based on the key of the row)
      /// </summary>
      /// <param name="propertyExpression">DbExpression containing the column reference (property expression).</param>
      /// <param name="value">DbExpression containing the value of the column.</param>
      internal void RegisterMemberValue(DbExpression propertyExpression, DbExpression value)
      {
        if (null != _memberValues)
        {
          // register the value for this property
          Debug.Assert(propertyExpression.ExpressionKind == DbExpressionKind.Property,
              "DML predicates and setters must be of the form property = value");

          // get name of left property 
          EdmMember property = ((DbPropertyExpression)propertyExpression).Property;

          // don't track null values
          if (value.ExpressionKind != DbExpressionKind.Null)
          {
            Debug.Assert(value.ExpressionKind == DbExpressionKind.Constant,
                "value must either constant or null");
            // retrieve the last parameter added (which describes the parameter)
            _memberValues[property] = _parameters[_parameters.Count - 1];
          }
        }
      }

      public override void Visit(DbIsNullExpression expression)
      {
        expression.Argument.Accept(this);
        _commandText.Append(" IS NULL");
      }

      public override void Visit(DbNotExpression expression)
      {
        _commandText.Append("NOT (");
        expression.Accept(this);
        _commandText.Append(")");
      }

      public override void Visit(DbConstantExpression expression)
      {
        if (_createParameters)
        {
            IngresParameter parameter =
                CreateParameter(expression.Value, expression.ResultType);
            _commandText.Append(parameter.ParameterName);
        }
        else
        {
            _commandText.Append(
                _sqlGenerator.WriteSql(expression.Accept(_sqlGenerator)));
        }
      }

      public override void Visit(DbScanExpression expression)
      {
        string definingQuery = MetadataHelpers.TryGetValueForMetadataProperty<string>(expression.Target, "DefiningQuery");
        if (definingQuery != null)
        {
          throw new NotSupportedException(String.Format("Unable to update the EntitySet '{0}' because it has a DefiningQuery and no <{1}> element exists in the <ModificationFunctionMapping> element to support the current operation.", expression.Target.Name, _kind));
        }
        _commandText.Append(SqlGenerator.GetTargetTSql(expression.Target));
      }

      public override void Visit(DbPropertyExpression expression)
      {
        _commandText.Append(GenerateMemberTSql(expression.Property));
      }

      public override void Visit(DbNullExpression expression)
      {
        _commandText.Append("NULL");
      }

      public override void Visit(DbNewInstanceExpression expression)
      {
        // assumes all arguments are self-describing (no need to use aliases
        // because no renames are ever used in the projection)
        bool first = true;
        foreach (DbExpression argument in expression.Arguments)
        {
          if (first) { first = false; }
          else { _commandText.Append(", "); }
          argument.Accept(this);
        }
      }

      private void VisitBinary(DbBinaryExpression expression, string separator)
      {
        _commandText.Append("(");
        expression.Left.Accept(this);
        _commandText.Append(separator);
        expression.Right.Accept(this);
        _commandText.Append(")");
      }
    }
  }
}

