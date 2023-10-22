using System;
using System.ComponentModel.DataAnnotations;

namespace CurrencyRatesApp.Models
{
    public class CurrencyRate
    {
        public string Id { get; set; }
        public string CurrencyCode { get; set; }
        public decimal ExchangeRate { get; set; }
        public DateTime Date { get; set; }
    }
}
