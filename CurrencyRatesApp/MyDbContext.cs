using CurrencyRatesApp.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CurrencyRatesApp
{
    public class MyDbContext : DbContext
    {
        public DbSet<CurrencyRate> CurrencyRates { get; set; }
        public MyDbContext():base ("ConnectionString")
        {

        }
    }
}
