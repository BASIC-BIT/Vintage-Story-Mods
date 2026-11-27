using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ProperVersion;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.UpdateCheck
{
    /// <summary>
    /// Pure decision rules for the update-available notice. No game state, no I/O.
    ///
    /// ModDB stores a whole-major game-version selection as a flat list of the exact patches that
    /// existed at upload time, identical to a hand-picked pin, so intent is read from the tag set:
    /// a stable release tagged for exactly our patch means the author has spoken about it and the
    /// install-information answer is final; no tag for our patch means tagging has lagged, and the
    /// newest tagged patch of our own minor is the best evidence. UpdateCheckTests holds the table.
    /// </summary>
    public static class UpdateCheck
    {
        public const string ModId = "thebasics";

        private const int RetractedErrorCode = 4101;
        private const int RetractedForcedErrorCode = 4102;

        public sealed record InstallInfo(string RecommendedUpgrade, int? ErrorCode, string RetractionReason);

        public sealed record Release(string ModVersion, IReadOnlyList<string> Tags);

        public sealed record Decision(string UpgradeVersion, string TaggedForGameVersion, bool Retracted, string RetractionReason)
        {
            public static readonly Decision None = new(null, null, false, null);

            public bool HasNotice => UpgradeVersion != null || Retracted;

            public string Key => $"{UpgradeVersion}|{Retracted}";
        }

        /// <summary>Call A body (`/api/v2/mods/install-information`). Null when it is not the expected shape.</summary>
        public static InstallInfo ParseInstallInfo(string json)
        {
            try
            {
                if (JToken.Parse(json) is not JObject root ||
                    root["data"] is not JObject data ||
                    data[ModId] is not JObject entry)
                {
                    return null;
                }

                return new InstallInfo(
                    StringOrNull(entry["recommendedUpgrade"]),
                    entry["errorCode"]?.Type == JTokenType.Integer ? entry.Value<int>("errorCode") : null,
                    StripControlChars(StringOrNull(entry["retractionReason"])));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Call B body (v1 `/api/mod/thebasics`). Reads only modversion and tags.</summary>
        public static IReadOnlyList<Release> ParseReleases(string json)
        {
            try
            {
                if (JToken.Parse(json) is not JObject root ||
                    root["mod"] is not JObject mod ||
                    mod["releases"] is not JArray releases)
                {
                    return null;
                }

                return releases.OfType<JObject>()
                    .Select(r => new Release(
                        StringOrNull(r["modversion"]),
                        (r["tags"] as JArray)?.Select(StringOrNull).Where(t => t != null).ToList() ?? []))
                    .ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool NeedsReleaseList(InstallInfo info)
        {
            return info != null && !TryRecommended(info, out _) &&
                   (info.ErrorCode == null || IsRetraction(info.ErrorCode));
        }

        /// <summary>
        /// Returns null on failure, so the caller keeps its cached result. <paramref name="cached"/> is
        /// only read when the release list was needed but is missing and A carried a retraction.
        /// </summary>
        public static Decision Decide(string installed, string gameVersion, InstallInfo info,
            IReadOnlyList<Release> releases, Decision cached = null)
        {
            if (info == null)
            {
                return null;
            }

            var retracted = IsRetraction(info.ErrorCode);
            if (info.ErrorCode != null && !retracted)
            {
                return Decision.None;
            }

            var reason = retracted ? info.RetractionReason : null;
            var quiet = retracted ? new Decision(null, null, true, reason) : Decision.None;

            if (!TryVersion(installed, out var current))
            {
                return quiet;
            }

            if (TryRecommended(info, out var recommended))
            {
                return recommended > current ? new Decision(recommended.ToString(), null, retracted, reason) : quiet;
            }

            if (releases == null)
            {
                return retracted ? (cached ?? Decision.None) with { Retracted = true, RetractionReason = reason } : null;
            }

            if (!TryVersion(gameVersion, out var game))
            {
                return quiet;
            }

            var stable = StableReleases(releases);

            // 6a: our exact patch is tagged, so A was authoritative. This honours a pin.
            if (stable.Any(r => r.Tags.Contains(game)))
            {
                return quiet;
            }

            // 6b: the newest stable tag of our own minor that is older than our patch. Never cross a minor.
            var fallbackTag = stable.SelectMany(r => r.Tags)
                .Where(t => IsStable(t) && t.Major == game.Major && t.Minor == game.Minor && t < game)
                .Max();
            if (fallbackTag == null)
            {
                return quiet;
            }

            // 6c
            var best = stable.Where(r => r.Tags.Contains(fallbackTag)).Max(r => r.Version);
            return best > current ? new Decision(best.ToString(), fallbackTag.ToString(), retracted, reason) : quiet;
        }

        // Pre-release mod versions are dropped before any rule runs. Unparseable versions and tags are skipped.
        private static List<(SemVer Version, List<SemVer> Tags)> StableReleases(IEnumerable<Release> releases)
        {
            var stable = new List<(SemVer Version, List<SemVer> Tags)>();
            foreach (var release in releases)
            {
                if (release != null && TryVersion(release.ModVersion, out var version) && IsStable(version))
                {
                    var tags = (release.Tags ?? []).Select(t => TryVersion(t, out var v) ? v : null).Where(v => v != null).ToList();
                    stable.Add((version, tags));
                }
            }

            return stable;
        }

        // Unparseable or pre-release recommendations are ignored and the release-list rules decide.
        private static bool TryRecommended(InstallInfo info, out SemVer recommended) =>
            TryVersion(info.RecommendedUpgrade, out recommended) && IsStable(recommended);

        // Free text from ModDB: a newline could forge a second notice line in chat or the log.
        private static string StripControlChars(string s) =>
            s == null ? null : new string(s.Select(c => char.IsControl(c) ? ' ' : c).ToArray());

        private static bool IsRetraction(int? errorCode) => errorCode is RetractedErrorCode or RetractedForcedErrorCode;

        private static bool IsStable(SemVer version) => version.PreReleaseIdentifiers.Length == 0;

        // SemVer.TryParse throws on null (most A responses have no recommendedUpgrade) and int.Parse
        // inside it throws on a numeric part too large for an int.
        private static bool TryVersion(string s, out SemVer version)
        {
            version = null;
            try
            {
                return s != null && SemVer.TryParse(s, out version);
            }
            catch (OverflowException)
            {
                version = null;
                return false;
            }
        }

        private static string StringOrNull(JToken token) => token?.Type == JTokenType.String ? (string)token : null;
    }

    /// <summary>
    /// Dedicated servers only: asks ModDB about a minute after boot and then every ~6 hours whether a
    /// newer stable release exists, and tells online admins (controlserver) once per distinct result.
    /// Notify only: <c>fileUrl</c> is never followed and nothing is downloaded.
    /// </summary>
    public class UpdateCheckSystem : BaseBasicModSystem
    {
        private const string ModDbHost = "https://mods.vintagestory.at";
        private const int FirstCheckMinDelayMs = 30_000;
        private const int FirstCheckMaxDelayMs = 90_000;
        private const int CheckIntervalMs = 6 * 60 * 60 * 1000;
        private const int CheckJitterMs = 30 * 60 * 1000;
        private const int MaxReasonLength = 200;

        private readonly HashSet<string> _notified = new();
        private HttpClient _http;
        private string _installed;
        private string _gameVersion;
        private long? _callbackId;
        private int _checkInFlight;
        private volatile bool _disposed;
        private volatile UpdateCheck.Decision _cached;

        protected override void BasicStartServerSide()
        {
            // ServerMain is only constructed with isDedicatedServer: true by ServerProgram; singleplayer
            // and open-to-LAN worlds run the client-hosted server, where this is false.
            if (!API.Server.IsDedicated)
            {
                return;
            }

            _installed = Mod.Info.Version;
            // ShortGameVersion is a const: reading it directly would freeze the version this mod was
            // compiled against. The field's metadata comes from the VintagestoryAPI.dll that is running.
            _gameVersion = typeof(GameVersion).GetField(nameof(GameVersion.ShortGameVersion))?.GetRawConstantValue() as string;
            if (string.IsNullOrEmpty(_installed) || string.IsNullOrEmpty(_gameVersion))
            {
                return;
            }

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 1_000_000 };
            _http.DefaultRequestHeaders.UserAgent.ParseAdd($"thebasics-updatecheck/{_installed}");

            API.Event.PlayerNowPlaying += OnPlayerNowPlaying;

            if (Config.EnableUpdateCheck)
            {
                ScheduleNext(Random.Shared.Next(FirstCheckMinDelayMs, FirstCheckMaxDelayMs));
            }
        }

        protected override void OnConfigReloaded(IReadOnlySet<string> changedKeys)
        {
            if (_http == null || !changedKeys.Contains(nameof(Config.EnableUpdateCheck)))
            {
                return;
            }

            CancelCallback();
            if (Config.EnableUpdateCheck)
            {
                ScheduleNext(Random.Shared.Next(FirstCheckMinDelayMs, FirstCheckMaxDelayMs));
            }
            else
            {
                _cached = null;
            }
        }

        public override void Dispose()
        {
            _disposed = true;
            if (API?.Event != null && _http != null)
            {
                CancelCallback();
                API.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
            }

            _http?.Dispose();
            _http = null;
            base.Dispose();
        }

        private void ScheduleNext(int delayMs)
        {
            _callbackId = API.Event.RegisterCallback(_ => OnCheckDue(), delayMs);
        }

        private void CancelCallback()
        {
            if (_callbackId is { } id)
            {
                API.Event.UnregisterCallback(id);
                _callbackId = null;
            }
        }

        private void OnCheckDue()
        {
            _callbackId = null;
            if (_disposed || !Config.EnableUpdateCheck)
            {
                return;
            }

            // Reschedule on the game thread before the fetch starts, so a hung request never stops the cycle.
            ScheduleNext(CheckIntervalMs + Random.Shared.Next(-CheckJitterMs, CheckJitterMs));
            _ = Task.Run(CheckAsync);
        }

        private async Task CheckAsync()
        {
            if (Interlocked.Exchange(ref _checkInFlight, 1) == 1)
            {
                return;
            }

            var urlA = $"{ModDbHost}/api/v2/mods/install-information?ids={UpdateCheck.ModId}@{Uri.EscapeDataString(_installed)}&gv={Uri.EscapeDataString(_gameVersion)}";
            var urlB = $"{ModDbHost}/api/mod/{UpdateCheck.ModId}";
            var current = urlA;
            try
            {
                var info = UpdateCheck.ParseInstallInfo(await GetBodyAsync(urlA).ConfigureAwait(false));
                var needsReleases = UpdateCheck.NeedsReleaseList(info);
                current = urlB;
                var releases = needsReleases ? UpdateCheck.ParseReleases(await GetBodyAsync(urlB).ConfigureAwait(false)) : null;
                var decision = UpdateCheck.Decide(_installed, _gameVersion, info, releases, _cached);

                API.Logger.Debug($"The BASICs update check: GET {urlA}{(needsReleases ? $" and {urlB}" : "")} -> {DescribeOutcome(decision)}");

                API.Event.EnqueueMainThreadTask(() => ApplyResult(decision), "thebasics-updatecheck");
            }
            catch (Exception e)
            {
                API.Logger.Debug($"The BASICs update check: GET {current} failed ({e.GetType().Name}), keeping previous result");
            }
            finally
            {
                Interlocked.Exchange(ref _checkInFlight, 0);
            }
        }

        private static string DescribeOutcome(UpdateCheck.Decision decision)
        {
            if (decision == null)
            {
                return "failed, keeping previous result";
            }

            if (decision.UpgradeVersion != null)
            {
                return $"upgrade {decision.UpgradeVersion}{(decision.Retracted ? ", installed version retracted" : "")}";
            }

            return decision.Retracted ? "retracted, no upgrade" : "up to date";
        }

        private async Task<string> GetBodyAsync(string url)
        {
            var http = _http;
            if (http == null)
            {
                return null;
            }

            using var response = await http.GetAsync(url).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK
                ? await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                : null;
        }

        private void ApplyResult(UpdateCheck.Decision decision)
        {
            // A check that finishes after Dispose still gets enqueued.
            if (_disposed || decision == null || !Config.EnableUpdateCheck)
            {
                return;
            }

            var isNew = _cached?.Key != decision.Key;
            _cached = decision;
            if (!decision.HasNotice)
            {
                return;
            }

            if (isNew)
            {
                LogDecision(decision);
            }

            // AllOnlinePlayers includes clients still connecting, whose chat HUD does not exist yet and
            // crashes on a message. Connected is when PlayerNowPlaying fires and the server sends its own
            // welcome message; Playing comes later. Skipped players are covered by the join path.
            foreach (var player in API.World.AllOnlinePlayers.OfType<IServerPlayer>()
                         .Where(p => p.ConnectionState is EnumClientState.Connected or EnumClientState.Playing))
            {
                Notify(player);
            }
        }

        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            if (Config.EnableUpdateCheck)
            {
                Notify(player);
            }
        }

        private void Notify(IServerPlayer player)
        {
            var decision = _cached;
            if (decision is not { HasNotice: true } || !player.HasPrivilege(Privilege.controlserver) ||
                !_notified.Add(player.PlayerUID + "|" + decision.Key))
            {
                return;
            }

            if (decision.Retracted)
            {
                Send(player, Lang.Get("thebasics:update-retracted", _installed, VtmlUtils.EscapeVtml(Truncate(decision.RetractionReason))));
            }

            if (decision.UpgradeVersion != null)
            {
                Send(player, decision.TaggedForGameVersion == null
                    ? Lang.Get("thebasics:update-available", decision.UpgradeVersion, _installed)
                    : Lang.Get("thebasics:update-available-untagged", decision.UpgradeVersion, _installed, decision.TaggedForGameVersion, _gameVersion));
            }

            Send(player, Lang.Get("thebasics:update-silence-hint"));
        }

        private void LogDecision(UpdateCheck.Decision decision)
        {
            if (decision.Retracted)
            {
                // The reason is free text from ModDB: pass it as an argument, never as the format string.
                API.Logger.Warning("The BASICs: the installed version {0} was retracted on ModDB. Reason: {1}", _installed, Truncate(decision.RetractionReason));
            }

            if (decision.UpgradeVersion != null)
            {
                var tagged = decision.TaggedForGameVersion == null
                    ? ""
                    : $" It is tagged for game {decision.TaggedForGameVersion}; this game {_gameVersion} is not tagged yet.";
                API.Logger.Notification($"The BASICs {decision.UpgradeVersion} is available (this server runs {_installed}).{tagged} https://mods.vintagestory.at/thebasics");
            }
        }

        private static void Send(IServerPlayer player, string message)
        {
            player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
        }

        private static string Truncate(string reason)
        {
            reason ??= "";
            return reason.Length <= MaxReasonLength ? reason : reason[..MaxReasonLength] + "...";
        }
    }
}
