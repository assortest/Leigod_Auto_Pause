using System.Xml.Linq;

namespace Leigod_Auto_Pause.Tests.Packaging;

public class PublishSettingsTests
{
    [Fact]
    public void ReleaseProfile_EnablesSingleFileSelfContainedPublish()
    {
        var projectRoot = ResolveProjectRoot();
        var profilePath = Path.Combine(
            projectRoot,
            "src",
            "Leigod_Auto_Pause",
            "Properties",
            "PublishProfiles",
            "ReleaseSingleFile.pubxml");

        var doc = XDocument.Load(profilePath);
        var values = doc.Descendants()
            .Where(x => x.Parent is not null && x.Parent.Name.LocalName == "PropertyGroup")
            .ToDictionary(x => x.Name.LocalName, x => x.Value);

        Assert.Equal("true", values["PublishSingleFile"]);
        Assert.Equal("true", values["SelfContained"]);
        Assert.Equal("win-x64", values["RuntimeIdentifier"]);
    }

    private static string ResolveProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
