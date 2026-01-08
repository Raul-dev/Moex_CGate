using BenchmarkDotNet.Columns;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestPerformance
{
    public partial class TestDBContext : DbContext
    {
        public TestDBContext(DbContextOptions<TestDBContext> options)
            : base(options)
        {
        }



    }
}
