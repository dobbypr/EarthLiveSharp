using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EarthLiveSharp.Core;
using EarthLiveSharp.Core.Satellites;
using Xunit;

namespace EarthLiveSharp.Tests
{
    public class ConfigTests
    {
        [Fact]
        public void Config_Defaults_AreReasonable()
        {
            var cfg = new Config();
            Assert.Equal("Himawari9", cfg.Satellite);
            Assert.Equal(20, cfg.IntervalMinutes);
            Assert.Equal(1, cfg.Size);
            Assert.Equal(100, cfg.Zoom);
            Assert.True(cfg.SetWallpaper);
            Assert.False(cfg.Autostart);
        }

        [Fact]
        public void Config_SaveAndLoad_RoundTrips()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), $"els_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            try
            {
                // Override XDG_CONFIG_HOME so Config uses our temp directory
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmpDir);

                var original = new Config
                {
                    Satellite = "NasaEpic",
                    IntervalMinutes = 30,
                    Size = 4,
                    Zoom = 75,
                    SetWallpaper = false
                };
                original.Save();

                var loaded = Config.Load();
                Assert.Equal("NasaEpic", loaded.Satellite);
                Assert.Equal(30, loaded.IntervalMinutes);
                Assert.Equal(4, loaded.Size);
                Assert.Equal(75, loaded.Zoom);
                Assert.False(loaded.SetWallpaper);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
                Directory.Delete(tmpDir, recursive: true);
            }
        }

        [Fact]
        public void Config_Load_ReturnsDefaults_WhenFileAbsent()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), $"els_test_{Guid.NewGuid():N}");
            // Do NOT create the directory — simulate missing config
            try
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", tmpDir);
                var cfg = Config.Load();
                Assert.Equal("Himawari9", cfg.Satellite);
            }
            finally
            {
                Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", null);
            }
        }

        [Fact]
        public void Config_SaveAndLoad_UsesExplicitConfigPathOverride()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), $"els_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tmpDir);
            string configPath = Path.Combine(tmpDir, "custom-config.json");

            try
            {
                Environment.SetEnvironmentVariable("EARTHLIVESHARP_CONFIG_PATH", configPath);

                var original = new Config { Satellite = "NasaEpic", IntervalMinutes = 5 };
                original.Save();

                Assert.True(File.Exists(configPath));
                var loaded = Config.Load();
                Assert.Equal("NasaEpic", loaded.Satellite);
                Assert.Equal(5, loaded.IntervalMinutes);
            }
            finally
            {
                Environment.SetEnvironmentVariable("EARTHLIVESHARP_CONFIG_PATH", null);
                Directory.Delete(tmpDir, recursive: true);
            }
        }
    }

    public class EpicSourceTests
    {
        [Fact]
        public async Task GetLatestImageIdAsync_ParsesValidResponse()
        {
            string json = """
                [
                  {
                    "image": "epic_1b_20250101120000",
                    "date": "2025-01-01 12:00:00",
                    "caption": "Test"
                  }
                ]
                """;

            var handler = new FakeHttpHandler(json, "application/json");
            using var http = new HttpClient(handler);
            var source = new EpicSource(http);

            string? id = await source.GetLatestImageIdAsync();

            Assert.NotNull(id);
            Assert.Contains("epic_1b_20250101120000", id);
            Assert.Contains("2025-01-01 12:00:00", id);
        }

        [Fact]
        public async Task GetLatestImageIdAsync_ReturnsNull_OnEmptyArray()
        {
            var handler = new FakeHttpHandler("[]", "application/json");
            using var http = new HttpClient(handler);
            var source = new EpicSource(http);

            string? id = await source.GetLatestImageIdAsync();
            Assert.Null(id);
        }

        [Fact]
        public async Task GetLatestImageIdAsync_ReturnsNull_OnHttpError()
        {
            var handler = new FakeHttpHandler("", statusCode: HttpStatusCode.InternalServerError);
            using var http = new HttpClient(handler);
            var source = new EpicSource(http);

            string? id = await source.GetLatestImageIdAsync();
            Assert.Null(id);
        }

        [Fact]
        public void ResetState_DoesNotThrow()
        {
            var handler = new FakeHttpHandler("[]", "application/json");
            using var http = new HttpClient(handler);
            var source = new EpicSource(http);
            source.ResetState(); // should not throw
        }
    }

    public class HimawariSourceTests
    {
        [Fact]
        public async Task GetLatestImageIdAsync_ParsesValidResponse()
        {
            string json = """{"timestamps_int":[20250101120000,20250101110000]}""";
            var handler = new FakeHttpHandler(json, "application/json");
            using var http = new HttpClient(handler);
            var source = new HimawariSource(http);

            string? id = await source.GetLatestImageIdAsync();
            Assert.Equal("20250101120000", id);
        }

        [Fact]
        public async Task GetLatestImageIdAsync_ReturnsNull_OnHttpError()
        {
            var handler = new FakeHttpHandler("", statusCode: HttpStatusCode.ServiceUnavailable);
            using var http = new HttpClient(handler);
            var source = new HimawariSource(http);

            string? id = await source.GetLatestImageIdAsync();
            Assert.Null(id);
        }

        [Fact]
        public void ResetState_DoesNotThrow()
        {
            var handler = new FakeHttpHandler("{}", "application/json");
            using var http = new HttpClient(handler);
            var source = new HimawariSource(http);
            source.ResetState();
        }
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    internal sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _contentType;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpHandler(string body, string contentType = "text/plain",
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            _body = body;
            _contentType = contentType;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, _contentType)
            };
            return Task.FromResult(response);
        }
    }
}
