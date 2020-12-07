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

            if (context.DataItems.Any())
            {
                return; // Database has been populated.
            }

            // populate the database
            Models.DataItem[] dataItems = new Models.DataItem[]
            {
                new Models.DataItem(){Id="FirstItem", Value="This is an item added by the Database initialization."}
            };
            foreach(Models.DataItem dataItem in dataItems)
            {
                context.DataItems.Add(dataItem);
            }


            context.SaveChanges();
        }
    }
}
