using System;
using System.IO;
using Xunit;

namespace WsprPc.Tests;

public class AppConfigTests
{
    [Fact]
    public void Load_AllowsEmptyLastUpdateCheckUtc()
    {
        string temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, "{\n  \"LastUpdateCheckUtc\": \"\"\n}");
            var config = AppConfig.Load(temp);
            Assert.Null(config.LastUpdateCheckUtc);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Load_AllowsInvalidLastUpdateCheckUtc()
    {
        string temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, "{\n  \"LastUpdateCheckUtc\": \"not-a-date\"\n}");
            var config = AppConfig.Load(temp);
            Assert.Null(config.LastUpdateCheckUtc);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Fact]
    public void Load_MissingLastUpdateCheckUtc_DoesNotThrow()
    {
        string temp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(temp, "{\n  \"UpdateRepoName\": \"TapScribePC-Public\"\n}");
            var config = AppConfig.Load(temp);
            Assert.Null(config.LastUpdateCheckUtc);
        }
        finally
        {
            File.Delete(temp);
        }
    }
}
