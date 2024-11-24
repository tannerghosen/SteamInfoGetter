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

        public static void Init()
        {
            hc.Timeout = TimeSpan.FromSeconds(10);
            SettingsInit();
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

        public static async Task<bool> IsSteamAPIUp()
        {
            try
            {
                var response = await hc.GetAsync(GetOwnedGamesURL);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
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

        // called at the start of a command to ensure we can actually do stuff with it
        public static async Task<bool> IsEverythingOK()
        {
            if (await IsSteamAPIUp() == true)
            {
                if ((APIKey == "" || SteamID == 0))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Either the API Key or Steam ID are at their default settings. Please alter these via the settings command before using the program.");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Cannot connect to Steam API. Make sure your internet is on and that you can connect to the Steam website at https://www.steampowered.com .");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                return false;
            }
        }

        public static async Task Games()
        {
            if (await IsEverythingOK() == true)
            {
                string rawdata = await GetData(GetOwnedGamesURL);
                if (rawdata != null)
                {
                    Console.WriteLine();
                    JsonDocument data = JsonDocument.Parse(rawdata);
                    JsonElement games = data.RootElement.GetProperty("response").GetProperty("games");
                    int achievementsearnedtotal = 0, achievementstotal = 0, perfectgames = 0;
                    double playtimeoverall = 0;
                    // for each element in games
                    foreach (JsonElement game in games.EnumerateArray())
                    {
                        string name = game.GetProperty("name").GetString(); // game -> name
                        double playtime = Math.Round(game.GetProperty("playtime_forever").GetDouble() / 60, 2);
                        playtimeoverall += playtime;
                        int appid = game.GetProperty("appid").GetInt32(); // game -> appid
                        int achstotal = 0, achsearned = 0;
                        // We try - catch because not every game that shows up actually has achievements or stats or anything of that nature.
                        // Additionally, not every 'game' in a user's library is a valid game (some have no data other than an appid, and aren't even counted towards the game total (I believe?)).
                        try
                        {
                            string rawachdata = await GetData($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={appid}&steamid={SteamID}&key={APIKey}");
                            JsonDocument achdata = JsonDocument.Parse(rawachdata);
                            JsonElement achievements = achdata.RootElement.GetProperty("playerstats").GetProperty("achievements");
                            // for each element in playerstats -> achievements
                            foreach (JsonElement achievement in achievements.EnumerateArray())
                            {
                                achstotal++;
                                achievementstotal++;
                                if (achievement.GetProperty("achieved").GetInt32() == 1) // achievement -> achieved
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
                    Console.WriteLine("No data.");
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
        }

        public static async Task Achievements() // Todo maybe
        {
            if (await IsEverythingOK() == true)
            {
            }
        }
        
        public static async Task Inventory() // Todo maybe???
        {
            if (await IsEverythingOK() == true)
            {
            }
        }

        public static async Task Main(string[] args)
        {
            Init();
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
                        await Games();
                        break;
                    case "achievements":
                        await Achievements();
                        break;
                    case "inventory":
                        await Inventory();
                        break;
                    case "settings":
                        Console.WriteLine("Enter setting: ");
                        string setting = Console.ReadLine();
                        Console.WriteLine("Enter value: ");
                        string value = Console.ReadLine();
                        // if setting is steam id and it's an integer OR if setting is apikey
                        if ((setting == "steamid" && int.TryParse(value, out int result)) || setting == "apikey")
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Updated setting " + setting + " to " + value);
                            Console.ForegroundColor = ConsoleColor.White;
                            UpdateSettings(setting, value);
                        }
                        break;
                    case "":
                    case null:
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