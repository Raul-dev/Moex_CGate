using ContextSync.Scenarios;
using Scriban;
using Scriban.Runtime;
using Xunit;

namespace ContextSync.Tests.Scenarios;

public class ScribanEvaluatorTests
{
    private static ScriptObject Root()
    {
        var def = new ScenarioDefinition { Name = "eval", Inputs = { ["file"] = "some-file.txt" } };
        var ctx = new ScenarioContext(def);
        return ctx.Root;
    }

    [Fact]
    public void RendersPlainString()
    {
        Assert.Equal("hello", ScribanEvaluator.RenderString("hello", Root()));
    }

    [Fact]
    public void RendersEnvFunction()
    {
        Environment.SetEnvironmentVariable("CTX_TEST_TOKEN", "abc123");
        var value = ScribanEvaluator.RenderString("{{ env 'CTX_TEST_TOKEN' }}", Root());
        Assert.Equal("abc123", value);
    }

    [Fact]
    public void RendersInputs()
    {
        Assert.Equal("some-file.txt", ScribanEvaluator.RenderString("{{ inputs.file }}", Root()));
    }

    [Fact]
    public void CustomDefFillsNull()
    {
        var root = Root();
        var item = new ScriptObject();
        root["item"] = item;
        var result = ScribanEvaluator.RenderString("{{ item.hours | def 6 }}h", root);
        Assert.Equal("6h", result);
    }

    [Fact]
    public void InvalidTemplateThrows()
    {
        Assert.Throws<ScenarioStepException>(() => ScribanEvaluator.RenderString("{{ item.hours && }}", Root()));
    }
}
