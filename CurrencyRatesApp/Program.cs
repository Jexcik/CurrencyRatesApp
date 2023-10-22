using CurrencyRatesApp.Models;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Linq;

namespace CurrencyRatesApp
{
    public class Program
    {
        private const string ConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ExchangeRates;Integrated Security=True;";
        private const string CbrApiUrl = "https://www.cbr.ru/scripts/XML_daily.asp";

        static async Task Main(string[] args)
        {
            var endDate = DateTime.Now;
            var startDate = endDate.AddMonths(-1);

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    List<CurrencyRate> currencyList = await GetCurrencyListFromCbrAsync(client);
                    using (SqlConnection connection = new SqlConnection(ConnectionString))
                    {
                        connection.Open();
                        foreach (var currency in currencyList)
                        {
                            bool isRecordExists = CheckIfRecordExists(connection, currency);
                            if (!isRecordExists)
                            {
                                InsertCurrencyRateToDatabase(connection, currency);
                                Console.WriteLine($"Запись для курса валюты {currency.CurrencyCode} на {currency.Date} успешно добавлена");
                            }
                            else
                            {
                                Console.WriteLine($"Запись для курса валюты {currency.CurrencyCode} на {currency.Date} уже существует");
                            }
                        }

                        foreach (var itemElement in currencyList)
                        {
                            List<CurrencyRate> currencyRates2 = await GetCurrencyRatesFromCbrAsync(client, itemElement.Id, startDate, endDate);
                            foreach (var rates in currencyRates2)
                            {
                                rates.Id = itemElement.Id;
                                rates.CurrencyCode = itemElement.CurrencyCode;
                                bool isRecordExists2 = CheckIfRecordExists(connection, rates);
                                if (!isRecordExists2)
                                {
                                    InsertCurrencyRateToDatabase(connection, rates);
                                    Console.WriteLine($"В БД запись для курса валюты {rates.CurrencyCode} на {rates.Date} успешно добавлена");
                                }
                                else
                                {
                                    Console.WriteLine($"В БД, запись для курса влюты {rates.CurrencyCode} на {rates.Date} уже существует");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при выполнении HTTP-запроса: {ex.Message}");
                }
            }

            Console.ReadLine();
        }

        private static async Task<List<CurrencyRate>> GetCurrencyListFromCbrAsync(HttpClient client)
        {
            var currencyList = new List<CurrencyRate>();
            HttpResponseMessage response = await client.GetAsync(CbrApiUrl);
            if (response.IsSuccessStatusCode)
            {
                var xmlData = await response.Content.ReadAsStringAsync();
                XDocument xmlDoc = XDocument.Parse(xmlData);
                currencyList = xmlDoc.Root.Elements("Valute").Select(x => new CurrencyRate
                {
                    Id = x.Attribute("ID").Value,
                    CurrencyCode = x.Element("CharCode").Value,
                    ExchangeRate = Convert.ToDecimal(x.Element("Value").Value.Replace('.', ',')),
                    Date = DateTime.Today
                }).ToList();
            }
            else
            {
                Console.WriteLine("Ошибка при получении данных: " + response.ReasonPhrase);
            }
            return currencyList;
        }

        private static async Task<List<CurrencyRate>> GetCurrencyRatesFromCbrAsync(HttpClient client, string valuteId, DateTime startDate, DateTime endDate)
        {
            string url = $"https://www.cbr.ru/scripts/XML_dynamic.asp?date_req1={startDate:dd/MM/yyyy}&date_req2={endDate:dd/MM/yyyy}&VAL_NM_RQ={valuteId}";
            HttpResponseMessage response = await client.GetAsync(url);
            var xmlData = await response.Content.ReadAsStringAsync();
            XDocument xDoc = XDocument.Parse(xmlData);
            var currencyRates2 = xDoc.Root.Elements("Record").Select(record => new CurrencyRate
            {
                Date = DateTime.Parse(record.Attribute("Date").Value).Date,
                ExchangeRate = decimal.Parse(record.Element("Value").Value.Replace(".", ","))
            }).ToList();
            return currencyRates2;
        }

        private static bool CheckIfRecordExists(SqlConnection connection, CurrencyRate rate)
        {
            string checkIfQuery = "SELECT TOP 1 1 FROM CurrencyRates WHERE CurrencyCode=@CurrencyCode AND Date=@Date";
            using (SqlCommand checkCommand = new SqlCommand(checkIfQuery, connection))
            {
                checkCommand.Parameters.AddWithValue("@CurrencyCode", rate.CurrencyCode);
                checkCommand.Parameters.AddWithValue("@Date", rate.Date);

                using(SqlDataReader reader=checkCommand.ExecuteReader())
                {
                    return reader.HasRows;
                }
            }
        }

        private static void InsertCurrencyRateToDatabase(SqlConnection connection, CurrencyRate rate)
        {
            string newRecord = "INSERT INTO CurrencyRates (CurrencyCode, ExchangeRate, Date) VALUES (@CurrencyCode, @ExchangeRate, @Date)";
            using (SqlCommand insertCommand = new SqlCommand(newRecord, connection))
            {
                insertCommand.Parameters.AddWithValue("@CurrencyCode", rate.CurrencyCode);
                insertCommand.Parameters.AddWithValue("@ExchangeRate", rate.ExchangeRate);
                insertCommand.Parameters.AddWithValue("@Date", rate.Date);
                insertCommand.ExecuteNonQuery();
            }
        }
    }
}
