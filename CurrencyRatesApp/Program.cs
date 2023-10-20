using CurrencyRatesApp.Models;
using Quartz;
using Quartz.Impl;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;
using System.Linq;

namespace CurrencyRatesApp
{

    public class Program
    {
        static async Task Main(string[] args)
        {
            //Строка подключения к БД
            string connectionString = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ExchangeRates;Integrated Security=True;";

            //Ссылка на получение данных о курсе валют на сегодняшний день
            string cbrApiUrl = "https://www.cbr.ru/scripts/XML_daily.asp";

            DateTime endDate = DateTime.Now;
            DateTime startDate = endDate.AddMonths(-1);
            //Ссылка на получение данных о курсах валют за последний месяц
            string url = $"https://www.cbr.ru/scripts/XML_dynamic.asp?date_req1={startDate:dd/MM/yyyy}&date_req2={endDate:dd/MM/yyyy}&VAL_NM_RQ=R01235";

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    //Выполнение GET запроса к сервису ЦБР
                    HttpResponseMessage response1 = await client.GetAsync(cbrApiUrl);
                    HttpResponseMessage response2 = await client.GetAsync(url);
                    if (response1.IsSuccessStatusCode && response2.IsSuccessStatusCode)
                    {
                        //получение данных XML
                        string xmlData1 = await response1.Content.ReadAsStringAsync();
                        string xmlData2 = await response2.Content.ReadAsStringAsync();

                        //Разбор XML
                        XmlDocument xmlDoc1 = new XmlDocument();
                        xmlDoc1.LoadXml(xmlData1);

                        //Парсинг курсов валют
                        var currencyRates = xmlDoc1.SelectNodes("//Valute");
                        var currencyList = currencyRates.Cast<XmlNode>().Select(node => new
                        {
                            CurrencyCode = node.SelectSingleNode("CharCode").InnerText,
                            ExchangeRate = Convert.ToDecimal(node.SelectSingleNode("Value").InnerText.Replace('.', ',')),
                            Date = DateTime.Today
                        }).ToList();

                        XDocument xDoc = XDocument.Parse(xmlData2);
                        List<CurrencyRate> currencyRates2 = new List<CurrencyRate>();
                        foreach (XElement record in xDoc.Root.Elements("Record"))
                        {
                            decimal rate = decimal.Parse(record.Element("Value").Value.Replace(".", ","));
                            string currencyCode = "USD";
                            DateTime date = DateTime.Parse(record.Attribute("Date").Value);

                            currencyRates2.Add(new CurrencyRate
                            {
                                Date = date.Date.Date,
                                CurrencyCode = currencyCode,
                                ExchangeRate = rate
                            });
                        }


                        using (SqlConnection connection = new SqlConnection(connectionString))
                        {
                            connection.Open();

                            foreach (var rate in currencyRates2)
                            {
                                //Проверка существует ли запись с такими данными
                                string checkIfQuery = "SELECT COUNT(*) FROM MonthlExchangeRates WHERE CurrencyCode=@CurrencyCode AND Date=@Date";


                                using (SqlCommand checkCommand = new SqlCommand(checkIfQuery, connection))
                                {
                                    checkCommand.Parameters.AddWithValue("@CurrencyCode", rate.CurrencyCode);
                                    checkCommand.Parameters.AddWithValue("@ExchangeRate", rate.ExchangeRate);
                                    checkCommand.Parameters.AddWithValue("@Date", rate.Date);

                                    var existingRecords = (int)checkCommand.ExecuteScalar();
                                    if (existingRecords == 0)
                                    {
                                        //Если записи нет то добавляем новую
                                        string newRecord = "INSERT INTO MonthlExchangeRates (CurrencyCode, ExchangeRate, Date) VALUES (@CurrencyCode, @ExchangeRate, @Date)";
                                        using (SqlCommand insertCommand = new SqlCommand(newRecord, connection))
                                        {
                                            insertCommand.Parameters.AddWithValue("@CurrencyCode", rate.CurrencyCode);
                                            insertCommand.Parameters.AddWithValue("@ExchangeRate", rate.ExchangeRate);
                                            insertCommand.Parameters.AddWithValue("@Date", rate.Date);

                                            insertCommand.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"В БД, запись для {rate.CurrencyCode} и {rate.Date} уже существует");
                                    }
                                }

                            }

                            foreach (var currency in currencyList)
                            {
                                //Проверка существует ли запись с такими данными
                                string checkIfQuery = "SELECT COUNT(*) FROM CurrencyRates WHERE CurrencyCode=@CurrencyCode AND Date=@Date";
                                using (SqlCommand checkCommand = new SqlCommand(checkIfQuery, connection))
                                {
                                    checkCommand.Parameters.AddWithValue("@CurrencyCode", currency.CurrencyCode);
                                    checkCommand.Parameters.AddWithValue("@ExchangeRate", currency.ExchangeRate);
                                    checkCommand.Parameters.AddWithValue("@Date", currency.Date);

                                    var existingRecords = (int)checkCommand.ExecuteScalar();
                                    if (existingRecords == 0)
                                    {
                                        //Если записи нет то добавляем новую
                                        string newRecord = "INSERT INTO CurrencyRates (CurrencyCode, ExchangeRate, Date) VALUES (@CurrencyCode, @ExchangeRate, @Date)";
                                        using (SqlCommand insertCommand = new SqlCommand(newRecord, connection))
                                        {
                                            insertCommand.Parameters.AddWithValue("@CurrencyCode", currency.CurrencyCode);
                                            insertCommand.Parameters.AddWithValue("@ExchangeRate", currency.ExchangeRate);
                                            insertCommand.Parameters.AddWithValue("@Date", currency.Date);

                                            insertCommand.ExecuteNonQuery();
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Запись для {currency.CurrencyCode} и {currency.Date} уже существует");
                                    }

                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Ошибка при получении данных: " + response1.ReasonPhrase);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при выполнении HTTP-запроса: {ex.Message}");
                }
                Console.ReadLine();
            }
        }
    }
}


