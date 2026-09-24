using System.Reflection;
using TcgStockBot.WebApi.Features.StockTasks.Models.DTOs;

namespace TcgStockBot.UnitTest.Sdk;

/// <summary>
/// The Sdk models are hand-kept copies of the WebApi DTOs (like a generated SDK). This test fails as soon as
/// a DTO gains, loses or retypes a property without the Sdk copy following.
/// </summary>
public class SdkContractTests
{
    private static readonly Assembly WebApiAssembly = typeof(StockTaskDTO).Assembly;
    private static readonly Assembly SdkAssembly = typeof(TcgStockBot.Sdk.TcgStockBotClient).Assembly;

    public static TheoryData<string> ContractTypes()
    {
        var names = SdkAssembly.GetTypes()
            .Where(static type => type.Namespace == "TcgStockBot.Sdk.Models" && type.IsPublic)
            .Select(static type => type.Name);
        var data = new TheoryData<string>();
        foreach (var name in names)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ContractTypes))]
    public void SdkModel_MatchesTheWebApiDto(string typeName)
    {
        var sdkType = SdkAssembly.GetTypes().Single(type => type.Namespace == "TcgStockBot.Sdk.Models" && type.Name == typeName);
        var apiType = WebApiAssembly.GetTypes().SingleOrDefault(type => type.Name == typeName && type.Namespace?.EndsWith(".Models.DTOs", StringComparison.Ordinal) == true);

        Assert.True(apiType is not null, $"No WebApi DTO named {typeName}.");
        Assert.Equal(Describe(apiType), Describe(sdkType));
    }

    [Fact]
    public void EveryWebApiDto_HasAnSdkModel()
    {
        var sdkNames = SdkAssembly.GetTypes().Where(static type => type.Namespace == "TcgStockBot.Sdk.Models").Select(static type => type.Name).ToHashSet();
        var missing = WebApiAssembly.GetTypes()
            .Where(static type => type.IsPublic && type.Namespace?.EndsWith(".Models.DTOs", StringComparison.Ordinal) == true)
            .Select(static type => type.Name)
            .Where(name => !sdkNames.Contains(name))
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>"Name:Type" per public property; nested DTO types are compared by name.</summary>
    private static List<string> Describe(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => $"{property.Name}:{TypeName(property.PropertyType)}")
            .Order(StringComparer.Ordinal)
            .ToList();

    private static string TypeName(Type type) => type.IsGenericType
        ? $"{type.Name}<{string.Join(",", type.GetGenericArguments().Select(TypeName))}>"
        : type.FullName!.StartsWith("TcgStockBot.WebApi", StringComparison.Ordinal) || type.FullName.StartsWith("TcgStockBot.Sdk", StringComparison.Ordinal)
            ? type.Name
            : type.FullName;
}
