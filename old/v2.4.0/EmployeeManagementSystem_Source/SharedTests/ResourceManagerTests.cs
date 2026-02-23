using System.IO;
using Shared.Utils;

namespace SharedTests;

public class ResourceManagerTests
{
    private static string GetResourcesPath()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var paths = new[]
        {
            Path.Combine(baseDir, "..", "..", "SharedData", "resources.xml"),
            Path.Combine(baseDir, "..", "..", "..", "SharedData", "resources.xml"),
            Path.Combine(baseDir, "..", "..", "..", "..", "SharedData", "resources.xml"),
            Path.Combine(baseDir, "SharedData", "resources.xml"),
            @"e:\projects\employee_management_csharp\SharedData\resources.xml",
        };
        foreach (var p in paths)
        {
            var full = Path.GetFullPath(p);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    [Fact]
    public void LoadResources_FromSharedData_LoadsDisplaySupervisorAndNoSupervisor()
    {
        var path = GetResourcesPath();
        Assert.NotNull(path);

        ResourceManager.LoadResources(path);

        var displaySupervisor = ResourceManager.GetString("display_supervisor", "");
        var displayNoSupervisor = ResourceManager.GetString("display_no_supervisor", "");

        // Verify keys exist and have expected format (placeholder {0} for supervisor name)
        Assert.Contains("{0}", displaySupervisor);
        Assert.False(string.IsNullOrEmpty(displayNoSupervisor));
        // Verify we got values from file, not fallbacks
        Assert.DoesNotContain("[display_supervisor]", displaySupervisor);
        Assert.DoesNotContain("[display_no_supervisor]", displayNoSupervisor);
    }

    private static string? GetResourcesFaPath()
    {
        var baseDir = Directory.GetCurrentDirectory();
        var paths = new[]
        {
            Path.Combine(baseDir, "..", "..", "SharedData", "resources.fa.xml"),
            Path.Combine(baseDir, "..", "..", "..", "SharedData", "resources.fa.xml"),
            Path.Combine(baseDir, "..", "..", "..", "..", "SharedData", "resources.fa.xml"),
            Path.Combine(baseDir, "SharedData", "resources.fa.xml"),
            @"e:\projects\employee_management_csharp\SharedData\resources.fa.xml",
        };
        foreach (var p in paths)
        {
            var full = Path.GetFullPath(p);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    [Fact]
    public void LoadResources_FromResourcesFa_LoadsPersianStrings()
    {
        var path = GetResourcesFaPath();
        Assert.NotNull(path);

        ResourceManager.LoadResources(path);

        var appTitle = ResourceManager.GetString("app_title", "");
        var shiftMorning = ResourceManager.GetString("shift_morning", "");
        var msgError = ResourceManager.GetString("msg_error", "");

        // Persian values (same keys as English file)
        Assert.False(string.IsNullOrEmpty(appTitle));
        Assert.False(string.IsNullOrEmpty(shiftMorning));
        Assert.False(string.IsNullOrEmpty(msgError));
        Assert.DoesNotContain("[app_title]", appTitle);
        // Persian "Error" is "خطا", "Morning" is "صبح"
        Assert.Equal("صبح", shiftMorning);
    }
}
