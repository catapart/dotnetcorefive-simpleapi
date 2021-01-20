using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace SimpleAPI_NetCore50.Data
{
    public class SimpleApiDBContext : IdentityDbContext<Authentication.Account>
    {
        public DbSet<Authentication.Account> Accounts { get; set; }
        public DbSet<Models.DataItem> DataItems { get; set; }
        public DbSet<Models.FileMap> FileMaps { get; set; }

        public SimpleApiDBContext(DbContextOptions<SimpleApiDBContext> options) : base(options)
        {
            // [enhancement] handle updating the DB model without destroying the whole DB.
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Models.DataItem>().ToTable("DataItem");
            modelBuilder.Entity<Models.FileMap>().ToTable("FileMap");
        }
    }
}
