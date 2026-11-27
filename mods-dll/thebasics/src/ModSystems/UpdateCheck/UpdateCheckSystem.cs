using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ProperVersion;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using thebasics.Utilities;

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

        // The installed version was retracted (by its author, or by a moderator). ModDB still answers
        // with recommendedUpgrade, so these are handled like no error at all.
        private const int InstalledRetractedErrorCode = 4101;
        private const int InstalledRetractedForcedErrorCode = 4102;

        public sealed record InstallInfo(string RecommendedUpgrade, int? ErrorCode);

        public sealed record Release(string ModVersion, IReadOnlyList<string> Tags);

        public sealed record Decision(string UpgradeVersion, string TaggedForGameVersion)
        {
            public static readonly Decision None = new(null, null);

            public bool HasNotice => UpgradeVersion != null;

            public string Key => UpgradeVersion;
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
                    ErrorCodeOrNull(entry["errorCode"]));
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
                   !IsQuietError(info.ErrorCode);
        }

        /// <summary>Returns null on failure, so the caller keeps its cached result.</summary>
        public static Decision Decide(string installed, string gameVersion, InstallInfo info,
            IReadOnlyList<Release> releases)
        {
            if (info == null)
            {
                return null;
            }

            if (IsQuietError(info.ErrorCode) || !TryVersion(installed, out var current))
            {
                return Decision.None;
            }

            if (TryRecommended(info, out var recommended))
            {
                return recommended > current ? new Decision(recommended.ToString(), null) : Decision.None;
            }

            // An empty list, or one in which no release has a readable version, means the v1 shape changed: that is a
            // failure, not "up to date".
            if (releases == null || !releases.Any(r => TryVersion(r?.ModVersion, out _)))
            {
                return null;
            }

            if (!TryVersion(gameVersion, out var game))
            {
                return Decision.None;
            }

            var stable = StableReleases(releases);

            // 6a: our exact patch is tagged, so A was authoritative. This honours a pin.
            if (stable.Any(r => r.Tags.Contains(game)))
            {
                return Decision.None;
            }

            // 6b: the newest stable tag of our own minor that is older than our patch. Never cross a minor.
            var fallbackTag = stable.SelectMany(r => r.Tags)
                .Where(t => IsStable(t) && t.Major == game.Major && t.Minor == game.Minor && t < game)
                .Max();
            if (fallbackTag == null)
            {
                return Decision.None;
            }

            // 6c
            var best = stable.Where(r => r.Tags.Contains(fallbackTag)).Max(r => r.Version);
            return best > current ? new Decision(best.ToString(), fallbackTag.ToString()) : Decision.None;
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

        // Any error code except the retracted-install ones means there is nothing to offer.
        private static bool IsQuietError(int? errorCode) =>
            errorCode is not (null or InstalledRetractedErrorCode or InstalledRetractedForcedErrorCode);

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

        // The v1 API quotes its status code as a string; accept either form here.
        private static int? ErrorCodeOrNull(JToken token) => token?.Type switch
        {
            JTokenType.Integer => (int)token,
            JTokenType.String => int.TryParse((string)token, out var code) ? code : null,
            _ => null,
        };

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
        private const int FailuresBeforeWarning = 4;

        private readonly HashSet<string> _notified = new();
        private HttpClient _http;
        private string _installed;
        private string _gameVersion;
        private long? _callbackId;
        private int _checkInFlight;
        private int _consecutiveFailures;
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

            // Nothing in here may stop the mod from loading: a notify-only feature just switches itself off.
            HttpClient http = null;
            try
            {
                _installed = Mod.Info.Version;
                // ShortGameVersion is a const: reading it directly would freeze the version this mod was
                // compiled against. The field's metadata comes from the VintagestoryAPI.dll that is running.
                _gameVersion = typeof(GameVersion).GetField(nameof(GameVersion.ShortGameVersion))?.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(_installed) || string.IsNullOrEmpty(_gameVersion))
                {
                    return;
                }

                http = new HttpClient { Timeout = TimeSpan.FromSeconds(10), MaxResponseContentBufferSize = 4_000_000 };
                http.DefaultRequestHeaders.UserAgent.ParseAdd($"thebasics-updatecheck/{_installed}");
            }
            catch (Exception e)
            {
                http?.Dispose();
                API.Logger.Warning("The BASICs update check is off: it could not start ({0}).", e.GetType().Name);
                return;
            }

            _http = http;

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
                var decision = UpdateCheck.Decide(_installed, _gameVersion, info, releases);

                API.Logger.Debug($"The BASICs update check: GET {urlA}{(needsReleases ? $" and {urlB}" : "")} -> {DescribeOutcome(decision, info)}");
                NoteOutcome(decision != null);

                API.Event.EnqueueMainThreadTask(() => ApplyResult(decision), "thebasics-updatecheck");
            }
            catch (Exception e)
            {
                API.Logger.Debug($"The BASICs update check: GET {current} failed ({e.GetType().Name}), keeping previous result");
                NoteOutcome(false);
            }
            finally
            {
                Interlocked.Exchange(ref _checkInFlight, 0);
            }
        }

        // Failures are Debug-only so a network blip stays quiet, but a check that has been broken for
        // about a day says so once where operators will see it.
        private void NoteOutcome(bool succeeded)
        {
            if (succeeded)
            {
                _consecutiveFailures = 0;
            }
            else if (++_consecutiveFailures == FailuresBeforeWarning)
            {
                API.Logger.Warning($"The BASICs update check has failed {FailuresBeforeWarning} times in a row, so update notices may be missing. See the debug log for the cause, or turn it off with EnableUpdateCheck.");
            }
        }

        private static string DescribeOutcome(UpdateCheck.Decision decision, UpdateCheck.InstallInfo info)
        {
            if (decision == null)
            {
                return "failed, keeping previous result";
            }

            if (decision.UpgradeVersion != null)
            {
                return $"upgrade {decision.UpgradeVersion}";
            }

            // An error code is ModDB declining to answer (4041: it does not know this version, as with an
            // unreleased build). That is not the same as being up to date.
            return info?.ErrorCode is { } code ? $"ModDB answered error code {code}, nothing to report" : "up to date";
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

            Send(player, decision.TaggedForGameVersion == null
                ? Lang.Get("thebasics:update-available", VtmlUtils.EscapeVtml(decision.UpgradeVersion), _installed)
                : Lang.Get("thebasics:update-available-untagged", VtmlUtils.EscapeVtml(decision.UpgradeVersion), _installed, VtmlUtils.EscapeVtml(decision.TaggedForGameVersion), _gameVersion));

            Send(player, Lang.Get("thebasics:update-silence-hint"));
        }

        private void LogDecision(UpdateCheck.Decision decision)
        {
            var tagged = decision.TaggedForGameVersion == null
                ? ""
                : $" It is tagged for game {decision.TaggedForGameVersion}; this game {_gameVersion} is not tagged yet.";
            API.Logger.Notification($"The BASICs {decision.UpgradeVersion} is available (this server runs {_installed}).{tagged} https://mods.vintagestory.at/thebasics");
        }

        private static void Send(IServerPlayer player, string message)
        {
            player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
        }
    }
}
