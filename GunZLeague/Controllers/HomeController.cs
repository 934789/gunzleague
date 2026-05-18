using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GunZLeague.Data;
using GunZLeague.Models;
using GunZLeague.Models.GunZ;

namespace GunZLeague.Controllers;

public class HomeController : Controller
{
    private readonly GunZDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HomeController> _logger;

    public HomeController(GunZDbContext context, IConfiguration configuration, ILogger<HomeController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        if (_configuration.GetValue<bool>("UseMockData"))
        {
            SetDemoHomeData();

            return View(GetSiteNews());
        }

        try
        {
            ViewBag.TotalPlayers = await _context.Characters
                .Where(c => (c.DeleteFlag ?? 0) == 0)
                .CountAsync();

            ViewBag.TotalClans = await _context.Clans
                .Where(c => (c.DeleteFlag ?? 0) == 0)
                .CountAsync();

            ViewBag.TopPlayers = await _context.Characters
                .AsNoTracking()
                .Where(c => (c.DeleteFlag ?? 0) == 0)
                .OrderByDescending(c => c.Level)
                .ThenByDescending(c => c.XP)
                .Take(12)
                .ToListAsync();

            ViewBag.TopClans = await _context.Clans
                .AsNoTracking()
                .Where(c => (c.DeleteFlag ?? 0) == 0)
                .OrderByDescending(c => c.Point)
                .ThenByDescending(c => c.WinCount)
                .Take(12)
                .ToListAsync();

            try
            {
                var latestPlayerWarLogs = await _context.PlayerWarLogs
                    .AsNoTracking()
                    .OrderByDescending(l => l.RegDate)
                    .Take(8)
                    .ToListAsync();
                var latestPlayerWarCharacterNames = await GetCharacterNameLookupAsync(latestPlayerWarLogs);
                ViewBag.LatestPlayerWars = PlayerWarRankingBuilder.BuildLatestMatches(latestPlayerWarLogs, latestPlayerWarCharacterNames);

                var playerWarStandings = await GetPlayerWarStandingsAsync();
                if (!playerWarStandings.Any())
                {
                    var rankingPlayerWarLogs = await _context.PlayerWarLogs
                        .AsNoTracking()
                        .OrderByDescending(l => l.RegDate)
                        .Take(200)
                        .ToListAsync();

                    var characterNames = await GetCharacterNameLookupAsync(rankingPlayerWarLogs);
                    playerWarStandings = PlayerWarRankingBuilder.Build(rankingPlayerWarLogs, characterNames).Take(8).ToList();
                }

                ViewBag.TopPlayerWars = playerWarStandings.Take(8).ToList();
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Player Wars indisponivel. Exibindo dados de exemplo.");
                ViewBag.TopPlayerWars = GetDemoPlayerWarStandings();
                ViewBag.LatestPlayerWars = GetDemoLatestPlayerWarMatches();
            }

            return View(GetSiteNews());
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
        {
            _logger.LogWarning(ex, "Banco indisponivel. Exibindo Home com dados de desenvolvimento.");

            SetDemoHomeData();

            return View(GetSiteNews());
        }
    }

    private void SetDemoHomeData()
    {
        var topPlayers = new List<Character>
        {
            new() { Name = "DashMaster", Level = 85, KillCount = 1240, DeathCount = 620, XP = 985000 },
            new() { Name = "KStylePro", Level = 79, KillCount = 1105, DeathCount = 710, XP = 846200 },
            new() { Name = "Reload", Level = 72, KillCount = 932, DeathCount = 540, XP = 710400 },
            new() { Name = "SilentShot", Level = 68, KillCount = 800, DeathCount = 590, XP = 655100 },
            new() { Name = "Aerial", Level = 63, KillCount = 760, DeathCount = 610, XP = 590300 },
            new() { Name = "BladeRun", Level = 59, KillCount = 690, DeathCount = 520, XP = 501900 },
            new() { Name = "North", Level = 55, KillCount = 602, DeathCount = 488, XP = 462100 },
            new() { Name = "ZeroDelay", Level = 52, KillCount = 580, DeathCount = 470, XP = 431700 },
            new() { Name = "Rush", Level = 48, KillCount = 530, DeathCount = 450, XP = 390400 },
            new() { Name = "Nexus", Level = 44, KillCount = 490, DeathCount = 430, XP = 340200 }
        };

        var topClans = new List<Clan>
        {
            new() { Name = "Eclipse", Point = 4320, WinCount = 188, LoseCount = 61 },
            new() { Name = "Reapers", Point = 3980, WinCount = 171, LoseCount = 74 },
            new() { Name = "Vortex", Point = 3510, WinCount = 145, LoseCount = 88 },
            new() { Name = "Legacy", Point = 3090, WinCount = 132, LoseCount = 92 },
            new() { Name = "Phantom", Point = 2740, WinCount = 119, LoseCount = 97 },
            new() { Name = "Nova", Point = 2480, WinCount = 104, LoseCount = 84 },
            new() { Name = "Origin", Point = 2210, WinCount = 96, LoseCount = 91 },
            new() { Name = "Pulse", Point = 1980, WinCount = 86, LoseCount = 89 },
            new() { Name = "Atlas", Point = 1740, WinCount = 72, LoseCount = 80 },
            new() { Name = "Ion", Point = 1510, WinCount = 64, LoseCount = 77 }
        };

        ViewBag.TopPlayers = topPlayers;
        ViewBag.TopClans = topClans;
        ViewBag.TopPlayerWars = GetDemoPlayerWarStandings();
        ViewBag.LatestPlayerWars = GetDemoLatestPlayerWarMatches();
        ViewBag.TotalPlayers = topPlayers.Count;
        ViewBag.TotalClans = topClans.Count;
    }

    private static List<PlayerWarStanding> GetDemoPlayerWarStandings()
    {
        return new List<PlayerWarStanding>
        {
            new() { PlayerName = "DashMaster", Points = 24, Wins = 8, Losses = 1 },
            new() { PlayerName = "Reload", Points = 19, Wins = 6, Losses = 1 },
            new() { PlayerName = "Aerial", Points = 16, Wins = 5, Losses = 1 },
            new() { PlayerName = "North", Points = 13, Wins = 4, Losses = 1 },
            new() { PlayerName = "ZeroDelay", Points = 10, Wins = 3, Losses = 1 }
        };
    }

    private static List<PlayerWarMatchSummary> GetDemoLatestPlayerWarMatches()
    {
        return new List<PlayerWarMatchSummary>
        {
            new() { Winners = "DashMaster, Reload", Losers = "Aerial, North", WinnerPlayers = new[] { "DashMaster", "Reload" }, LoserPlayers = new[] { "Aerial", "North" }, WinScore = 4, LoseScore = 1, RoundScore = "4-1", MapID = 5, MapName = PlayerWarMapResolver.GetName(5), PlayedAt = DateTime.Now.AddMinutes(-18) },
            new() { Winners = "ZeroDelay, Rush, Nexus, BladeRun", Losers = "SilentShot, Aerial, North, Ion", WinnerPlayers = new[] { "ZeroDelay", "Rush", "Nexus", "BladeRun" }, LoserPlayers = new[] { "SilentShot", "Aerial", "North", "Ion" }, WinScore = 4, LoseScore = 3, RoundScore = "4-3", MapID = 0, MapName = PlayerWarMapResolver.GetName(0), PlayedAt = DateTime.Now.AddMinutes(-42) },
            new() { Winners = "SilentShot", Losers = "DashMaster", WinnerPlayers = new[] { "SilentShot" }, LoserPlayers = new[] { "DashMaster" }, WinScore = 4, LoseScore = 0, RoundScore = "4-0", MapID = 13, MapName = PlayerWarMapResolver.GetName(13), PlayedAt = DateTime.Now.AddHours(-1) },
            new() { Winners = "Reload, Nexus, Aerial", Losers = "Rush, BladeRun, North", WinnerPlayers = new[] { "Reload", "Nexus", "Aerial" }, LoserPlayers = new[] { "Rush", "BladeRun", "North" }, WinScore = 4, LoseScore = 2, RoundScore = "4-2", MapID = 20, MapName = PlayerWarMapResolver.GetName(20), PlayedAt = DateTime.Now.AddHours(-2) }
        };
    }

    private async Task<Dictionary<int, string>> GetCharacterNameLookupAsync(IEnumerable<PWGameLog> logs)
    {
        var characterIds = PlayerWarRankingBuilder.ExtractCharacterIds(logs);
        if (characterIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _context.Characters
            .AsNoTracking()
            .Where(c => (c.DeleteFlag ?? 0) == 0 && characterIds.Contains(c.CID))
            .Select(c => new { c.CID, c.Name })
            .ToDictionaryAsync(c => c.CID, c => c.Name);
    }

    private async Task<List<PlayerWarStanding>> GetPlayerWarStandingsAsync()
    {
        return await _context.PlayerWarCharacters
            .AsNoTracking()
            .Where(p => p.Score > 0 || p.Wins > 0 || p.Losses > 0 || p.Draws > 0)
            .GroupJoin(
                _context.Characters.AsNoTracking(),
                p => p.CID,
                c => c.CID,
                (p, characters) => new { PlayerWar = p, Character = characters.FirstOrDefault() })
            .Select(x => new PlayerWarStanding
            {
                CharacterId = x.PlayerWar.CID,
                PlayerName = x.PlayerWar.Name ?? (x.Character != null ? x.Character.Name : x.PlayerWar.CID.ToString()),
                Points = x.PlayerWar.Score,
                Wins = x.PlayerWar.Wins,
                Losses = x.PlayerWar.Losses
            })
            .OrderBy(s => s.Points == 0 ? int.MaxValue : 0)
            .ThenByDescending(s => s.Points)
            .ThenByDescending(s => s.Wins)
            .ThenBy(s => s.PlayerName)
            .Take(100)
            .ToListAsync();
    }

    private static List<News> GetSiteNews()
    {
        return new List<News>();
    }

    public IActionResult Download()
    {
        return View();
    }

    public IActionResult Donate()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
