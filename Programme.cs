using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.IO;
using DotNetEnv;

namespace AICommandLineApp
{
    class Program
    {
        private static string OpenAIApiKey;
        private static string MeteoApiKey;
        private static string modele = "gpt-4o-mini";

        static async Task Main()
        {
            bool continuer = true;
            while (continuer)
            {
                Console.WriteLine("=== SPELLCASTER CLI ===");
                Console.WriteLine("Choisissez une option :");
                Console.WriteLine("1 : Vérificateur d'orthographe");
                Console.WriteLine("2 : Traduction US ou UK");
                Console.WriteLine("3 : Météo & Génération HTML");
                Console.WriteLine("4 : Quitter");
                Console.Write("Votre choix : ");
                string choix = Console.ReadLine() ?? "";

                switch (choix)
                {
                    case "1":
                        await CorrectionTexte();
                        break;
                    case "2":
                        await TraductionTexte();
                        break;
                    case "3":
                        await ObtenirMeteoEtGenererHTML();
                        break;
                    case "4":
                        continuer = false;
                        break;
                    default:
                        Console.WriteLine("Option non reconnue.");
                        break;
                }

                Console.WriteLine();
            }

            Console.WriteLine("Merci d'avoir utilisé l'application. Au revoir !");
        }

        static async Task CorrectionTexte()
        {
            Console.WriteLine("Entrez le texte en français :");
            string userText = Console.ReadLine() ?? "";

            string correctedText = await EnvoyerRequeteIA($"Corrige l'orthographe et la grammaire du texte suivant en français, en retournant uniquement le texte corrigé : {userText}");
            Console.WriteLine("\nTexte corrigé : " + correctedText);
        }

        static async Task TraductionTexte()
        {
            Console.WriteLine("Entrez le texte en français à traduire :");
            string userText = Console.ReadLine() ?? "";

            Console.WriteLine("\nChoisissez la traduction : (1) Anglais US, (2) Anglais UK");
            string option = Console.ReadLine() ?? "";
            string targetLang = option == "2" ? "anglais britannique" : "anglais américain";

            string translatedText = await EnvoyerRequeteIA($"Traduis ce texte du français vers l'{targetLang}, en retournant uniquement la traduction : {userText}");
            Console.WriteLine("\nTexte traduit : " + translatedText);
        }

        static async Task ObtenirMeteoEtGenererHTML()
        {
            Console.Write("Entrez une Ville : ");
            string ville = Console.ReadLine() ?? "";

            using HttpClient client = new HttpClient();
            string url = $"https://api.openweathermap.org/data/2.5/weather?q={ville}&appid={MeteoApiKey}&units=metric";

            try
            {
                string responseBody = await client.GetStringAsync(url);
                var weatherData = JsonSerializer.Deserialize<JsonElement>(responseBody);

                if (!weatherData.TryGetProperty("weather", out JsonElement meteo) || meteo.GetArrayLength() == 0)
                {
                    Console.WriteLine("Ville introuvable ou erreur API.");
                    return;
                }

                string description = meteo[0].GetProperty("description").GetString() ?? "Non disponible";
                string temperature = weatherData.GetProperty("main").GetProperty("temp").ToString();

                Console.WriteLine($"Météo : {description}");
                Console.WriteLine($"Température : {temperature}°C");

                GenererFichierHTML(ville, description, temperature);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur : {ex.Message}");
            }
        }

        static void GenererFichierHTML(string ville, string description, string temperature)
        {
            string htmlContent = $@"<!DOCTYPE html>
<html lang=""fr"">
<head>
    <meta charset=""UTF-8"">
    <title>Météo à {ville}</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            text-align: center;
            background: linear-gradient(120deg, #6EC6FF, #2196F3);
            color: white;
            margin: 20px;
        }}
        h1 {{
            margin-top: 20px;
            font-size: 28px;
        }}
        .weather-container {{
            display: inline-block;
            background: rgba(255, 255, 255, 0.2);
            padding: 20px;
            border-radius: 15px;
            box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);
            width: 300px;
        }}
        .icon {{
            font-size: 50px;
            margin-bottom: 10px;
        }}
        .temperature {{
            font-size: 24px;
            font-weight: bold;
        }}
        .description {{
            font-size: 18px;
            margin-top: 10px;
        }}
    </style>
</head>
<body>
    <h1>Météo à {ville}</h1>
    <div class=""weather-container"">
        <div class=""icon"">{ObtenirIcôneMétéo(description)}</div>
        <div class=""temperature"">{temperature}°C</div>
        <div class=""description"">{description}</div>
    </div>
</body>
</html>";

            string fileName = $"{ville}_meteo.html";
            File.WriteAllText(fileName, htmlContent);
            Console.WriteLine($"✅ Fichier HTML généré : {fileName}");
        }

        static string ObtenirIcôneMétéo(string description)
        {
            description = description.ToLower();

            if (description.Contains("soleil") || description.Contains("clair"))
                return "☀️";
            if (description.Contains("nuage"))
                return "☁️";
            if (description.Contains("pluie"))
                return "🌧️";
            if (description.Contains("orage"))
                return "⛈️";
            if (description.Contains("neige"))
                return "❄️";

            return "";
        }
        static async Task<string> EnvoyerRequeteIA(string prompt)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {OpenAIApiKey}");
                var requestBody = new
                {
                    model = modele,
                    messages = new[]
                    {
                        new { role = "system", content = "Tu es un assistant de correction et de traduction." },
                        new { role = "user", content = prompt }
                    }
                };

                string jsonBody = JsonSerializer.Serialize(requestBody);
                var response = await client.PostAsync(
                    "https://api.openai.com/v1/chat/completions",
                    new StringContent(jsonBody, Encoding.UTF8, "application/json")
                );

                if (response.IsSuccessStatusCode)
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(responseContent);
                    return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
                }
                else
                {
                    return "Erreur lors de la requête à l'API.";
                }
            }
        }
    }
}