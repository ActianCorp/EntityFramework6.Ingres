﻿#region License
// The PostgreSQL License
//
// Copyright (C) 2016 The Npgsql Development Team
//
// Permission to use, copy, modify, and distribute this software and its
// documentation for any purpose, without fee, and without a written
// agreement is hereby granted, provided that the above copyright notice
// and this paragraph and the following two paragraphs appear in all copies.
//
// IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY
// FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES,
// INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
// DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//
// THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
// ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS
// TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using Ingres.Client;

// ReSharper disable once CheckNamespace
namespace EntityFramework6.Ingres.Tests
{
    public static class TestUtil
    {
        public static bool IsOnBuildServer => Environment.GetEnvironmentVariable("TEAMCITY_VERSION") != null;

        /// <summary>
        /// Calls Assert.Ignore() unless we're on the build server, in which case calls
        /// Assert.Fail(). We don't to miss any regressions just because something was misconfigured
        /// at the build server and caused a test to be inconclusive.
        /// </summary>
        public static void IgnoreExceptOnBuildServer(string message)
        {
            if (IsOnBuildServer)
                Assert.Fail(message);
            else
                Assert.Ignore(message);
        }

        public static void IgnoreExceptOnBuildServer(string message, params object[] args)
            => IgnoreExceptOnBuildServer(string.Format(message, args));

        public static void MinimumPgVersion(IngresConnection conn, string minVersion, string ignoreText = null)
        {
            //var min = new Version(minVersion);
            //if (conn.PostgreSqlVersion < min)
            //{
            //    var msg = $"Postgresql backend version {conn.PostgreSqlVersion} is less than the required {min}";
            //    if (ignoreText != null)
            //        msg += ": " + ignoreText;
            //    Assert.Ignore(msg);
            //}
        }

        public static string GetUniqueIdentifier(string prefix)
            => prefix + Interlocked.Increment(ref _counter);

        static int _counter;
    }

    public static class IngresConnectionExtensions
    {
        public static int ExecuteNonQuery(this IngresConnection conn, string sql, IngresTransaction tx = null)
        {
            var cmd = tx == null ? new IngresCommand(sql, conn) : new IngresCommand(sql, conn, tx);
            using (cmd)
                return cmd.ExecuteNonQuery();
        }

        [CanBeNull]
        public static object ExecuteScalar(this IngresConnection conn, string sql, IngresTransaction tx = null)
        {
            var cmd = tx == null ? new IngresCommand(sql, conn) : new IngresCommand(sql, conn, tx);
            using (cmd)
                return cmd.ExecuteScalar();
        }

        public static async Task<int> ExecuteNonQueryAsync(this IngresConnection conn, string sql, IngresTransaction tx = null)
        {
            var cmd = tx == null ? new IngresCommand(sql, conn) : new IngresCommand(sql, conn, tx);
            using (cmd)
                return await cmd.ExecuteNonQueryAsync();
        }

        [CanBeNull]
        public static async Task<object> ExecuteScalarAsync(this IngresConnection conn, string sql, IngresTransaction tx = null)
        {
            var cmd = tx == null ? new IngresCommand(sql, conn) : new IngresCommand(sql, conn, tx);
            using (cmd)
                return await cmd.ExecuteScalarAsync();
        }
    }

    /// <summary>
    /// Semantic attribute that points to an issue linked with this test (e.g. this
    /// test reproduces the issue)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class IssueLink : Attribute
    {
        public string LinkAddress { get; private set; }
        public IssueLink(string linkAddress)
        {
            LinkAddress = linkAddress;
        }
    }

    /// <summary>
    /// Causes the test to be ignored on mono
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false)]
    public class MonoIgnore : Attribute, ITestAction
    {
        readonly string _ignoreText;

        public MonoIgnore(string ignoreText = null) { _ignoreText = ignoreText; }

        public void BeforeTest([NotNull] ITest test)
        {
            if (Type.GetType("Mono.Runtime") != null)
            {
                var msg = "Ignored on mono";
                if (_ignoreText != null)
                    msg += ": " + _ignoreText;
                Assert.Ignore(msg);
            }
        }

        public void AfterTest([NotNull] ITest test) { }
        public ActionTargets Targets => ActionTargets.Test;
    }
}
