using System;
using System.ComponentModel.DataAnnotations;

namespace CurrencyRatesApp.Models
{
    public class CurrencyRate
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string CurrencyCode { get; set; }
        [Required]
        public decimal ExchangeRate { get; set; }
        [Required]
        public DateTime Date { get; set; }
    }
}
