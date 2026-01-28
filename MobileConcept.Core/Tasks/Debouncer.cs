namespace MobileConcept.Core.Tasks;

public class Debouncer
{
    private CancellationTokenSource _cancellationTokenSource;
    private readonly int _delayMilliseconds;

    public Debouncer(int delayMilliseconds = 300)
    {
        _delayMilliseconds = delayMilliseconds;
    }

    public async Task DebounceAsync(Func<CancellationToken, Task> action)
    {
        // Cancel any pending operation
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        
        // Create new cancellation token
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        try
        {
            // Wait for the delay period
            await Task.Delay(_delayMilliseconds, token);
            
            // If not cancelled, execute the action with the token
            if (!token.IsCancellationRequested)
            {
                await action(token);
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when debounce is triggered again
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}

/*
private async void OnMapViewChanged(object sender, EventArgs e)
   {
       await _mapDebouncer.DebounceAsync(async (cancellationToken) => 
       {
           await LoadPinsForCurrentView(cancellationToken);
       });
   }
   
   private async Task LoadPinsForCurrentView(CancellationToken cancellationToken)
   {
       var bounds = GetCurrentMapBounds();
       
       // Pass token to async operations
       var pins = await FetchPinsInBounds(bounds, cancellationToken);
       
       // Check before updating UI
       if (!cancellationToken.IsCancellationRequested)
       {
           UpdateMapPins(pins);
       }
   }
*/