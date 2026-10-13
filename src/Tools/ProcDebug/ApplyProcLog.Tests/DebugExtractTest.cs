using System.IO;
using System.Reflection;
using ApplyProcLog.dal;
using Xunit;

namespace ApplyProcLog.Tests;

public class DebugExtractTest
{
    [Fact]
    public void Debug_Version10Body()
    {
        string body = File.ReadAllText(@"Calculate_10.txt");

        var type = typeof(DBHelper);
        var method = type.GetMethod("ExtractParamsFromBody", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("not found");
        var removeMethod = type.GetMethod("RemoveSqlComments", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new Exception("RemoveSqlComments not found");

        string noComments = (string)removeMethod.Invoke(null, new object[] { body })!;

        // Сохраняем промежуточный результат
        File.WriteAllText(@"_no_comments.txt", noComments);

        string result = (string)method.Invoke(null, new object[] { body })!;
        File.WriteAllText(@"_result.txt", result);

        // Проверяем что result содержит параметры
        Assert.Contains("@type", result);
        Assert.Contains("@nnFirm", result);
    }
}
