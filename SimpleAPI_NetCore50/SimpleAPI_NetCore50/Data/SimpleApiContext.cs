using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace SimpleAPI_NetCore50.Data
{
    public class SimpleApiContext : IdentityDbContext<Authentication.Account>
    {
        public DbSet<Authentication.Account> Accounts { get; set; }
        public SimpleApiContext(DbContextOptions<SimpleApiContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
