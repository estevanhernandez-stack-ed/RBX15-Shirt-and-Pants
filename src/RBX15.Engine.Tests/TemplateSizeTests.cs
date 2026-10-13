using RBX15.Engine;

namespace RBX15.Engine.Tests;

public class TemplateSizeTests
{
    [Fact]
    public void Template_is_the_Roblox_classic_585_by_559()
    {
        Assert.Equal(585, TemplateSize.Width);
        Assert.Equal(559, TemplateSize.Height);
    }
}
