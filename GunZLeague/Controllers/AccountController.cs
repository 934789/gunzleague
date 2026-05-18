using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using GunZLeague.Data;
using GunZLeague.Models.GunZ;
using GunZLeague.Models.ViewModels;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace GunZLeague.Controllers
{
    public class AccountController : Controller
    {
        private const int DemoAccountId = -1;
        private const string DemoUsername = "demo";
        private const string DemoPassword = "demo123";
        private const long MaxClanEmblemBytes = 256 * 1024;
        private const int ClanEmblemSize = 64;

        private readonly GunZDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IDataProtector _passwordResetProtector;

        public AccountController(
            GunZDbContext context,
            IConfiguration configuration,
            ILogger<AccountController> logger,
            IWebHostEnvironment environment,
            IDataProtectionProvider dataProtectionProvider)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _environment = environment;
            _passwordResetProtector = dataProtectionProvider.CreateProtector("GunZLeague.PasswordReset.v1");
        }

        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login(string userId, string password, string? returnUrl = null)
        {
            userId = userId?.Trim() ?? string.Empty;

            if (!IsAcceptableLoginInput(userId, password))
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            if (IsDemoModeEnabled() &&
                string.Equals(userId, DemoUsername, StringComparison.OrdinalIgnoreCase) &&
                password == DemoPassword)
            {
                HttpContext.Session.Clear();
                HttpContext.Session.SetInt32("UserId", DemoAccountId);
                HttpContext.Session.SetString("UserName", DemoUsername);

                return RedirectToAction("Index", "Home");
            }

            var login = await _context.Logins
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserID == userId && l.Password == password);

            var account = login == null
                ? null
                : await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AID == login.AID && a.UGradeID >= 0 && (a.Status == null || a.Status != "B"));

            if (account == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32("UserId", account.AID);
            HttpContext.Session.SetString("UserName", account.UserID);

            _logger.LogInformation("User {UserId} logged in successfully.", userId);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Register()
        {
            return View();
        }

        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Email == email && a.UGradeID >= 0 && (a.Status == null || a.Status != "B"));

            if (account != null)
            {
                var login = await _context.Logins
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.AID == account.AID && l.UserID == account.UserID);

                if (login != null)
                {
                    var token = CreatePasswordResetToken(account.AID, account.UserID, login.Password);
                    var resetLink = BuildPasswordResetLink(token);

                    if (!string.IsNullOrWhiteSpace(resetLink))
                    {
                        var sent = await SendPasswordResetEmailAsync(email, resetLink);
                        if (!sent && _environment.IsDevelopment())
                        {
                            TempData["DevResetLink"] = resetLink;
                        }
                    }
                }
            }

            TempData["ForgotPasswordMessage"] = "If this email exists, a password reset link has been sent.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        public IActionResult ResetPassword(string token)
        {
            if (!TryReadPasswordResetToken(token, out _))
            {
                ViewBag.TokenInvalid = true;
                return View(new ResetPasswordViewModel());
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!TryReadPasswordResetToken(model.Token, out var resetToken))
            {
                ViewBag.TokenInvalid = true;
                return View(new ResetPasswordViewModel());
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var login = await _context.Logins
                .FirstOrDefaultAsync(l => l.AID == resetToken.AccountId && l.UserID == resetToken.UserId);

            if (login == null || !IsPasswordResetTokenCurrent(resetToken, login))
            {
                ViewBag.TokenInvalid = true;
                return View(new ResetPasswordViewModel());
            }

            login.Password = model.NewPassword;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} reset password by email link.", resetToken.UserId);

            TempData["SuccessMessage"] = "Password reset successfully. Log in with your new password.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register(string userId, string password, string confirmPassword, string? email)
        {
            userId = userId?.Trim() ?? string.Empty;
            email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

            if (!IsValidNewUserId(userId) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(string.Empty, "Username must be 4-20 characters and use only letters, numbers, or underscore.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return View();
            }

            if (userId.Length < 4 || userId.Length > 20)
            {
                ModelState.AddModelError(string.Empty, "Username must be between 4 and 20 characters.");
                return View();
            }

            if (password.Length < 4 || password.Length > 20)
            {
                ModelState.AddModelError(string.Empty, "Password must be between 4 and 20 characters.");
                return View();
            }

            if (email != null && (email.Length > 50 || !IsValidEmail(email)))
            {
                ModelState.AddModelError(string.Empty, "Enter a valid email address.");
                return View();
            }

            var existingAccount = await _context.Logins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserID == userId);

            if (existingAccount != null)
            {
                ModelState.AddModelError(string.Empty, "This username already exists.");
                return View();
            }

            var accountGrades = await GetAccountGradesAsync();
            if (accountGrades == null)
            {
                ModelState.AddModelError(string.Empty, "Account grade tables are empty. Configure UserGrade and PremiumGrade before registration.");
                return View();
            }

            var newAccount = new Account
            {
                UserID = userId,
                Name = userId,
                Email = email,
                RegDate = DateTime.Now,
                UGradeID = accountGrades.Value.UserGradeId,
                PGradeID = accountGrades.Value.PremiumGradeId
            };

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    _context.Accounts.Add(newAccount);
                    await _context.SaveChangesAsync();

                    var loginAccount = new LoginAccount
                    {
                        UserID = userId,
                        AID = newAccount.AID,
                        Password = password
                    };

                    _context.Logins.Add(loginAccount);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Could not create account {UserId}.", userId);
                ModelState.AddModelError(string.Empty, "Could not create account. Try another username.");
                return View();
            }

            _logger.LogInformation("New account created: {UserId}", userId);

            TempData["SuccessMessage"] = "Account created successfully. Log in to continue.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Profile(int? clanId = null)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login");
            }

            if (IsDemoModeEnabled() && userId == DemoAccountId)
            {
                return View(new AccountProfileViewModel { Characters = GetDemoCharacters() });
            }

            var characters = await _context.Characters
                .AsNoTracking()
                .Where(c => c.AID == userId && (c.DeleteFlag ?? 0) == 0)
                .OrderByDescending(c => c.Level)
                .ToListAsync();

            var clanPanels = await GetAccountClanPanelsAsync(userId.Value, characters);
            var selectedClan = clanPanels.FirstOrDefault(c => c.CLID == clanId) ?? clanPanels.FirstOrDefault();

            return View(new AccountProfileViewModel
            {
                Characters = characters,
                Clans = clanPanels,
                SelectedClanId = selectedClan?.CLID,
                SelectedClan = selectedClan
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> KickClanMember(int cmid, int? returnClanId = null)
        {
            var accountId = HttpContext.Session.GetInt32("UserId");
            if (accountId == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (IsDemoModeEnabled() && accountId == DemoAccountId)
            {
                TempData["ClanPanelError"] = "Demo account cannot manage clan members.";
                return RedirectToAction(nameof(Profile), new { clanId = returnClanId });
            }

            var targetMember = await _context.ClanMembers
                .FirstOrDefaultAsync(m => m.CMID == cmid);

            if (targetMember == null)
            {
                TempData["ClanPanelError"] = "Clan member was not found.";
                return RedirectToAction(nameof(Profile), new { clanId = returnClanId });
            }

            var accountCharacters = await _context.Characters
                .AsNoTracking()
                .Where(c => c.AID == accountId.Value && (c.DeleteFlag ?? 0) == 0)
                .Select(c => c.CID)
                .ToListAsync();

            if (accountCharacters.Count == 0 || accountCharacters.Contains(targetMember.CID))
            {
                TempData["ClanPanelError"] = "You cannot kick this clan member.";
                return RedirectToAction(nameof(Profile), new { clanId = returnClanId });
            }

            var actorMemberships = await _context.ClanMembers
                .AsNoTracking()
                .Where(m => m.CLID == targetMember.CLID && accountCharacters.Contains(m.CID))
                .ToListAsync();

            var canKick = actorMemberships.Any(actor => CanKickClanMember(actor.Grade, targetMember.Grade));
            if (!canKick)
            {
                TempData["ClanPanelError"] = "You do not have permission to kick this clan member.";
                return RedirectToAction(nameof(Profile), new { clanId = returnClanId });
            }

            _context.ClanMembers.Remove(targetMember);
            await _context.SaveChangesAsync();

            TempData["ClanPanelSuccess"] = "Clan member kicked successfully.";
            return RedirectToAction(nameof(Profile), new { clanId = returnClanId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> UploadClanEmblem(int clanId, IFormFile? emblem)
        {
            var accountId = HttpContext.Session.GetInt32("UserId");
            if (accountId == null) return RedirectToAction(nameof(Login));

            if (emblem == null || emblem.Length == 0)
            {
                TempData["ClanPanelError"] = "Choose a PNG emblem before uploading.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            if (emblem.Length > MaxClanEmblemBytes)
            {
                TempData["ClanPanelError"] = "Clan emblem must be 256 KB or smaller.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var extension = Path.GetExtension(emblem.FileName);
            if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ClanPanelError"] = "Clan emblem must be a PNG file.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var accountCharacterIds = await _context.Characters
                .AsNoTracking()
                .Where(c => c.AID == accountId.Value && (c.DeleteFlag ?? 0) == 0)
                .Select(c => c.CID)
                .ToListAsync();

            var isLeader = await _context.ClanMembers
                .AsNoTracking()
                .AnyAsync(m => m.CLID == clanId && accountCharacterIds.Contains(m.CID) && m.Grade == 1);

            if (!isLeader)
            {
                TempData["ClanPanelError"] = "Only the clan leader can update the clan emblem.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var clan = await _context.Clans.FirstOrDefaultAsync(c => c.CLID == clanId && (c.DeleteFlag ?? 0) == 0);
            if (clan == null)
            {
                TempData["ClanPanelError"] = "Clan was not found.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var emblemDirectory = Path.Combine(webRoot, "gunzweb", "clan_emblem");
            Directory.CreateDirectory(emblemDirectory);

            // 1. NOVA FAXINA: Olha no banco qual era o arquivo velho e apaga ele
            if (!string.IsNullOrWhiteSpace(clan.EmblemUrl))
            {
                var oldFilePath = Path.Combine(emblemDirectory, clan.EmblemUrl);
                if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
            }

            // Apaga também o formato antigo que seu amigo estava usando (se existir)
            var legacyFile = Path.Combine(emblemDirectory, $"{clanId}.png");
            if (System.IO.File.Exists(legacyFile)) System.IO.File.Delete(legacyFile);

            // 2. GERAÇÃO DO NOME (SÓ O HASH, COMO VOCÊ PEDIU)
            var uniqueHash = Guid.NewGuid().ToString("N");
            var finalFileName = $"{uniqueHash}.png"; // Resultado: "f2bc7620364f40dbb1626cdc4ceb1040.png"
            var finalFilePath = Path.Combine(emblemDirectory, finalFileName);

            try
            {
                using (var image = await Image.LoadAsync(emblem.OpenReadStream()))
                {
                    image.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(256, 256),
                    //  Mode = ResizeMode.Stretch // Deixa achatado
                        Mode = ResizeMode.Pad     // Preenche o espaço extra com transparência, mantendo a proporção original pra não achatar
                    }));

                    await image.SaveAsPngAsync(finalFilePath);
                }
            }
            catch
            {
                TempData["ClanPanelError"] = "Failed to process the emblem image. Make sure it's a valid PNG.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var fileChecksum = await CalculateGunZFileChecksumAsync(finalFilePath);

            clan.EmblemUrl = finalFileName;
            clan.EmblemChecksum = fileChecksum;

            await _context.SaveChangesAsync();

            TempData["ClanPanelSuccess"] = "Clan emblem updated and optimized to 256x256 successfully.";
            return RedirectToAction(nameof(Profile), new { clanId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteClanEmblem(int clanId)
        {
            var accountId = HttpContext.Session.GetInt32("UserId");
            if (accountId == null) return RedirectToAction(nameof(Login));

            var accountCharacterIds = await _context.Characters
                .AsNoTracking()
                .Where(c => c.AID == accountId.Value && (c.DeleteFlag ?? 0) == 0)
                .Select(c => c.CID)
                .ToListAsync();

            var isLeader = await _context.ClanMembers
                .AsNoTracking()
                .AnyAsync(m => m.CLID == clanId && accountCharacterIds.Contains(m.CID) && m.Grade == 1);

            if (!isLeader)
            {
                TempData["ClanPanelError"] = "Only the clan leader can delete the clan emblem.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var clan = await _context.Clans.FirstOrDefaultAsync(c => c.CLID == clanId && (c.DeleteFlag ?? 0) == 0);
            if (clan == null)
            {
                TempData["ClanPanelError"] = "Clan was not found.";
                return RedirectToAction(nameof(Profile), new { clanId });
            }

            var webRoot = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var emblemDirectory = Path.Combine(webRoot, "gunzweb", "clan_emblem");

            // NOVA FAXINA: Apaga exatamente o arquivo que está salvo no banco
            if (!string.IsNullOrWhiteSpace(clan.EmblemUrl))
            {
                var oldFilePath = Path.Combine(emblemDirectory, clan.EmblemUrl);
                if (System.IO.File.Exists(oldFilePath)) System.IO.File.Delete(oldFilePath);
            }

            var legacyFile = Path.Combine(emblemDirectory, $"{clanId}.png");
            if (System.IO.File.Exists(legacyFile)) System.IO.File.Delete(legacyFile);

            // Reseta as colunas do clã no banco de dados para os valores padrão vazios
            clan.EmblemUrl = "";
            clan.EmblemChecksum = 0;

            await _context.SaveChangesAsync();

            TempData["ClanPanelSuccess"] = "Clan emblem removed successfully.";
            return RedirectToAction(nameof(Profile), new { clanId });
        }

        private string BuildPublicUrl(string path)
        {
            var publicBaseUrl = _configuration["App:PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                return path;
            }

            publicBaseUrl = publicBaseUrl.Trim().TrimEnd('/');
            if (!publicBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !publicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                publicBaseUrl = $"http://{publicBaseUrl}";
            }

            return $"{publicBaseUrl}{path}";
        }

        private static async Task<int> CalculateGunZFileChecksumAsync(string filePath)
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
            uint checksum = unchecked((uint)bytes.Length);

            var offset = 0;
            while (offset + 4 <= bytes.Length)
            {
                checksum +=
                    bytes[offset]
                    | ((uint)bytes[offset + 1] << 8)
                    | ((uint)bytes[offset + 2] << 16)
                    | ((uint)bytes[offset + 3] << 24);
                offset += 4;
            }

            if (offset < bytes.Length)
            {
                uint remainder = 0;
                var shift = 0;
                while (offset < bytes.Length)
                {
                    remainder |= (uint)bytes[offset] << shift;
                    offset++;
                    shift += 8;
                }

                checksum += remainder;
            }

            return unchecked((int)checksum);
        }

        private static async Task<(int Width, int Height)?> ReadPngSizeAsync(IFormFile file)
        {
            byte[] pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
            var buffer = new byte[24];

            await using var stream = file.OpenReadStream();
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length));

            if (bytesRead < buffer.Length || !buffer.Take(pngSignature.Length).SequenceEqual(pngSignature))
            {
                return null;
            }

            var width = ReadBigEndianInt32(buffer, 16);
            var height = ReadBigEndianInt32(buffer, 20);
            return width > 0 && height > 0 ? (width, height) : null;
        }

        private static int ReadBigEndianInt32(byte[] buffer, int start)
        {
            return (buffer[start] << 24)
                | (buffer[start + 1] << 16)
                | (buffer[start + 2] << 8)
                | buffer[start + 3];
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult ChangePassword()
        {
            var accountId = HttpContext.Session.GetInt32("UserId");
            if (accountId == null)
            {
                return RedirectToAction("Login");
            }

            if (IsDemoModeEnabled() && accountId == DemoAccountId)
            {
                TempData["PasswordError"] = "Demo account password cannot be changed.";
                return RedirectToAction("Profile");
            }

            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("auth")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            var accountId = HttpContext.Session.GetInt32("UserId");
            if (accountId == null)
            {
                return RedirectToAction("Login");
            }

            if (IsDemoModeEnabled() && accountId == DemoAccountId)
            {
                TempData["PasswordError"] = "Demo account password cannot be changed.";
                return RedirectToAction("Profile");
            }

            ViewBag.UserName = HttpContext.Session.GetString("UserName");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var login = await _context.Logins
                .FirstOrDefaultAsync(l => l.AID == accountId.Value);

            if (login == null || login.Password != model.CurrentPassword)
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
                return View(model);
            }

            if (login.Password == model.NewPassword)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "Choose a password different from your current one.");
                return View(model);
            }

            login.Password = model.NewPassword;
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {AccountId} changed password successfully.", accountId.Value);

            TempData["PasswordSuccess"] = "Password changed successfully.";
            return RedirectToAction("Profile");
        }

        private static List<Character> GetDemoCharacters()
        {
            return new List<Character>
            {
                new() { Name = "DashMaster", Level = 85, KillCount = 1240, DeathCount = 620, XP = 985000 },
                new() { Name = "KStylePro", Level = 79, KillCount = 1105, DeathCount = 710, XP = 846200 },
                new() { Name = "Reload", Level = 72, KillCount = 932, DeathCount = 540, XP = 710400 }
            };
        }

        private async Task<List<AccountClanPanelViewModel>> GetAccountClanPanelsAsync(int accountId, IReadOnlyList<Character> characters)
        {
            var characterIds = characters.Select(c => c.CID).ToList();
            if (characterIds.Count == 0)
            {
                return new List<AccountClanPanelViewModel>();
            }

            var accountMemberships = await _context.ClanMembers
                .AsNoTracking()
                .Where(m => characterIds.Contains(m.CID))
                .ToListAsync();

            var clanIds = accountMemberships.Select(m => m.CLID).Distinct().ToList();
            if (clanIds.Count == 0)
            {
                return new List<AccountClanPanelViewModel>();
            }

            var clans = await _context.Clans
                .AsNoTracking()
                .Where(c => clanIds.Contains(c.CLID) && (c.DeleteFlag ?? 0) == 0)
                .ToDictionaryAsync(c => c.CLID);

            var allMemberRows = await _context.ClanMembers
                .AsNoTracking()
                .Where(member => clanIds.Contains(member.CLID))
                .ToListAsync();

            var allMemberCharacterIds = allMemberRows.Select(m => m.CID).Distinct().ToList();
            var memberCharacters = await _context.Characters
                .AsNoTracking()
                .Where(character => allMemberCharacterIds.Contains(character.CID))
                .Select(character => new { character.CID, character.Name, character.Level })
                .ToDictionaryAsync(character => character.CID);

            return accountMemberships
                .Where(m => clans.ContainsKey(m.CLID))
                .GroupBy(m => m.CLID)
                .Select(group =>
                {
                    var actorMembership = group
                        .OrderBy(m => GetClanGradeRank(m.Grade))
                        .First();

                    var actorCharacterName = characters.FirstOrDefault(c => c.CID == actorMembership.CID)?.Name ?? actorMembership.CID.ToString();
                    var members = allMemberRows
                        .Where(m => m.CLID == group.Key)
                        .OrderBy(m => GetClanGradeRank(m.Grade))
                        .ThenBy(m => memberCharacters.TryGetValue(m.CID, out var character) ? character.Name : m.CID.ToString())
                        .Select(member =>
                        {
                            memberCharacters.TryGetValue(member.CID, out var character);

                            return new AccountClanMemberViewModel
                            {
                                CMID = member.CMID,
                                CID = member.CID,
                                Name = character?.Name ?? member.CID.ToString(),
                                GradeName = NormalizeClanGradeName(ResolveClanGradeName(member.Grade)),
                                Level = character?.Level,
                                ContributionPoints = member.ContPoint,
                                CanKick = !characterIds.Contains(member.CID) && CanKickClanMember(actorMembership.Grade, member.Grade)
                            };
                        })
                        .ToList();

                    return new AccountClanPanelViewModel
                    {
                        CLID = group.Key,
                        ClanName = clans[group.Key].Name,
                        EmblemUrl = clans[group.Key].EmblemUrl,
                        EmblemChecksum = clans[group.Key].EmblemChecksum,
                        ActorCharacterName = actorCharacterName,
                        ActorGradeName = NormalizeClanGradeName(ResolveClanGradeName(actorMembership.Grade)),
                        CanManageMembers = members.Any(m => m.CanKick),
                        CanUpdateEmblem = actorMembership.Grade == 1,
                        Members = members
                    };
                })
                .OrderBy(c => c.ClanName)
                .ToList();
        }

        private static bool CanKickClanMember(byte? actorGrade, byte? targetGrade)
        {
            return actorGrade switch
            {
                1 => targetGrade != 1,
                2 => targetGrade == 3,
                _ => false
            };
        }

        private static int GetClanGradeRank(byte? grade)
        {
            return grade switch
            {
                1 => 0,
                2 => 1,
                _ => 2
            };
        }

        private static string ResolveClanGradeName(byte? grade)
        {
            return grade switch
            {
                1 => "Leader",
                2 => "Admin",
                _ => "Member"
            };
        }

        private static string NormalizeClanGradeName(string? grade)
        {
            return grade?.Trim().ToLowerInvariant() switch
            {
                "master" => "Leader",
                "leader" => "Leader",
                "officer" => "Admin",
                "admin" => "Admin",
                "" or null => "Member",
                _ => grade
            };
        }

        private bool IsDemoModeEnabled()
        {
            return _environment.IsDevelopment() && _configuration.GetValue<bool>("UseMockData");
        }

        private async Task<(int UserGradeId, int PremiumGradeId)?> GetAccountGradesAsync()
        {
            var userGradeIds = await _context.Database
                .SqlQueryRaw<int?>("SELECT TOP (1) UGradeID AS Value FROM dbo.UserGrade ORDER BY CASE WHEN UGradeID = 0 THEN 0 ELSE 1 END, UGradeID")
                .ToListAsync();

            var premiumGradeIds = await _context.Database
                .SqlQueryRaw<int?>("SELECT TOP (1) PGradeID AS Value FROM dbo.PremiumGrade ORDER BY CASE WHEN PGradeID = 0 THEN 0 ELSE 1 END, PGradeID")
                .ToListAsync();

            var userGradeId = userGradeIds.FirstOrDefault();
            var premiumGradeId = premiumGradeIds.FirstOrDefault();

            if (userGradeId == null || premiumGradeId == null)
            {
                return null;
            }

            return (userGradeId.Value, premiumGradeId.Value);
        }

        private static bool IsAcceptableLoginInput(string userId, string password)
        {
            return !string.IsNullOrEmpty(userId)
                && userId.Length <= 20
                && !HasControlCharacters(userId)
                && !string.IsNullOrEmpty(password)
                && password.Length <= 20
                && !HasControlCharacters(password);
        }

        private static bool IsValidNewUserId(string userId)
        {
            return userId.Length is >= 4 and <= 20
                && userId.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        private static bool HasControlCharacters(string value)
        {
            return value.Any(char.IsControl);
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new System.Net.Mail.MailAddress(email);
                return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string CreatePasswordResetToken(int accountId, string userId, string? currentPassword)
        {
            var token = new PasswordResetToken(accountId, userId, currentPassword ?? string.Empty, DateTimeOffset.UtcNow);
            return _passwordResetProtector.Protect(JsonSerializer.Serialize(token));
        }

        private string? BuildPasswordResetLink(string token)
        {
            var relativePath = Url.Action(nameof(ResetPassword), "Account", new { token });
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            var publicBaseUrl = _configuration["App:PublicBaseUrl"];
            if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                if (Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri) &&
                    (baseUri.Scheme == Uri.UriSchemeHttps || (_environment.IsDevelopment() && baseUri.Scheme == Uri.UriSchemeHttp)))
                {
                    return new Uri(baseUri, relativePath).ToString();
                }

                _logger.LogWarning("App:PublicBaseUrl is invalid. Password reset link was not generated.");
                return null;
            }

            if (_environment.IsDevelopment())
            {
                return Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme);
            }

            _logger.LogWarning("App:PublicBaseUrl is not configured. Password reset link was not generated.");
            return null;
        }

        private bool TryReadPasswordResetToken(string? token, out PasswordResetToken resetToken)
        {
            resetToken = new PasswordResetToken(0, string.Empty, string.Empty, DateTimeOffset.MinValue);

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            try
            {
                var json = _passwordResetProtector.Unprotect(token);
                var parsed = JsonSerializer.Deserialize<PasswordResetToken>(json);

                if (parsed == null || parsed.AccountId <= 0 || string.IsNullOrWhiteSpace(parsed.UserId))
                {
                    return false;
                }

                if (DateTimeOffset.UtcNow - parsed.IssuedAt > TimeSpan.FromHours(1))
                {
                    return false;
                }

                resetToken = parsed;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPasswordResetTokenCurrent(PasswordResetToken resetToken, LoginAccount login)
        {
            return string.Equals(resetToken.PasswordSnapshot, login.Password ?? string.Empty, StringComparison.Ordinal);
        }

        private async Task<bool> SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var host = _configuration["Smtp:Host"];
            var from = _configuration["Smtp:From"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                _logger.LogWarning("SMTP is not configured. Password reset email was not sent.");
                return false;
            }

            var port = _configuration.GetValue("Smtp:Port", 587);
            var enableSsl = _configuration.GetValue("Smtp:EnableSsl", true);
            var userName = _configuration["Smtp:UserName"];
            var password = _configuration["Smtp:Password"];

            using var message = new MailMessage(from, email)
            {
                Subject = "GunZ League password reset",
                Body = $"Use this link to reset your password. It expires in 1 hour.\n\n{resetLink}",
                IsBodyHtml = false
            };

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrWhiteSpace(userName))
            {
                client.Credentials = new NetworkCredential(userName, password);
            }

            try
            {
                await client.SendMailAsync(message);
                return true;
            }
            catch (SmtpException ex)
            {
                _logger.LogWarning(ex, "SMTP failed while sending password reset email.");
                return false;
            }
        }

        private sealed record PasswordResetToken(int AccountId, string UserId, string PasswordSnapshot, DateTimeOffset IssuedAt);
    }
}
