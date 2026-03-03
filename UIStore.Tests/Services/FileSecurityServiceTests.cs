using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using UIStore.Services;
using Xunit;

namespace UIStore.Tests.Services {
    public class FileSecurityServiceTests {
        private static IFormFile CreateZipFile(Action<ZipArchive> configure) {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true)) { configure(archive); }
            ms.Position = 0;
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.OpenReadStream()).Returns(ms);
            mockFile.Setup(f => f.Length).Returns(ms.Length);
            return mockFile.Object;
        }

        private static FileSecurityService CreateSvc() {
            var settingsSvc = new Mock<SystemSettingService>(Mock.Of<IServiceProvider>(), Mock.Of<CacheService>());
            settingsSvc.Setup(s => s.GetSettingAsync("Security_MaxZipSizeMB", 500)).ReturnsAsync(500);
            settingsSvc.Setup(s => s.GetSettingAsync("Security_MaxZipEntries", 5000)).ReturnsAsync(5000);
            settingsSvc.Setup(s => s.GetSettingAsync("Security_DangerousExts", It.IsAny<string[]>())).ReturnsAsync(new[] { ".exe", ".dll", ".bat", ".php" });
            return new FileSecurityService(settingsSvc.Object);
        }

        [Fact]
        public async Task ScanZipAsync_ValidZip_ReturnsSafe() {
            var file = CreateZipFile(archive => {
                var entry = archive.CreateEntry("index.html"); using var s = entry.Open(); s.Write(Encoding.UTF8.GetBytes("<html></html>"));
            });
            var svc = CreateSvc();
            var result = await svc.ScanZipContentAsync(file);
            Assert.True(result.IsSafe);
            Assert.False(result.IsMalicious);
        }

        [Fact]
        public async Task ScanZipAsync_DangerousFile_ReturnsMalicious() {
            var file = CreateZipFile(archive => {
                var entry = archive.CreateEntry("malware.exe"); using var s = entry.Open(); s.Write(new byte[100]);
            });
            var svc = CreateSvc();
            var result = await svc.ScanZipContentAsync(file);
            Assert.False(result.IsSafe);
            Assert.True(result.IsMalicious);
        }

        [Fact]
        public async Task ScanZipAsync_PathTraversal_ReturnsMalicious() {
            var file = CreateZipFile(archive => {
                var entry = archive.CreateEntry("../etc/passwd"); using var s = entry.Open(); s.Write(Encoding.UTF8.GetBytes("root:x:0:0"));
            });
            var svc = CreateSvc();
            var result = await svc.ScanZipContentAsync(file);
            Assert.False(result.IsSafe);
            Assert.True(result.IsMalicious);
        }

        [Fact]
        public async Task ScanZipAsync_EmptyZip_ReturnsNotSafe() {
            var file = CreateZipFile(archive => { }); // empty
            var svc = CreateSvc();
            var result = await svc.ScanZipContentAsync(file);
            Assert.False(result.IsSafe);
        }
    }
}
