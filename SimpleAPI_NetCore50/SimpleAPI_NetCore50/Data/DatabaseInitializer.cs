using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpleAPI_NetCore50.Data
{
    public static class DatabaseInitializer
    {
        public static void Initialize(SimpleApiContext context)
        {
            context.Database.EnsureCreated();

            if (context.Accounts.Any())
            {
                return; // Database has been populated.
            }

            // populate the database
            //var accounts = new Authentication.Account[] { };
            //foreach (Authentication.Account entity in accounts)
            //{
                //context.Accounts.Add(entity);
            //}
            //context.SaveChanges();
        }
    }
}
