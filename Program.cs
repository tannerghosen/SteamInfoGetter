using System.Text.Json;
using System.Net.Http;
public static class SteamInfoGetter
{
    private static string Settings = "./settings.json";
    private static string APIKey = "";
    private static long SteamID = 0;
    private static string GetOwnedGamesURL = "";
    private static readonly HttpClient hc = new HttpClient();
    public static void SettingsInit()
    {
        if (!File.Exists(Settings))
        {
            SaveSettings();
        }
        else
        {
            string json = File.ReadAllText(Settings); // read the file as a string
            JsonDocument settings = JsonDocument.Parse(json); // parse it as a json string

            APIKey = settings.RootElement.GetProperty("APIKey").GetString();
            SteamID = settings.RootElement.GetProperty("SteamID").GetInt64();

            GetOwnedGamesURL = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={APIKey}&steamid={SteamID}&format=json&include_appinfo=true&include_played_free_games=true";
            //Console.WriteLine(APIKey + " " + SteamID);
            //Console.WriteLine(GetOwnedGamesURL);

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
        catch(HttpRequestException e)
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
            JsonDocument data = JsonDocument.Parse(rawdata);
            JsonElement games = data.RootElement.GetProperty("response").GetProperty("games");

            foreach (JsonElement game in games.EnumerateArray())
            {
                string name = game.GetProperty("name").GetString();
                double playtime = Math.Round(game.GetProperty("playtime_forever").GetDouble() / 60, 2);
                int appid = game.GetProperty("appid").GetInt32();
                //Console.WriteLine($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={appid}&steamid={SteamID}&key={APIKey}");
                try
                {
                    string rawachdata = await GetData($"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v1/?appid={appid}&steamid={SteamID}&key={APIKey}");
                    JsonDocument achdata = JsonDocument.Parse(rawachdata);
                    JsonElement achievements = achdata.RootElement.GetProperty("playerstats").GetProperty("achievements");
                    int achstotal = 0, achsearned = 0;
                    foreach (JsonElement achievement in achievements.EnumerateArray())
                    {
                        achstotal++;
                        if (achievement.GetProperty("achieved").GetInt32() == 1)
                        {
                            achsearned++;
                        }
                    }
                    double percent = Math.Round(((double)achsearned / achstotal) * 100);
                    Console.WriteLine("Game: " + name + "\nPlaytime: " + playtime + "\nAchievements: " + achsearned + "/" + achstotal + " (" + percent + "%)");
                    Console.WriteLine();
                }
                catch
                {
                }
            }
            //Console.WriteLine(games);
            int gamestotal = data.RootElement.GetProperty("response").GetProperty("game_count").GetInt32();
            Console.WriteLine();
            Console.WriteLine("Games Total: " + gamestotal);
        }
        else
        {
            Console.WriteLine("No data. Make sure your Steam ID and API Key are put in.");
        }
    }
    public static async Task Main(string[] args)
    {
        SettingsInit();
        string command = "";
        Console.WriteLine("Steam Info Getter");
        Console.WriteLine("Commands: settings, games, exit");
        Console.WriteLine();
        while (command != "exit")
        {
            Console.WriteLine("Enter command: ");
            command = Console.ReadLine();
            switch (command)
            {
                case "games":
                    await DisplayGames();
                    break;
                case "settings":
                    Console.WriteLine("Enter setting: ");
                    string setting = Console.ReadLine();
                    Console.WriteLine("Enter value: ");
                    string value = Console.ReadLine();
                    if ((setting == "steamid" && int.TryParse(value, out int result)) || setting == "apikey")
                    {
                        UpdateSettings(setting, value);
                    }
                    break;
                case "":
                case "exit":
                    Console.WriteLine("Exiting...");
                    break;
            }
        }
    }
}