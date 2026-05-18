namespace GunZLeague.Models.GunZ
{
    public static class PlayerWarRankingBuilder
    {
        private static readonly char[] ParticipantSeparators = [' ', ',', ';', '|', '/', '\\', ':'];

        public static HashSet<int> ExtractCharacterIds(IEnumerable<PWGameLog> logs)
        {
            var ids = new HashSet<int>();

            foreach (var log in logs)
            {
                AddCharacterIds(ids, log.Winners);
                AddCharacterIds(ids, log.Losers);
            }

            return ids;
        }

        public static List<PlayerWarStanding> Build(IEnumerable<PWGameLog> logs, IReadOnlyDictionary<int, string> characterNames)
        {
            var standings = new Dictionary<string, PlayerWarStanding>(StringComparer.OrdinalIgnoreCase);

            foreach (var log in logs)
            {
                AddResult(standings, log.Winners, characterNames, points: 3, won: true);
                AddResult(standings, log.Losers, characterNames, points: 1, won: false);
            }

            return standings.Values
                .OrderByDescending(s => s.Points)
                .ThenByDescending(s => s.Wins)
                .ThenBy(s => s.PlayerName)
                .ToList();
        }

        public static string FormatParticipants(string participants, IReadOnlyDictionary<int, string> characterNames)
        {
            var names = ResolveParticipants(participants, characterNames)
                .Select(p => p.Name)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return names.Count > 0 ? string.Join(", ", names) : "-";
        }

        public static List<PlayerWarMatchSummary> BuildLatestMatches(IEnumerable<PWGameLog> logs, IReadOnlyDictionary<int, string> characterNames)
        {
            return logs
                .OrderByDescending(log => log.RegDate)
                .Select(log =>
                {
                    var winners = ResolveParticipantList(log.Winners, characterNames);
                    var losers = ResolveParticipantList(log.Losers, characterNames);

                    return new PlayerWarMatchSummary
                    {
                        Winners = winners.Count > 0 ? string.Join(", ", winners) : "-",
                        Losers = losers.Count > 0 ? string.Join(", ", losers) : "-",
                        WinnerPlayers = winners,
                        LoserPlayers = losers,
                        WinScore = log.WinScore,
                        LoseScore = log.LoseScore,
                        RoundScore = FormatRoundScore(log.WinScore, log.LoseScore),
                        MapID = log.MapID,
                        MapName = PlayerWarMapResolver.GetName(log.MapID),
                        PlayedAt = log.RegDate
                    };
                })
                .ToList();
        }

        private static string FormatRoundScore(int? winScore, int? loseScore)
        {
            if (winScore == 4 && loseScore is >= 0 and <= 3)
            {
                return $"{winScore}-{loseScore}";
            }

            return string.Empty;
        }

        private static List<string> ResolveParticipantList(string participants, IReadOnlyDictionary<int, string> characterNames)
        {
            return ResolveParticipants(participants, characterNames)
                .Select(p => p.Name)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
        }

        private static void AddCharacterIds(HashSet<int> ids, string participants)
        {
            foreach (var token in SplitParticipants(participants))
            {
                if (int.TryParse(token, out var characterId))
                {
                    ids.Add(characterId);
                }
            }
        }

        private static void AddResult(
            Dictionary<string, PlayerWarStanding> standings,
            string participants,
            IReadOnlyDictionary<int, string> characterNames,
            int points,
            bool won)
        {
            foreach (var participant in ResolveParticipants(participants, characterNames))
            {
                var playerName = participant.Name;
                if (!standings.TryGetValue(playerName, out var standing))
                {
                    standing = new PlayerWarStanding
                    {
                        CharacterId = participant.CharacterId,
                        PlayerName = playerName
                    };
                    standings[playerName] = standing;
                }

                standing.CharacterId ??= participant.CharacterId;
                standing.Points += points;
                if (won)
                {
                    standing.Wins++;
                }
                else
                {
                    standing.Losses++;
                }
            }
        }

        private static IEnumerable<(int? CharacterId, string Name)> ResolveParticipants(string participants, IReadOnlyDictionary<int, string> characterNames)
        {
            foreach (var token in SplitParticipants(participants))
            {
                if (int.TryParse(token, out var characterId))
                {
                    if (characterNames.TryGetValue(characterId, out var characterName) && !string.IsNullOrWhiteSpace(characterName))
                    {
                        yield return (characterId, characterName.Trim());
                    }

                    continue;
                }

                yield return (null, token);
            }
        }

        private static IEnumerable<string> SplitParticipants(string participants)
        {
            if (string.IsNullOrWhiteSpace(participants))
            {
                yield break;
            }

            foreach (var token in participants.Split(ParticipantSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    yield return token;
                }
            }
        }
    }
}
