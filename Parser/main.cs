using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AngleSharp;
using AngleSharp.Dom;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
//using OpenQA.Selenium.Firefox; // Запасной вариант перехода на Firefox //
using System.Data.SQLite;
using Newtonsoft.Json;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Reflection.Metadata;
using System.Drawing; 

class Program
{
    static void Main()
    {
        // Чтение ссылок из файла
        var links = LoadLinksFromDatabase();

        // Массив User Agent
        var userAgents = new List<string>
    {
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/115.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36"
    };

        // Инициализация веб-драйвера
        foreach (var link in links)
        {
            var domain = GetDomainFromUrl(link.Url);
            if (string.IsNullOrEmpty(domain))
            {
                Console.WriteLine($"Error: Domain is empty for link {link.Url}");
                continue;
            }

            var randomUserAgent = userAgents[new Random().Next(userAgents.Count)];
            var options = new ChromeOptions();

            // Set the path to the Chrome executable
            //options.BinaryLocation = @"C:\Users\SC12312\AppData\Local\Mozilla Firefox\firefox.exe";

            // Установка случайного User-Agent
            //options.AddArgument($"--user-agent={randomUserAgent}");

            // Исключение опций, связанных с обнаружением Selenium
            options.AddArgument("--disable-blink-features=AutomationControlled");
            options.AddArgument("--disable-blink-features");
            // Не надо использовать headless, потому что некоторые сайты блокируют буструю сборку данных
            //options.AddArgument("--headless");
            options.AddArgument("--ignore-certificate-errors");
            options.AddArgument("--incognito");
            options.AddArgument("start-maximized");
            options.AddArgument("--disable-extensions");
            options.AddArgument("--disable-plugins-discovery");
            options.AddArguments("--remote-allow-origins=*");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            using (var driver = new ChromeDriver(options))
            //using (var driver = new FirefoxDriver()) // Запасной вариант перехода на Firefox //
            {
                driver.Navigate().GoToUrl(link.Url);
                driver.Manage().Window.Size = new Size(new Random().Next(800, 1200), new Random().Next(600, 900));

                // Подождем 5 секунд
                Thread.Sleep(6000);

                // Парсинг данных
                var parser = GetParser(domain);
                var result = parser.Parse(driver.PageSource);

                // Проверка на null перед сохранением
                if (result != null)
                {
                    // Сохранение данных в SQLite
                    SaveToSQLite(result);

                    // Сохранение данных в JSON
                    SaveToJson(result);
                }
                else
                {
                    Console.WriteLine("Error: Result is null after parsing");
                }

                // Очистка кук, кеша и local storage
                driver.Manage().Cookies.DeleteAllCookies();
                driver.ExecuteScript("window.localStorage.clear()");
                driver.ExecuteScript("window.sessionStorage.clear()");
            }
        }
    }
    static string GetDomainFromUrl(string url)
    {
        try
        {
            Uri uri = new Uri(url);
            string host = uri.Host.ToLower(); // Приведем к нижнему регистру //
            if (host.StartsWith("www."))
            {
                host = host.Substring(4); // Удалим префикс "www." //
            }
            return host;
        }
        catch (UriFormatException)
        {
            Console.WriteLine($"Error: Invalid URL format - {url}");
            return null;
        }
    }
    static List<Link> LoadLinksFromDatabase()
    {
        var links = new List<Link>();

        using (var connection = new SQLiteConnection("Data Source=links.db"))
        {
            connection.Open();

            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = "SELECT partner, url FROM links";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var partner = reader["partner"].ToString();
                        var url = reader["url"].ToString();
                        links.Add(new Link { Partner = partner, Url = url });
                    }
                }
            }
        }

        return links;
    }

    static IParser GetParser(string domain)
    {
        switch (domain)
        {
            case "366.ru":
                return new Parser366();
            case "eapteka.ru":
                return new ParserEapteka();
            case "ozon.ru":
                return new ParserOzon();
            case "zdravcity.ru":
                return new ParserZdravCity();
            case "polza.ru":
                return new ParserPolza();
            case "planetazdorovo.ru":
                return new ParserPlanetaZdorovo();
            case "rigla.ru":
                return new ParserRigla();
            case "apteka.ru":
                return new ParserApteka();
            default:
                throw new NotImplementedException($"Parser for {domain} is not implemented");
        }
    }

    static void SaveToSQLite(Result result)
    {
        if (result == null)
        {
            Console.WriteLine("Error: Result is null");
            return;
        }

        using (var connection = new SQLiteConnection("Data Source=ecom_data.db"))
        {
            connection.Open();

            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = "CREATE TABLE IF NOT EXISTS Results (Partner TEXT, Name TEXT, Price REAL, Date DATETIME)";
                command.ExecuteNonQuery();

                command.CommandText = "INSERT INTO Results (Partner, Name, Price, Date) VALUES (@Partner, @Name, @Price, @Date)";
                command.Parameters.AddWithValue("@Partner", result.Partner);
                command.Parameters.AddWithValue("@Name", result.Name);
                command.Parameters.AddWithValue("@Price", result.Price);
                command.Parameters.AddWithValue("@Date", result.Date);

                command.ExecuteNonQuery();
            }
        }
    }

    static void SaveToJson(Result result)
    {
        if (result == null)
        {
            Console.WriteLine("Error: Result is null");
            return;
        }

        // Чтение существующего JSON-файла, если он существует
        List<Result> existingResults;
        try
        {
            string existingJson = File.ReadAllText("result.json");
            existingResults = JsonConvert.DeserializeObject<List<Result>>(existingJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading existing JSON file: {ex.Message}");
            existingResults = new List<Result>();
        }

        // Добавление нового результата в список
        existingResults.Add(result);

        // Сериализация и запись обновленного списка в файл
        try
        {
            var updatedJson = JsonConvert.SerializeObject(existingResults, Formatting.Indented);
            File.WriteAllText("result.json", updatedJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to JSON file: {ex.Message}");
        }
    }

}

class Link
{
    public string Partner { get; set; }
    public string Url { get; set; }
}

class Result
{
    public string Partner { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime Date { get; set; }
}

interface IParser
{
    Result Parse(string html);
}

class Parser366 : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var nameElement = document.QuerySelector("h1[itemprop='name']");
        var priceElement = document.QuerySelector("meta[itemprop='price']");

        return new Result
        {
            Partner = "366.ru",
            Name = nameElement?.TextContent.Trim(),
            Price = Convert.ToDecimal(priceElement?.GetAttribute("content"), CultureInfo.InvariantCulture),
            Date = DateTime.Now
        };
    }
}

class ParserEapteka : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var nameElement = document.QuerySelector("meta[itemprop='name']");
        var priceElement = document.QuerySelector("meta[itemprop='price']");

        return new Result
        {
            Partner = "eapteka.ru",
            Name = nameElement?.GetAttribute("content")?.Trim(),
            Price = Convert.ToDecimal(priceElement?.GetAttribute("content")),
            Date = DateTime.Now
        };
    }
}

class ParserOzon : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var titleElement = document.QuerySelector("title");
        var priceElement = document.QuerySelector("div.ml7.m6l span.ln.l8m");

        if (titleElement != null && priceElement != null)
        {
            var priceText = priceElement?.TextContent.Trim();
            var cleanedPriceText = Regex.Replace(priceText, @"[^\d.,]", "");

            // Проверка, не являются ли элементы null
            if (titleElement != null && priceElement != null)
            {
                return new Result
                {
                    Partner = "ozon.ru",
                    Name = titleElement.TextContent.Trim(),
                    Price = Convert.ToDecimal(cleanedPriceText, CultureInfo.InvariantCulture),
                    Date = DateTime.Now
                };
            }
            else
            {
                // Обработка случая, если элементы не найдены
                Console.WriteLine("Error: titleElement or priceElement is null");
                return null; // или возврат значения по умолчанию или другое действие, в зависимости от вашей логики
            }
        }
        else
        {
            // Обработка случая, если элементы не найдены
            Console.WriteLine("Error: titleElement or priceElement is null");
            return null; // или возврат значения по умолчанию или другое действие, в зависимости от вашей логики
        }
    }
}

class ParserZdravCity : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var titleElement = document.QuerySelector("title");
        var priceElement = document.QuerySelector("span.Price_price-prefix__hfnzr");

        if (priceElement != null && priceElement.NextSibling != null)
        {
            var rawPriceText = priceElement.NextSibling.TextContent.Trim();

            // Удаление символа '₽' и пробелов
            rawPriceText = rawPriceText.Replace("₽", "").Replace(" ", "");

            // Попытка преобразования в десятичное число
            if (decimal.TryParse(rawPriceText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
            {
                return new Result
                {
                    Partner = "zdravcity.ru",
                    Name = titleElement?.TextContent.Trim(),
                    Price = price,
                    Date = DateTime.Now
                };
            }
            else
            {
                Console.WriteLine($"Error: Unable to parse price '{rawPriceText}'");
            }
        }
        else
        {
            Console.WriteLine("Error: priceElement or priceElement.NextSibling is null");
        }

        return null;
    }
}

class ParserPolza : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var nameElement = document.QuerySelector("meta[itemprop='name']");
        var priceElement = document.QuerySelector("meta[itemprop='price']");

        return new Result
        {
            Partner = "polza.ru",
            Name = nameElement?.GetAttribute("content")?.Trim(),
            Price = Convert.ToDecimal(priceElement?.GetAttribute("content")),
            Date = DateTime.Now
        };
    }
}

class ParserPlanetaZdorovo : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var nameElement = document.QuerySelector("span[itemprop='name']");
        var priceElement = document.QuerySelector("meta[itemprop='price']");

        return new Result
        {
            Partner = "planetazdorovo.ru",
            Name = nameElement?.TextContent.Trim(),
            Price = Convert.ToDecimal(priceElement?.GetAttribute("content")),
            Date = DateTime.Now
        };
    }
}

class ParserRigla : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var nameElement = document.QuerySelector("div[itemtype='https://schema.org/Product'] meta[itemprop='name']");
        var priceElement = document.QuerySelector("meta[itemprop='price']");

        if (priceElement != null)
        {
            var priceString = priceElement.GetAttribute("content")?.Trim();

            if (!string.IsNullOrEmpty(priceString))
            {
                // Try parsing with InvariantCulture
                if (decimal.TryParse(priceString, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                {
                    return new Result
                    {
                        Partner = "rigla.ru",
                        Name = nameElement?.GetAttribute("content")?.Trim(),
                        Price = price,
                        Date = DateTime.Now
                    };
                }
                else
                {
                    Console.WriteLine($"Error: Unable to parse price '{priceString}' with InvariantCulture");
                }

                // Try parsing with CurrentCulture
                if (decimal.TryParse(priceString, out price))
                {
                    return new Result
                    {
                        Partner = "rigla.ru",
                        Name = nameElement?.GetAttribute("content")?.Trim(),
                        Price = price,
                        Date = DateTime.Now
                    };
                }
                else
                {
                    Console.WriteLine($"Error: Unable to parse price '{priceString}' with CurrentCulture");
                }
            }
            else
            {
                Console.WriteLine("Error: priceString is empty");
            }
        }
        else
        {
            Console.WriteLine("Error: priceElement is null");
        }

        return null;
    }
}

class ParserApteka : IParser
{
    public Result Parse(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = context.OpenAsync(req => req.Content(html)).Result;

        var nameElement = document.QuerySelector("h1[itemprop='name']");
        var priceElement = document.QuerySelector("meta[itemprop='price']");

        return new Result
        {
            Partner = "apteka.ru",
            Name = nameElement?.TextContent.Trim(),
            Price = Convert.ToDecimal(priceElement?.GetAttribute("content")),
            Date = DateTime.Now
        };
    }
}

