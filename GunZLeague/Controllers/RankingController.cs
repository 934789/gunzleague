using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GunZLeague.Data;
using GunZLeague.Models.GunZ;
using GunZLeague.Models.ViewModels;

namespace GunZLeague.Controllers
{
    public class RankingController : Controller
    {
        private const int MaxSearchLength = 24;
        private const int RankingPageSize = 10;
        private const int LatestPlayerWarLimit = 50;

        private readonly GunZDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RankingController> _logger;

        public RankingController(GunZDbContext context, IConfiguration configuration, ILogger<RankingController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // GET: /Ranking
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Ranking/Player
        public async Task<IActionResult> Player(string sort = "level", string? q = null, int page = 1)
        {
            page = Math.Clamp(page, 1, 1000);
            int pageSize = RankingPageSize;
            q = NormalizeSearchTerm(q);

            if (_configuration.GetValue<bool>("UseMockData"))
            {
                ViewBag.CurrentSort = sort;
                ViewBag.Search = q;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = 0;
                return View(new List<Character>());
            }

            var query = _context.Characters
                .Where(c => (c.DeleteFlag ?? 0) == 0)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(c => c.Name.Contains(q));
            }

            // Ordenação
            query = sort switch
            {
                "kills" => query.OrderByDescending(c => c.KillCount),
                "score" => query.OrderByDescending(c => c.XP),
                _ => query.OrderByDescending(c => c.Level)
            };

            List<Character> players;
            int totalCount;

            try
            {
                totalCount = await query.CountAsync();
                players = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Banco indisponivel. Exibindo ranking de jogadores vazio.");
                totalCount = 0;
                players = new List<Character>();
            }

            ViewBag.CurrentSort = sort;
            ViewBag.Search = q;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(players);
        }

        // GET: /Ranking/PlayerDetails/5
        public async Task<IActionResult> PlayerDetails(int id)
        {
            if (_configuration.GetValue<bool>("UseMockData"))
            {
                return NotFound();
            }

            try
            {
                var player = await _context.Characters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CID == id && (c.DeleteFlag ?? 0) == 0);

                if (player == null)
                {
                    return NotFound();
                }

                var account = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AID == player.AID);

                var login = account == null
                    ? null
                    : await _context.Logins.AsNoTracking().FirstOrDefaultAsync(l => l.AID == account.AID);

                var userGradeName = account == null
                    ? null
                    : await _context.UserGrades.AsNoTracking()
                        .Where(g => g.UGradeID == account.UGradeID)
                        .Select(g => g.Name)
                        .FirstOrDefaultAsync();

                var premiumGradeName = account == null
                    ? null
                    : await _context.PremiumGrades.AsNoTracking()
                        .Where(g => g.PGradeID == account.PGradeID)
                        .Select(g => g.Name)
                        .FirstOrDefaultAsync();

                var clanInfo = await (
                    from member in _context.ClanMembers.AsNoTracking()
                    where member.CID == id
                    join clan in _context.Clans.AsNoTracking()
                        on member.CLID equals clan.CLID
                    join grade in _context.ClanMemberGrades.AsNoTracking()
                        on (int)(member.Grade ?? 0) equals grade.GradeID into memberGrades
                    from grade in memberGrades.DefaultIfEmpty()
                    where (clan.DeleteFlag ?? 0) == 0
                    select new
                    {
                        Clan = clan,
                        GradeName = grade != null ? grade.Grade : null,
                        member.ContPoint,
                        member.RegDate
                    })
                    .FirstOrDefaultAsync();

                var model = new PlayerDetailsViewModel
                {
                    Player = player,
                    Account = account,
                    Clan = clanInfo?.Clan,
                    ClanGradeName = NormalizeClanGradeName(clanInfo?.GradeName),
                    RankName = ResolvePlayerRankName(userGradeName, premiumGradeName),
                    LastLoginAt = account?.LastLoginTime ?? login?.LastConnDate,
                    ContributionPoints = clanInfo?.ContPoint,
                    ClanJoinedAt = clanInfo?.RegDate
                };

                return View(model);
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Banco indisponivel ao carregar detalhes do jogador {CharacterId}.", id);
                return RedirectToAction(nameof(Player));
            }
        }

        // GET: /Ranking/Clan
        public async Task<IActionResult> Clan(string sort = "points", string? q = null, int page = 1)
        {
            page = Math.Clamp(page, 1, 1000);
            int pageSize = RankingPageSize;
            q = NormalizeSearchTerm(q);

            if (_configuration.GetValue<bool>("UseMockData"))
            {
                ViewBag.CurrentSort = sort;
                ViewBag.Search = q;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = 0;
                return View(new List<Clan>());
            }

            var query = _context.Clans
                .Where(c => (c.DeleteFlag ?? 0) == 0)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(c => c.Name != null && c.Name.Contains(q));
            }

            // Ordenação
            query = sort switch
            {
                "wins" => query.OrderByDescending(c => c.WinCount),
                _ => query.OrderByDescending(c => c.Point)
            };

            List<Clan> clans;
            int totalCount;

            try
            {
                totalCount = await query.CountAsync();
                clans = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Banco indisponivel. Exibindo ranking de clas vazio.");
                totalCount = 0;
                clans = new List<Clan>();
            }

            ViewBag.CurrentSort = sort;
            ViewBag.Search = q;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(clans);
        }

        // GET: /Ranking/ClanDetails/5
        public async Task<IActionResult> ClanDetails(int id)
        {
            if (_configuration.GetValue<bool>("UseMockData"))
            {
                return NotFound();
            }

            try
            {
                var clan = await _context.Clans
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CLID == id && (c.DeleteFlag ?? 0) == 0);

                if (clan == null)
                {
                    return NotFound();
                }

                var masterName = await _context.Characters
                    .AsNoTracking()
                    .Where(c => c.CID == clan.MasterCID)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync() ?? "Unknown";

                var members = await (
                    from member in _context.ClanMembers.AsNoTracking()
                    where member.CLID == id
                    join character in _context.Characters.AsNoTracking()
                        on member.CID equals character.CID into memberCharacters
                    from character in memberCharacters.DefaultIfEmpty()
                    join grade in _context.ClanMemberGrades.AsNoTracking()
                        on (int)(member.Grade ?? 0) equals grade.GradeID into memberGrades
                    from grade in memberGrades.DefaultIfEmpty()
                    select new ClanMemberViewModel
                    {
                        CID = member.CID,
                        Name = character != null ? character.Name : member.CID.ToString(),
                        Level = character != null ? character.Level : null,
                        KillCount = character != null ? character.KillCount : null,
                        DeathCount = character != null ? character.DeathCount : null,
                        GradeName = NormalizeClanGradeName(grade != null ? grade.Grade : null),
                        ContributionPoints = member.ContPoint,
                        JoinedAt = member.RegDate,
                        IsMaster = member.CID == clan.MasterCID
                    })
                    .OrderByDescending(m => m.IsMaster)
                    .ThenByDescending(m => m.ContributionPoints ?? 0)
                    .ThenByDescending(m => m.Level ?? 0)
                    .ThenBy(m => m.Name)
                    .ToListAsync();

                var model = new ClanDetailsViewModel
                {
                    Clan = clan,
                    MasterName = masterName,
                    MemberCount = members.Count,
                    Members = members
                };

                return View(model);
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Banco indisponivel ao carregar detalhes do cla {ClanId}.", id);
                return RedirectToAction(nameof(Clan));
            }
        }

        // GET: /Ranking/PlayerWars
        public async Task<IActionResult> PlayerWars(string view = "top", string? q = null, int page = 1)
        {
            page = Math.Clamp(page, 1, 1000);
            view = NormalizePlayerWarView(view);
            q = NormalizeSearchTerm(q);
            ViewBag.CurrentPlayerWarView = view;
            ViewBag.Search = q;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = 0;

            if (_configuration.GetValue<bool>("UseMockData"))
            {
                var demoLatest = Paginate(GetDemoLatestPlayerWarMatches(), page, RankingPageSize);
                var demoStandings = Paginate(FilterPlayerWarStandings(GetDemoPlayerWarStandings(), q), page, RankingPageSize);
                ViewBag.LatestPlayerWars = demoLatest.Items;
                ViewBag.TotalPages = view == "latest" ? demoLatest.TotalPages : demoStandings.TotalPages;
                return View(demoStandings.Items);
            }

            try
            {
                if (view == "latest")
                {
                    var latestWindow = await _context.PlayerWarLogs
                        .AsNoTracking()
                        .OrderByDescending(l => l.RegDate)
                        .Take(LatestPlayerWarLimit)
                        .ToListAsync();
                    var latestPageLogs = latestWindow
                        .Skip((page - 1) * RankingPageSize)
                        .Take(RankingPageSize)
                        .ToList();
                    var latestCharacterNames = await GetCharacterNameLookupAsync(latestPageLogs);
                    ViewBag.LatestPlayerWars = PlayerWarRankingBuilder.BuildLatestMatches(latestPageLogs, latestCharacterNames);
                    ViewBag.TotalPages = (int)Math.Ceiling(latestWindow.Count / (double)RankingPageSize);
                    return View(new List<PlayerWarStanding>());
                }

                var (standings, totalStandings) = await GetPlayerWarStandingsAsync(q, page, RankingPageSize);
                if (standings.Any())
                {
                    ViewBag.TotalPages = (int)Math.Ceiling(totalStandings / (double)RankingPageSize);
                    return View(standings);
                }

                var logs = await _context.PlayerWarLogs
                    .AsNoTracking()
                    .OrderByDescending(l => l.RegDate)
                    .Take(500)
                    .ToListAsync();

                var characterNames = await GetCharacterNameLookupAsync(logs);
                var fallbackStandings = Paginate(FilterPlayerWarStandings(PlayerWarRankingBuilder.Build(logs, characterNames), q), page, RankingPageSize);
                ViewBag.TotalPages = fallbackStandings.TotalPages;

                return View(fallbackStandings.Items);
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException || ex is InvalidOperationException)
            {
                _logger.LogWarning(ex, "Banco indisponivel. Exibindo Player Wars de exemplo.");
                var demoLatest = Paginate(GetDemoLatestPlayerWarMatches(), page, RankingPageSize);
                var demoStandings = Paginate(FilterPlayerWarStandings(GetDemoPlayerWarStandings(), q), page, RankingPageSize);
                ViewBag.LatestPlayerWars = demoLatest.Items;
                ViewBag.TotalPages = view == "latest" ? demoLatest.TotalPages : demoStandings.TotalPages;
                return View(demoStandings.Items);
            }
        }

        private async Task<(List<PlayerWarStanding> Items, int TotalCount)> GetPlayerWarStandingsAsync(string? search, int page, int pageSize)
        {
            var query = _context.PlayerWarCharacters
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
                });

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s => s.PlayerName.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(s => s.Points == 0 ? int.MaxValue : 0)
                .ThenByDescending(s => s.Points)
                .ThenByDescending(s => s.Wins)
                .ThenBy(s => s.PlayerName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
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

        private static string NormalizeClanGradeName(string? grade)
        {
            return grade?.Trim().ToLowerInvariant() switch
            {
                "master" => "Leader",
                "officer" => "Admin",
                "" or null => "Member",
                _ => grade
            };
        }

        private static string ResolvePlayerRankName(string? userGradeName, string? premiumGradeName)
        {
            if (!string.IsNullOrWhiteSpace(premiumGradeName) &&
                !premiumGradeName.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            {
                return premiumGradeName.Trim();
            }

            return string.IsNullOrWhiteSpace(userGradeName) ? "Normal" : userGradeName.Trim();
        }

        private static List<PlayerWarStanding> FilterPlayerWarStandings(List<PlayerWarStanding> standings, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return standings;
            }

            return standings
                .Where(s => s.PlayerName.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static (List<T> Items, int TotalPages) Paginate<T>(IEnumerable<T> items, int page, int pageSize)
        {
            var list = items.ToList();
            var totalPages = (int)Math.Ceiling(list.Count / (double)pageSize);
            var pageItems = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (pageItems, totalPages);
        }

        private static string? NormalizeSearchTerm(string? search)
        {
            search = search?.Trim();

            if (string.IsNullOrWhiteSpace(search))
            {
                return null;
            }

            return search.Length > MaxSearchLength
                ? search[..MaxSearchLength]
                : search;
        }

        private static string NormalizePlayerWarView(string? view)
        {
            return view?.Trim().ToLowerInvariant() switch
            {
                "latest" => "latest",
                _ => "top"
            };
        }

    }
}
