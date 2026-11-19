namespace FactoryERP.Web.Services;

public interface ILoadingService
{
    event Action? OnStateChanged;
    bool IsLoading { get; }
    string Message { get; }
    void Show(string message = "Procesando...");
    void Hide();
}

public sealed class LoadingService : ILoadingService
{
    private int _depth;

    public event Action? OnStateChanged;
    public bool IsLoading => _depth > 0;
    public string Message { get; private set; } = "Procesando...";

    public void Show(string message = "Procesando...")
    {
        Message = message;
        _depth++;
        OnStateChanged?.Invoke();
    }

    public void Hide()
    {
        if (_depth > 0) _depth--;
        OnStateChanged?.Invoke();
    }
}
