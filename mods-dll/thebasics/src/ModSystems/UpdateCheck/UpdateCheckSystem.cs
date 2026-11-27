using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ProperVersion;

namespace thebasics.ModSystems.UpdateCheck
{
    /// <summary>
    /// Pure decision rules for the update-available notice. No game state, no I/O.
    /// See docs/research/2026-11-26-update-notice-plan.md section 2 for the rule and its table.
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
                    StringOrNull(entry["retractionReason"]));
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
            return info != null && info.RecommendedUpgrade == null &&
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

            if (TryVersion(info.RecommendedUpgrade, out var recommended))
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

        private static bool IsRetraction(int? errorCode) => errorCode is RetractedErrorCode or RetractedForcedErrorCode;

        private static bool IsStable(SemVer version) => version.PreReleaseIdentifiers.Length == 0;

        // SemVer.TryParse throws on null, and most A responses have no recommendedUpgrade.
        private static bool TryVersion(string s, out SemVer version)
        {
            version = null;
            return s != null && SemVer.TryParse(s, out version);
        }

        private static string StringOrNull(JToken token) => token?.Type == JTokenType.String ? (string)token : null;
    }
}
