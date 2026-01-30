
namespace MobileConcept.Core.Tasks;

using System.Collections.Concurrent;

/// <summary>
/// TaskPool class that helps you execute async tasks in a pool with limited concurrency.
/// Supports completion callbacks for task lifecycle management.
/// </summary>
public class TaskPool : IDisposable
{
    private readonly HashSet<IInternalTask> _workingTasks = [];
    private readonly ConcurrentQueue<IInternalTask> _queue = new();
    private readonly SemaphoreSlim _semaphore;
    private readonly object _tasksMutex = new();
    private readonly CancellationTokenSource _disposalTokenSource = new();
    private bool _disposed;

    /// <summary>
    /// Gets the maximum number of concurrent threads allowed in the pool.
    /// </summary>
    public int ThreadsMaxCount { get; }

    private interface IInternalTask
    {
        /// <summary>
        /// Gets or sets the completion callback to execute after the task finishes.
        /// </summary>
        Action? OnComplete { get; set; }

        Task ExecuteAsync(CancellationToken cancellationToken);
    }

    private sealed class InternalTaskHolder : IInternalTask
    {
        public required Func<Task> Task { get; init; }
        public required TaskCompletionSource<object?> Waiter { get; init; }
        public Action? OnComplete { get; set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Waiter.SetCanceled(cancellationToken);
                    return;
                }

                await Task();
                Waiter.SetResult(null);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                Waiter.SetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                Waiter.SetException(ex);
            }
            finally
            {
                // Execute completion callback if provided
                OnComplete?.Invoke();
            }
        }
    }

    private sealed class InternalTaskHolderGeneric<T> : IInternalTask
    {
        public required Func<Task<T>> Task { get; init; }
        public required TaskCompletionSource<T> Waiter { get; init; }
        public Action? OnComplete { get; set; }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Waiter.SetCanceled(cancellationToken);
                    return;
                }

                var result = await Task();
                Waiter.SetResult(result);
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
            {
                Waiter.SetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                Waiter.SetException(ex);
            }
            finally
            {
                // Execute completion callback if provided
                OnComplete?.Invoke();
            }
        }
    }

    /// <summary>
    /// Raised when all tasks have been completed.
    /// </summary>
    public event EventHandler? Completed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskPool"/> class.
    /// </summary>
    /// <param name="threadsMaxCount">The maximum number of concurrent tasks.</param>
    /// <exception cref="ArgumentException">Thrown when threadsMaxCount is less than or equal to 0.</exception>
    public TaskPool(int threadsMaxCount)
    {
        if (threadsMaxCount <= 0)
            throw new ArgumentException("Thread count must be greater than 0", nameof(threadsMaxCount));

        ThreadsMaxCount = threadsMaxCount;
        _semaphore = new SemaphoreSlim(threadsMaxCount, threadsMaxCount);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskPool"/> class with initial tasks.
    /// </summary>
    /// <param name="threadsMaxCount">The maximum number of concurrent tasks.</param>
    /// <param name="tasks">The initial tasks to enqueue.</param>
    public TaskPool(int threadsMaxCount, IEnumerable<Func<Task>> tasks)
        : this(threadsMaxCount)
    {
        foreach (var task in tasks)
        {
            _queue.Enqueue(new InternalTaskHolder
            {
                Task = task,
                Waiter = new TaskCompletionSource<object?>()
            });
        }

        ProcessQueue();
    }

    /// <summary>
    /// Adds a task and runs it if a free thread exists. Otherwise, enqueues.
    /// </summary>
    /// <param name="task">The task that will be executed.</param>
    /// <param name="onComplete">Optional callback to execute when the task completes.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task EnqueueAsync(Func<Task> task, Action? onComplete = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var holder = new InternalTaskHolder
        {
            Task = task,
            Waiter = new TaskCompletionSource<object?>(),
            OnComplete = onComplete
        };

        lock (_tasksMutex)
        {
            _queue.Enqueue(holder);
            ProcessQueue();
        }

        return holder.Waiter.Task;
    }

    /// <summary>
    /// Adds a task and runs it if a free thread exists. Otherwise, enqueues.
    /// </summary>
    /// <typeparam name="T">The return type of the task.</typeparam>
    /// <param name="task">The task that will be executed.</param>
    /// <param name="onComplete">Optional callback to execute when the task completes.</param>
    /// <returns>A task representing the asynchronous operation with a result.</returns>
    public Task<T> EnqueueAsync<T>(Func<Task<T>> task, Action? onComplete = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var holder = new InternalTaskHolderGeneric<T>
        {
            Task = task,
            Waiter = new TaskCompletionSource<T>(),
            OnComplete = onComplete
        };

        lock (_tasksMutex)
        {
            _queue.Enqueue(holder);
            ProcessQueue();
        }

        return holder.Waiter.Task;
    }

    /// <summary>
    /// Adds a task with a result-aware completion callback.
    /// </summary>
    /// <typeparam name="T">The return type of the task.</typeparam>
    /// <param name="task">The task that will be executed.</param>
    /// <param name="onComplete">Callback to execute when the task completes, receiving the result.</param>
    /// <returns>A task representing the asynchronous operation with a result.</returns>
    public Task<T> EnqueueAsync<T>(Func<Task<T>> task, Action<T>? onComplete)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var waiter = new TaskCompletionSource<T>();

        var holder = new InternalTaskHolderGeneric<T>
        {
            Task = task,
            Waiter = waiter,
            OnComplete = onComplete != null 
                ? () => 
                {
                    if (waiter.Task.IsCompletedSuccessfully)
                    {
                        onComplete(waiter.Task.Result);
                    }
                } 
                : null
        };

        lock (_tasksMutex)
        {
            _queue.Enqueue(holder);
            ProcessQueue();
        }

        return holder.Waiter.Task;
    }

    /// <summary>
    /// Processes the queue and starts tasks up to the max thread count.
    /// </summary>
    private void ProcessQueue()
    {
        while (!_queue.IsEmpty && _workingTasks.Count < ThreadsMaxCount)
        {
            if (!_queue.TryDequeue(out var task))
                break;

            _workingTasks.Add(task);
            _ = StartTaskAsync(task);
        }
    }

    /// <summary>
    /// Starts the execution of a task.
    /// </summary>
    /// <param name="task">The task that should be executed.</param>
    private async Task StartTaskAsync(IInternalTask task)
    {
        try
        {
            await task.ExecuteAsync(_disposalTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception in task pool: {ex}");
        }
        finally
        {
            TaskCompleted(task);
        }
    }

    private void TaskCompleted(IInternalTask task)
    {
        lock (_tasksMutex)
        {
            _workingTasks.Remove(task);
            ProcessQueue();

            if (_queue.IsEmpty && _workingTasks.Count == 0)
            {
                OnCompleted();
            }
        }
    }

    /// <summary>
    /// Waits for all currently queued and running tasks to complete.
    /// </summary>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    public async Task WaitForCompletionAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        EventHandler? handler = null;

        handler = (sender, args) =>
        {
            Completed -= handler;
            tcs.SetResult(true);
        };

        lock (_tasksMutex)
        {
            if (_queue.IsEmpty && _workingTasks.Count == 0)
            {
                return;
            }

            Completed += handler;
        }

        await tcs.Task;
    }

    /// <summary>
    /// Raises the Completed event.
    /// </summary>
    protected virtual void OnCompleted()
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes the task pool and cancels all pending tasks.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _disposalTokenSource.Cancel();
        _disposalTokenSource.Dispose();
        _semaphore.Dispose();
    }
}

/*
Usage Example:

// Create a TaskPool
using var taskPool = new TaskPool(8);

var waitingTasks = new List<Task<string>>();

for (int i = 0; i < 10; i++)
{
    int taskId = i;

    // Option 1: Simple completion callback
    var task = taskPool.EnqueueAsync(async () =>
    {
        Console.WriteLine($"Task {taskId} started.");
        await Task.Delay(1000);
        return $"path/result/{taskId}";
    }, onComplete: () => Console.WriteLine($"Task {taskId} finished!"));

    // Option 2: Result-aware completion callback
    var task2 = taskPool.EnqueueAsync(async () =>
    {
        await Task.Delay(500);
        return $"result_{taskId}";
    }, onComplete: result => Console.WriteLine($"Got result: {result}"));

    waitingTasks.Add(task);
}

await taskPool.WaitForCompletionAsync();
Console.WriteLine("All tasks completed!");
   
   using MobileConcept.Core.Tasks;
   
   // Example 1: Basic usage with 1000 tasks
   async Task BasicUsageAsync()
   {
       using var taskPool = new TaskPool(8);
       
       var results = new List<Task<int>>();
       
       for (int i = 0; i < 1000; i++)
       {
           int taskId = i; // Capture for closure
           var task = taskPool.EnqueueAsync(async () =>
           {
               await Task.Delay(Random.Shared.Next(50, 200)); // Simulate work
               return taskId * 2;
           });
           results.Add(task);
       }
       
       await taskPool.WaitForCompletionAsync();
       Console.WriteLine($"All 1000 tasks completed. Sum: {results.Sum(t => t.Result)}");
   }
   
   // Example 2: With completion callbacks
   async Task AdvancedUsageAsync()
   {
       int completedCount = 0;
       object lockObj = new();

       using var taskPool = new TaskPool(16);

       var results = new List<Task<string>>();

       for (int i = 0; i < 1000; i++)
       {
           int taskId = i;

           var task = taskPool.EnqueueAsync(
               async () =>
               {
                   // Simulate API call or heavy work
                   await Task.Delay(Random.Shared.Next(100, 500));
                   return $"Result_{taskId}";
               },
               onComplete: result =>
               {
                   lock (lockObj)
                   {
                       completedCount++;
                       if (completedCount % 100 == 0)
                       {
                           Console.WriteLine($"Progress: {completedCount}/1000 - Last result: {result}");
                       }
                   }
               }
           );

           results.Add(task);
       }

       await taskPool.WaitForCompletionAsync();
       Console.WriteLine($"All done! Completed: {completedCount}");
   }
   
   // Example 3: Download simulation with progress tracking
   async Task DownloadSimulationAsync()
   {
       int successCount = 0;
       int errorCount = 0;
       object lockObj = new();

       using var taskPool = new TaskPool(10);
       
       var downloadTasks = new List<Task<(int Id, bool Success, string Path)>>();
       
       for (int i = 0; i < 1000; i++)
       {
           int fileId = i;
           
           var task = taskPool.EnqueueAsync(
               async () =>
               {
                   try
                   {
                       // Simulate file download
                       await Task.Delay(Random.Shared.Next(50, 300));
                       
                       // Simulate occasional failures (5% failure rate)
                       if (Random.Shared.Next(100) < 5)
                           throw new Exception($"Download failed for file {fileId}");
                       
                       return (fileId, true, $"/downloads/file_{fileId}.dat");
                   }
                   catch
                   {
                       return (fileId, false, string.Empty);
                   }
               },
               onComplete: result =>
               {
                   lock (lockObj)
                   {
                       if (result.Success)
                           successCount++;
                       else
                           errorCount++;
                       
                       int total = successCount + errorCount;
                       if (total % 100 == 0)
                       {
                           Console.WriteLine($"Downloaded: {total}/1000 (Success: {successCount}, Errors: {errorCount})");
                       }
                   }
               }
           );
           
           downloadTasks.Add(task);
       }
       
       await taskPool.WaitForCompletionAsync();
       
       var successfulDownloads = downloadTasks
           .Select(t => t.Result)
           .Where(r => r.Success)
           .ToList();
       
       Console.WriteLine($"Download complete!");
       Console.WriteLine($"  Success: {successCount}");
       Console.WriteLine($"  Errors: {errorCount}");
   }
   
   // Example 4: Process items from a list
   async Task ProcessItemsAsync()
   {
       var items = Enumerable.Range(1, 1000)
           .Select(i => new { Id = i, Name = $"Item_{i}" })
           .ToList();
       
       var processedItems = new ConcurrentBag<string>();
       
       using var taskPool = new TaskPool(12);
       
       foreach (var item in items)
       {
           var capturedItem = item;
           
           await taskPool.EnqueueAsync(
               async () =>
               {
                   // Process item
                   await Task.Delay(Random.Shared.Next(10, 100));
                   var result = $"Processed: {capturedItem.Name}";
                   processedItems.Add(result);
                   return result;
               },
               onComplete: () =>
               {
                   if (processedItems.Count % 200 == 0)
                   {
                       Console.WriteLine($"Processed {processedItems.Count} items...");
                   }
               }
           );
       }
       
       await taskPool.WaitForCompletionAsync();
       Console.WriteLine($"Total processed: {processedItems.Count}");
   }
   
   // Run examples
   await BasicUsageAsync();
   await AdvancedUsageAsync();
   await DownloadSimulationAsync();
   await ProcessItemsAsync();
*/