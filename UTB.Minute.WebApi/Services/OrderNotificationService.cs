using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace UTB.Minute.WebApi.Services;

/// <summary>
/// Broadcasts order events to all SSE subscribers. When Notify is called, all waiting
/// subscribers receive the same event (sync pattern like the article's FoodService).
/// </summary>
public class OrderNotificationService : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _current = string.Empty;

    public void Notify(string message)
    {
        _current = message;
        OnPropertyChanged();
    }

    public async IAsyncEnumerable<string> GetEvents(
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var tcs = new TaskCompletionSource();
            PropertyChangedEventHandler handler = (_, _) => tcs.TrySetResult();
            PropertyChanged += handler;
            try
            {
                await tcs.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            finally
            {
                PropertyChanged -= handler;
            }
            yield return _current;
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
