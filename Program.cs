using System.Text.Json;
using System.Net.Http;

namespace SteamUserDataGetter
{
    public static class SteamUserDataGetter
    {
        private static string Settings = "./settings.json";
        private static string APIKey = "";
        private static long SteamID = 0;
        private static string GetOwnedGamesURL = "";
        private static readonly HttpClient hc = new HttpClient();

        public static async Task<bool> IsInternetAvailable()
        {
            try
            {
                var response = await hc.GetAsync("http://api.steampowered.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Either you are not connected to the internet or the Steam website is down. Double check the following:\n* Your network connections and if you are connected to them.\n* Try going to https://store.steampowered.com/ in your web browser and seeing if it loads. If not, the issue is not on your side.\n* Additionally, Valve tends to do maintance on Tuesday evenings. If that is the case, the issue is not on your side.\n");
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        public static void SettingsInit()
        {
            if (!File.Exists(Settings))
            {
                SaveSettings();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("First Time Use: Make sure to utilize the 'settings' command to input your Steam WebAPI Key and SteamID, as these are required for the program to work.");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                string json = File.ReadAllText(Settings); // read the file as a string
                JsonDocument settings = JsonDocument.Parse(json); // parse it as a json string

                APIKey = settings.RootElement.GetProperty("APIKey").GetString();
                SteamID = settings.RootElement.GetProperty("SteamID").GetInt64();

                GetOwnedGamesURL = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={APIKey}&steamid={SteamID}&format=json&include_appinfo=true&include_played_free_games=true";

                settings.Dispose(); // end the Parse
            }
        }

        public static void UpdateSettings(string setting, string value)
        {
            switch (setting)
            {
                case "apikey":
                    APIKey = value;
                    break;
                case "steamid":
                    SteamID = int.Parse(value);
                    break;
                default:
                    break;
            }
            SaveSettings();
        }

        public static void SaveSettings()
        {
            // We write into our settings.json file a JSON object
            // This contains our settings.
            string apikey = JsonSerializer.Serialize(APIKey);
            long steamid = SteamID;
            using (StreamWriter writer = new StreamWriter(Settings))
            {
                writer.WriteLine("{");
                writer.WriteLine("\"APIKey\": " + apikey + ",");
                writer.WriteLine("\"SteamID\": " + steamid);
                writer.WriteLine("}");
                writer.Close();
            }
        }

        public static async Task<string> GetData(string url)
        {
            try
            {
                var JsonResponse = await hc.GetStringAsync(url);
                //Console.WriteLine("JSON Success");
                return JsonResponse;
            }
            catch (HttpRequestException e)
            {
                //Console.WriteLine("JSON Failure");
                //Console.WriteLine(e.Message);
                return null;
            }
        }

        public static async Task DisplayGames()
        {
            string rawdata = await GetData(GetOwnedGamesURL);
            if (rawdata != null)
            {
                Console.WriteLine();
                JsonDocument data = JsonDocument.Parse(rawdata);
                JsonElement games = data.RootElement.GetProperty("response").GetProperty("games");
                int achievementsearnedtotal = 0, achievementstotal = 0, perfectgames = 0;
                double playtimeoverall = 0;
                foreach (JsonElement game in games.EnumerateArray())
                {
                    string name = game.GetProperty("name").GetString();
                    double playtime = Math.Round(game.GetProperty("playtime_forever").GetDouble() / 60, 2);
                    playtimeoverall += playtime;
                    int appid = game.GetProperty("appid").GetInt32();
                    int achstotal = 0, achsearned = 0;
                    // We try - catch because not every game that shows up actually has achievements or stats or anything of that nature.
                    // Additionally, not every 'game' in a user's library is a valid game (some have no data other than an appid, and aren't even counted towards the game total (I believe?)).
                    try
                    {
                        string rawachdata = await GetData($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={appid}&steamid={SteamID}&key={APIKey}");
                        JsonDocument achdata = JsonDocument.Parse(rawachdata);
                        JsonElement achievements = achdata.RootElement.GetProperty("playerstats").GetProperty("achievements");
                        foreach (JsonElement achievement in achievements.EnumerateArray())
                        {
                            achstotal++;
                            achievementstotal++;
                            if (achievement.GetProperty("achieved").GetInt32() == 1)
                            {
                                achsearned++;
                                achievementsearnedtotal++;
                            }
                        }
                        double percent = Math.Round(((double)achsearned / achstotal) * 100);

                        Console.WriteLine("Game: " + name + "\nPlaytime: " + playtime + " Hours\nAchievements: " + achsearned + "/" + achstotal + " (" + percent + "%)");
                        if (percent == 100)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine("Perfect Game!");
                            Console.ForegroundColor = ConsoleColor.White;
                            perfectgames++;
                        }
                        Console.WriteLine();
                    }
                    catch // as mentioned above, not every game has achievements!
                    {
                        Console.WriteLine("Game: " + name + "\nPlaytime: " + playtime + " Hours");
                        Console.WriteLine();
                    }
                }
                int gamestotal = data.RootElement.GetProperty("response").GetProperty("game_count").GetInt32();
                Console.WriteLine("Games Total: " + gamestotal);
                Console.WriteLine("Playtime Overall: " + Math.Round(playtimeoverall, 2) + " Hours");
                Console.WriteLine("Achievements Earned / Total: " + achievementsearnedtotal + "/" + achievementstotal + " (" + Math.Round(((double)achievementsearnedtotal / achievementstotal) * 100) + "%)");
                Console.WriteLine("Perfect Games: " + perfectgames);
                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No data. Make sure your Steam ID and API Key are put in.");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static async Task DisplayAchievements() // Todo maybe
        {
        }

        public static async Task DisplayGameInventory() // Todo definitely
        {
        }

        public static async Task Main(string[] args)
        {
            SettingsInit();
            string command = "";
            Random random = new Random();
            foreach (char c in "Steam User Data Getter")
            {
                ConsoleColor co = (ConsoleColor)random.Next(0, 16);
                Console.ForegroundColor = co;
                Console.Write(c);
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\nA program for getting your Steam-related data utilizing the Steam WebAPI");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Commands: settings, games, exit, help");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
            while (command != "exit")
            {
                Console.WriteLine("Enter command: ");
                command = Console.ReadLine();
                switch (command)
                {
                    case "games":
                        if (await IsInternetAvailable() == true)
                        {
                            if (!(APIKey == "" || SteamID == 0))
                            {
                                await DisplayGames();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Either the API Key is blank or the SteamID is still 0. Please alter these via the settings command before using the program.");
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                        }
                        break;
                    case "inventory":
                        break;
                    case "achievements":
                        break;
                    case "settings":
                        Console.WriteLine("Enter setting: ");
                        string setting = Console.ReadLine();
                        Console.WriteLine("Enter value: ");
                        string value = Console.ReadLine();
                        if ((setting == "steamid" && int.TryParse(value, out int result)) || setting == "apikey")
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Updated setting " + setting + " to " + value);
                            Console.ForegroundColor = ConsoleColor.White;
                            UpdateSettings(setting, value);
                        }
                        break;
                    case "":
                    case "help":
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\nAbout:\nSteam User Data Getter is a program for getting various data involving your Steam account, mostly related to games you own." +
                            "\nAs of this time, it supports:" +
                            "\n* Steam Library Retrieval\n** Game Name\n** Playtime (in hours)\n** Achievements Earned / Total (where applicable)\n** Perfect Games" +
                            "\n\nCommands:\nexit - exits program\nhelp - this command!\nsettings - used to alter the Steam WebAPI Key and Steam ID used for this program.\ngames - display general game data\n");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case "exit":
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Exiting...");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }
            }
        }
    }
}