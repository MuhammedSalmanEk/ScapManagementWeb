namespace ScrapManagement.Web.Modules;

/// <summary>
/// Registry of business modules shown in the navigation.
/// To release the Sales module: build it inside Areas/Sales, then set Implemented = true here.
/// Each shop gets it only when its plan has SalesModuleEnabled (main admin controls this).
/// </summary>
public record AppModule(string Key, string Title, string Area, string Controller, string Action, bool Implemented);

public static class AppModules
{
    public static readonly AppModule Sales = new("sales", "Sales", "Sales", "Sales", "Index", Implemented: false);
    public static IReadOnlyList<AppModule> All { get; } = new[] { Sales };
}
