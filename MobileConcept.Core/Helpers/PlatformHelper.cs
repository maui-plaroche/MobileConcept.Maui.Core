namespace MobileConcept.Core.Helpers;

public static class PlatformHelper
{
    public static Application? GetApplication() 
        => IPlatformApplication.Current?.Application as Application;
    
    public static Window? GetCurrentWindow() 
        => GetApplication()?.Windows.FirstOrDefault();
    
    public static Page? GetMainPage() 
        => GetCurrentWindow()?.Page;
    
    public static IServiceProvider? GetServices() 
        => IPlatformApplication.Current?.Services;
}