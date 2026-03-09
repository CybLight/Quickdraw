using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PriorityManagerX
{
    public sealed class UpdateAssetInfo
    {
        public string Name { get; init; } = string.Empty;
        public string DownloadUrl { get; init; } = string.Empty;
        public long Size { get; init; }
    }

    public sealed class UpdateReleaseInfo
    {
        public string TagName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string HtmlUrl { get; init; } = string.Empty;
        public bool Prerelease { get; init; }
        public DateTimeOffset PublishedAt { get; init; }
        public UpdateAssetInfo? PreferredInstallerAsset { get; init; }
    }

    public sealed class UpdateCheckResult
    {
        public bool IsSuccess { get; init; }
        public bool IsUpdateAvailable { get; init; }
        public string Error { get; init; } = string.Empty;
        public Version CurrentVersion { get; init; } = new(0, 0, 0, 0);
        public Version? LatestVersion { get; init; }
        public UpdateReleaseInfo? Release { get; init; }
    }

    public static class GitHubUpdater
    {
        const string RepoOwner = "CybLight";
        const string RepoName = "Priority-Manager-X";
        const string InstallerNameHint = "Setup-x64.exe";

        static readonly HttpClient Client = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PriorityManagerX", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(AppSettings settings, Version currentVersion, CancellationToken cancellationToken = default)
        {
            try
            {
                var release = await GetReleaseAsync(settings, cancellationToken).ConfigureAwait(false);
                if (release == null)
                {
                    return new UpdateCheckResult
                    {
                        IsSuccess = false,
                        Error = "Release data was not found.",
                        CurrentVersion = currentVersion
                    };
                }

                var releaseVersion = ParseVersionFromRelease(release.TagName, release.Name);
                if (releaseVersion == null)
                {
                    return new UpdateCheckResult
                    {
                        IsSuccess = false,
                        Error = $"Failed to parse release version from '{release.TagName}'.",
                        CurrentVersion = currentVersion
                    };
                }

                return new UpdateCheckResult
                {
                    IsSuccess = true,
                    IsUpdateAvailable = releaseVersion > currentVersion,
                    CurrentVersion = currentVersion,
                    LatestVersion = releaseVersion,
                    Release = release
                };
            }
            catch (OperationCanceledException)
            {
                return new UpdateCheckResult
                {
                    IsSuccess = false,
                    Error = "Operation cancelled.",
                    CurrentVersion = currentVersion
                };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult
                {
                    IsSuccess = false,
                    Error = ex.Message,
                    CurrentVersion = currentVersion
                };
            }
        }

        public static async Task DownloadAssetAsync(string url, string targetFilePath, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var targetDir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrWhiteSpace(targetDir))
                Directory.CreateDirectory(targetDir);

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var destination = File.Create(targetFilePath);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        static async Task<UpdateReleaseInfo?> GetReleaseAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            if (settings.IncludePrereleaseUpdates)
                return await GetLatestFromListAsync(settings, cancellationToken).ConfigureAwait(false);

            return await GetLatestStableAsync(settings, cancellationToken).ConfigureAwait(false);
        }

        static async Task<UpdateReleaseInfo?> GetLatestStableAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var json = await Client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return ParseRelease(doc.RootElement, InstallerNameHint);
        }

        static async Task<UpdateReleaseInfo?> GetLatestFromListAsync(AppSettings settings, CancellationToken cancellationToken)
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases?per_page=10";
            var json = await Client.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var releaseElement in doc.RootElement.EnumerateArray())
            {
                if (releaseElement.TryGetProperty("draft", out var draftElement) && draftElement.ValueKind == JsonValueKind.True)
                    continue;

                if (!settings.IncludePrereleaseUpdates
                    && releaseElement.TryGetProperty("prerelease", out var prereleaseElement)
                    && prereleaseElement.ValueKind == JsonValueKind.True)
                {
                    continue;
                }

                return ParseRelease(releaseElement, InstallerNameHint);
            }

            return null;
        }

        static UpdateReleaseInfo? ParseRelease(JsonElement root, string assetNameHint)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var tag = GetString(root, "tag_name");
            var name = GetString(root, "name");
            var htmlUrl = GetString(root, "html_url");
            var prerelease = GetBool(root, "prerelease");
            var publishedAt = GetDate(root, "published_at");

            var assets = new List<UpdateAssetInfo>();
            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assetsElement.EnumerateArray())
                {
                    assets.Add(new UpdateAssetInfo
                    {
                        Name = GetString(a, "name"),
                        DownloadUrl = GetString(a, "browser_download_url"),
                        Size = GetLong(a, "size")
                    });
                }
            }

            return new UpdateReleaseInfo
            {
                TagName = tag,
                Name = name,
                HtmlUrl = htmlUrl,
                Prerelease = prerelease,
                PublishedAt = publishedAt,
                PreferredInstallerAsset = PickInstallerAsset(assets, assetNameHint)
            };
        }

        static UpdateAssetInfo? PickInstallerAsset(List<UpdateAssetInfo> assets, string hint)
        {
            var normalizedHint = hint?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedHint))
            {
                var byHint = assets.Find(a =>
                    !string.IsNullOrWhiteSpace(a.Name)
                    && a.Name.Contains(normalizedHint, StringComparison.OrdinalIgnoreCase));
                if (byHint != null)
                    return byHint;
            }

            return assets.Find(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        }

        static Version? ParseVersionFromRelease(string tagName, string releaseName)
        {
            var tagVersion = ParseVersion(tagName);
            if (tagVersion != null)
                return tagVersion;

            return ParseVersion(releaseName);
        }

        static Version? ParseVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var match = Regex.Match(value, @"(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?");
            if (!match.Success)
                return null;

            var major = int.Parse(match.Groups[1].Value);
            var minor = int.Parse(match.Groups[2].Value);
            var build = int.Parse(match.Groups[3].Value);
            var revision = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
            return new Version(major, minor, build, revision);
        }

        static string GetString(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString() ?? string.Empty;

            return string.Empty;
        }

        static bool GetBool(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False))
                return value.GetBoolean();

            return false;
        }

        static long GetLong(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var parsed))
                return parsed;

            return 0;
        }

        static DateTimeOffset GetDate(JsonElement obj, string name)
        {
            if (obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var parsed))
            {
                return parsed;
            }

            return DateTimeOffset.MinValue;
        }
    }
}
