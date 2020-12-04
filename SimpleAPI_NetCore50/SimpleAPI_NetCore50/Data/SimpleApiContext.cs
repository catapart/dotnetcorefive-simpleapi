using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SimpleAPI_NetCore50.Data
{
    public class SimpleApiContext : DbContext
    {
        public SimpleApiContext(DbContextOptions<SimpleApiContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}
