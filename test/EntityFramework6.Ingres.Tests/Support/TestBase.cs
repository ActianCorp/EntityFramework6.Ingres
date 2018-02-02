#region License
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
using NLog.Config;
using NLog.Targets;
using NLog;
using Ingres.Client;
//using Npgsql.Logging;

using NUnit.Framework;

// ReSharper disable once CheckNamespace
namespace EntityFramework6.Ingres.Tests
{
    public abstract class TestBase
    {
        static readonly Logger _log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// The connection string that will be used when opening the connection to the tests database.
        /// May be overridden in fixtures, e.g. to set special connection parameters
        /// </summary>
        protected virtual string ConnectionString =>
            _connectionString ?? (_connectionString = Environment.GetEnvironmentVariable("INGRES_TEST_DB") ?? DefaultConnectionString);

        string _connectionString;

        static bool _loggingSetUp;

        /// <summary>
        /// Unless the NPGSQL_TEST_DB environment variable is defined, this is used as the connection string for the
        /// test database.
        /// </summary>
        const string DefaultConnectionString = 
"Server=Blundsford-w541;Port=M67;Database=ef6db;User ID=ingres;Password=What$now-135";
        //const string DefaultConnectionString = "Server=thoda01-790;Port=MM7;User ID=ingres;Password=Djtdjtjt8;Database=ingres_test_ef6";
        //const string DefaultConnectionString = "Server=localhost;User ID=npgsql_tests;Password=npgsql_tests;Database=npgsql_tests_ef6";

        #region Setup / Teardown

        [OneTimeSetUp]
        public virtual void TestFixtureSetup()
        {
            SetupLogging();
            _log.Debug("Connection string is: " + ConnectionString);
        }

        protected virtual void SetupLogging()
        {
            var config = new LoggingConfiguration();
            var consoleTarget = new ConsoleTarget();
            consoleTarget.Layout = @"${message} ${exception:format=tostring}";
            config.AddTarget("console", consoleTarget);
            var rule = new LoggingRule("*", NLog.LogLevel.Debug, consoleTarget);
            config.LoggingRules.Add(rule);
            NLog.LogManager.Configuration = config;

            if (!_loggingSetUp)
            {
                //NpgsqlLogManager.Provider = new NLogLoggingProvider();
                //NpgsqlLogManager.IsParameterLoggingEnabled = true;
                _loggingSetUp = true;
            }
        }

        #endregion

        #region Utilities for use by tests

        protected IngresConnection OpenConnection(string connectionString = null)
        {
            if (connectionString == null)
                connectionString = ConnectionString;
            var conn = new IngresConnection(connectionString);
            try
            {
                conn.Open();
            }
            catch (IngresException) // e)
            {
                //if (e. e.SqlState == "3D000")
                //    TestUtil.IgnoreExceptOnBuildServer("Please create a database npgsql_tests, owned by user npgsql_tests");
                //else if (e.SqlState == "28P01")
                //    TestUtil.IgnoreExceptOnBuildServer("Please create a user npgsql_tests as follows: create user npgsql_tests with password 'npgsql_tests'");
                //else
                    throw;
            }

            return conn;
        }

        protected IngresConnection OpenConnection(IngresConnectionStringBuilder csb)
        {
            return OpenConnection(csb.ToString());
        }

        #endregion
    }
}
