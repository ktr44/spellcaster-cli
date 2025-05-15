using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;
using System.IO;
using System.Text;
using DotNetEnv;
using System.Collections.Generic;

namespace SpellcasterCLI
{
    class Program
    {
        private static Dictionary<string, string> apiKeys;
        private static Dictionary<string, Func<Task>> menuActions;
        private static string modeleIA = "gpt-4o-mini";

        static async Task Main()
        {
            DotNetEnv.Env.Load(); // Variable d'environnement
            apiKeys = new Dictionary<string, string>
            {
                { "openAiKey", Environment.GetEnvironmentVariable("OPENAI_API_KEY") },
                { "weatherApiKey", Environment.GetEnvironmentVariable("METEO_API_KEY") }
            };

            menuActions = new Dictionary<string, Func<Task>>
            {
                { "1", VerifierOrthographe },
                { "2", TraduireTexte },
                { "3", AfficherMeteoEtCreerHTML },
                { "4", QuitterApplication }
            };

            bool enCours = true;
            while (enCours)
            {
                Console.WriteLine("\n=== SPELLCASTER CLI ===");
                Console.WriteLine("Choisissez une option ");
                Console.WriteLine("1 : Vérificateur d'orthographe");
                Console.WriteLine("2 : Traduction US ou UK");
                Console.WriteLine("3 : Météo & Génération HTML");
                Console.WriteLine("4 : Quitter");
                Console.Write("Votre choix : ");
                string choix = Console.ReadLine();

                if (menuActions.ContainsKey(choix))
                {
                    await menuActions[choix]();
                    if (choix == "4") enCours = false;
                }
                else
                {
                    Console.WriteLine("Option non reconnue.");
                }

                Console.WriteLine();
            }
        }

        static async Task VerifierOrthographe()
        {
            Console.WriteLine("Entrez votre texte en français : ");
            string texteUtilisateur = Console.ReadLine();
            string texteCorrige = await EnvoyerRequeteIA("Corrige l'orthographe et la grammaire du texte suivant en français, en retournant uniquement le texte corrigé : " + texteUtilisateur);
            Console.WriteLine("\nTexte corrigé : " + texteCorrige);
        }

        static async Task TraduireTexte()
        {
            Console.WriteLine("Entrez le texte en français à traduire : ");
            string texteUtilisateur = Console.ReadLine();
            Console.WriteLine("\nChoisissez la traduction  (1) Anglais US, (2) Anglais UK");
            string choixLangue = Console.ReadLine();
            string langueCible;
            if (choixLangue == "2")
            {
                langueCible = "anglais britannique";
            }
            else
            {
                langueCible = "anglais américain";
            }

            string texteTraduit = await EnvoyerRequeteIA("Traduis ce texte du français vers l'" + langueCible + ", en retournant uniquement la traduction : " + texteUtilisateur);
            Console.WriteLine("\nTexte traduit : " + texteTraduit);
        }

        static async Task AfficherMeteoEtCreerHTML()
        {
            Console.Write("Entrez une ville : ");
            string ville = Console.ReadLine();
            using HttpClient client = new HttpClient();
            string url = "https://api.openweathermap.org/data/2.5/weather?q=" + ville + "&appid=" + apiKeys["weatherApiKey"] + "&units=metric&lang=fr";

            try
            {
                string responseBody = await client.GetStringAsync(url);
                var donneesMeteo = JsonSerializer.Deserialize<JsonElement>(responseBody);
                if (!donneesMeteo.TryGetProperty("weather", out JsonElement meteo) || meteo.GetArrayLength() == 0)
                {
                    Console.WriteLine("Ville introuvable ou erreur API.");
                    return;
                }

                string descriptionMeteo = meteo[0].GetProperty("description").GetString();
                string temperature = donneesMeteo.GetProperty("main").GetProperty("temp").ToString();
                Console.WriteLine("Météo : " + descriptionMeteo);
                Console.WriteLine("Température : " + temperature + "°C");
                CreerFichierHTML(ville, descriptionMeteo, temperature);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur : " + ex.Message);
            }
        }

        static void CreerFichierHTML(string ville, string descriptionMeteo, string temperature)
        {
            string contenuHTML = "<!DOCTYPE html>\n<html lang=\"fr\">\n<head>\n<meta charset=\"UTF-8\">\n<title>Météo à " + ville + "</title>\n<style>\nbody {\n    font-family: Arial, sans-serif;\n    text-align: center;\n    background: linear-gradient(120deg, #6EC6FF, #2196F3);\n    color: white;\n    margin: 20px;\n}\n.weather-container {\n    display: inline-block;\n    background: rgba(255, 255, 255, 0.2);\n    padding: 20px;\n    border-radius: 15px;\n}\n</style>\n</head>\n<body>\n<h1>Météo à " + ville + "</h1>\n<div class=\"weather-container\">\n    <div class=\"icon\">" + AssocierIconeMeteo(descriptionMeteo) + "</div>\n    <div class=\"temperature\">" + temperature + "°C</div>\n    <div class=\"description\">" + descriptionMeteo + "</div>\n</div>\n</body>\n</html>";

            string nomFichier = ville + "_meteo.html";
            File.WriteAllText(nomFichier, contenuHTML);
            Console.WriteLine("Fichier HTML généré : " + nomFichier);
        }

        static string AssocierIconeMeteo(string descriptionMeteo)
        {
            var iconesMeteo = new Dictionary<string, string>
            {
                { "soleil", "images/01.png" },
                { "clair", "images/01.png" },
                { "nuage", "images/02.png" },
                { "couvert", "images/02.png" },
                { "pluie", "images/04.png" },
                { "orage", "images/05.png" },
                { "neige", "images/06.png" },
                { "brouillard", "images/03.png" }
            };

            foreach (var condition in iconesMeteo)
            {
                if (descriptionMeteo.Contains(condition.Key))
                {
                    return "<img src='" + condition.Value + "' alt='" + descriptionMeteo + "' />";
                }
            }

            return "<p>Icône indisponible</p>";
        }

        static async Task<string> EnvoyerRequeteIA(string message)
        {
            try
            {
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKeys["openAiKey"]);

                string json = $"{{\"model\":\"{modeleIA}\",\"messages\":[{{\"role\":\"user\",\"content\":\"{message}\"}}]}}";
                var contenu = new StringContent(json, Encoding.UTF8, "application/json");

                var reponse = await client.PostAsync("https://api.openai.com/v1/chat/completions", contenu);
                string texte = await reponse.Content.ReadAsStringAsync();

                var doc = JsonDocument.Parse(texte);
                var choix = doc.RootElement.GetProperty("choices")[0];
                string texteCorrige = choix.GetProperty("message").GetProperty("content").GetString();

                return texteCorrige;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erreur : " + ex.Message);
                return "Erreur lors de la correction.";
            }
        }
        static async Task QuitterApplication()
        {
            Console.WriteLine("Merci d'avoir utilisé l'application. À bientôt ");
            await Task.CompletedTask;
        }
    }
}
