using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EntityFramework6.Ingres.Tests;


namespace EntityFramework6.Ingres.ConsoleTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            EntityFrameworkBasicTests test = new EntityFrameworkBasicTests();
            test.TestFixtureSetup();
            //            test.TestComputedValue();
            //            test.SelectWithWhere();
            test.InsertAndSelect();
            //test.DataTypes();
            return 0;
        }
    }
}
