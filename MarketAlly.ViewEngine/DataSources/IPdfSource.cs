namespace MarketAlly.Maui.ViewEngine.DataSources;

public interface IPdfSource
{
    Task<string> GetFilePathAsync();
}