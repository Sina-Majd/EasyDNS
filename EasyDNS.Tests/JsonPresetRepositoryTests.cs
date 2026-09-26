using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EasyDNS.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace EasyDNS.Tests
{
    public class JsonPresetRepositoryTests
    {
        [Fact]
        public void Repository_ShouldInitializeWithDefaultPresets()
        {
            var repo = new JsonPresetRepository();
            var presets = repo.GetAllPresets();

            presets.Should().NotBeEmpty();
            presets.Should().Contain(p => p.Name.Contains("Cloudflare"));
            presets.Should().Contain(p => p.Name.Contains("Google"));
            presets.Should().Contain(p => p.Name.Contains("Quad9"));
        }

        [Fact]
        public void Repository_ShouldProvideValidCategories()
        {
            var repo = new JsonPresetRepository();
            var categories = repo.GetCategories();

            categories.Should().Contain(new[] { "All", "General", "Security", "Gaming" });
        }

        [Fact]
        public async Task AddCustomPreset_ShouldAddAndPersist()
        {
            var repo = new JsonPresetRepository();
            string testName = "Test DNS " + System.Guid.NewGuid().ToString("N")[..6];

            await repo.AddCustomPresetAsync(testName, "1.2.3.4", "1.2.3.5", "Unit test description");

            var presets = repo.GetAllPresets();
            var added = presets.FirstOrDefault(p => p.Name == testName);
            added.Should().NotBeNull();
            added!.PrimaryDns.Should().Be("1.2.3.4");
            added.IsCustom.Should().BeTrue();

            // Cleanup
            await repo.DeleteCustomPresetAsync(added.Id);
            repo.GetAllPresets().Should().NotContain(p => p.Id == added.Id);
        }

        [Fact]
        public async Task ExportAndImport_ShouldPreservePresets()
        {
            var repo = new JsonPresetRepository();
            string tempFile = Path.Combine(Path.GetTempPath(), $"easydns_test_{System.Guid.NewGuid():N}.json");

            try
            {
                string testName = "ExportTest_" + System.Guid.NewGuid().ToString("N")[..6];
                await repo.AddCustomPresetAsync(testName, "10.0.0.1", "10.0.0.2", "Export test");

                var exportResult = await repo.ExportCustomPresetsAsync(tempFile);
                exportResult.Success.Should().BeTrue();
                File.Exists(tempFile).Should().BeTrue();

                // Delete it from repo
                var preset = repo.GetAllPresets().First(p => p.Name == testName);
                await repo.DeleteCustomPresetAsync(preset.Id);

                // Now import it back
                var (importResult, count) = await repo.ImportCustomPresetsAsync(tempFile);
                importResult.Success.Should().BeTrue();
                count.Should().BeGreaterThan(0);

                var reloaded = repo.GetAllPresets().FirstOrDefault(p => p.Name == testName);
                reloaded.Should().NotBeNull();

                // Cleanup
                if (reloaded != null)
                {
                    await repo.DeleteCustomPresetAsync(reloaded.Id);
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
