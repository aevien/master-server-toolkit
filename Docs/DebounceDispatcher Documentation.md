# DebounceDispatcher Documentation

## Introduction

`DebounceDispatcher` is a class that implements the "debounce" pattern for C#, which allows delaying the execution of a function until a specified time interval has passed since the last call. This is useful for optimizing performance in scenarios with frequent calls, such as processing user input, API requests, or UI updates.

## Contents

- Overview of Classes
- Installation
- Usage Examples
  - Basic Debounce with Action
  - Asynchronous Debounce
  - Using Cancellation
  - Getting Results with DebounceDispatcher\<T>
- Advanced Scenarios
- Usage Recommendations

## Overview of Classes

The library consists of two main classes:

1. **DebounceDispatcher\<T>** - The main generic class that allows you to specify the return value type.
2. **DebounceDispatcher** - A simplified version that inherits from `DebounceDispatcher<bool>` and provides convenient methods for actions without return values.

## Installation

Add the `DebounceDispatcher.cs` and `DebounceDispatcherGeneric.cs` files to your project and make sure you're using the correct namespace:

```csharp
using MasterServerToolkit.DebounceThrottle;
```

## Usage Examples

### Basic Debounce with Action

```csharp
// Create a debounce dispatcher with a 500ms interval
var debouncer = new DebounceDispatcher(500);

// Imagine this method is called frequently, for example during text input
void HandleTextChanged(string text)
{
    // Wrap the handler in Debounce, so it's called only after a 500ms pause
    debouncer.Debounce(() => {
        Console.WriteLine($"Performing search for text: {text}");
        // Here would be code that performs a search or other expensive operation
    });
}

// Simulation of rapid sequential calls
HandleTextChanged("a");
HandleTextChanged("ab");
HandleTextChanged("abc");
HandleTextChanged("abcd");

// Only the last call with text "abcd" will be executed after 500ms
```

### Asynchronous Debounce

```csharp
// Create a debounce dispatcher with a 300ms interval
var debouncer = new DebounceDispatcher(300);

// Asynchronous method that is called as the user types
async Task HandleSearchAsync(string query)
{
    // Use asynchronous debounce
    await debouncer.DebounceAsync(async () =>
    {
        Console.WriteLine($"Performing asynchronous search for query: {query}");
        
        // Simulating an asynchronous operation, such as an API request
        await Task.Delay(1000); // Simulating network operation
        
        Console.WriteLine($"Search for query '{query}' completed");
    });
}

// Use in an asynchronous method
async Task RunSearchExample()
{
    // Simulation of sequential requests
    await HandleSearchAsync("progra");
    await HandleSearchAsync("programm");
    await HandleSearchAsync("programming");
    
    // Only the search for "programming" will be executed
}
```

### Using Cancellation

```csharp
// Create a debounce dispatcher with a 1000ms interval
var debouncer = new DebounceDispatcher(1000);

// Cancellation token source
using var cts = new CancellationTokenSource();

// Method with cancellation support
async Task PerformOperationWithCancellation()
{
    try
    {
        await debouncer.DebounceAsync(async () =>
        {
            Console.WriteLine("Starting a long operation...");
            
            // Simulating long-running work
            for (int i = 0; i < 10; i++)
            {
                // Check for cancellation token in the loop
                if (cts.Token.IsCancellationRequested)
                {
                    Console.WriteLine("Operation was cancelled during execution");
                    return;
                }
                
                await Task.Delay(500); // Simulating an operation step
                Console.WriteLine($"Step {i + 1} of 10");
            }
            
            Console.WriteLine("Operation completed successfully");
        }, cts.Token);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Operation was cancelled before execution started");
    }
}

// Example of using with cancellation
async Task CancellationExample()
{
    // Start the operation
    var task = PerformOperationWithCancellation();
    
    // After 1500ms cancel the operation
    await Task.Delay(1500);
    cts.Cancel();
    
    // Wait for the task to complete
    await task;
}
```

### Getting Results with DebounceDispatcher\<T>

```csharp
// Create a generic dispatcher to get a string result
var resultDebouncer = new DebounceDispatcher<string>(800);

// Function that will return a result
async Task<string> FetchDataWithResultAsync(string query)
{
    return await resultDebouncer.DebounceAsync(async () =>
    {
        Console.WriteLine($"Fetching data for: {query}");
        
        // Simulating an API request
        await Task.Delay(1000);
        
        return $"Results for query '{query}': found 42 matches";
    });
}

// Usage
async Task ResultExample()
{
    // Call sequentially, but only the last one will execute
    Task<string> task1 = FetchDataWithResultAsync("C#");
    Task<string> task2 = FetchDataWithResultAsync("C# debounce");
    Task<string> task3 = FetchDataWithResultAsync("C# debounce pattern");
    
    // Each of these variables will get the result of the last call
    string result1 = await task1;
    string result2 = await task2;
    string result3 = await task3;
    
    // All three variables will contain the same result:
    // "Results for query 'C# debounce pattern': found 42 matches"
    Console.WriteLine($"result1: {result1}");
    Console.WriteLine($"result2: {result2}");
    Console.WriteLine($"result3: {result3}");
}
```

## Advanced Scenarios

### Combination with Throttle

If you need a combination of debounce and throttle patterns, you can implement the following approach:

```csharp
public class DebouncedSearchService
{
    private readonly DebounceDispatcher _debouncer;
    private readonly SemaphoreSlim _throttleSemaphore;
    private readonly int _throttleInterval;
    private DateTime _lastExecutionTime = DateTime.MinValue;

    public DebouncedSearchService(int debounceInterval, int throttleInterval, int maxConcurrent = 1)
    {
        _debouncer = new DebounceDispatcher(debounceInterval);
        _throttleSemaphore = new SemaphoreSlim(maxConcurrent);
        _throttleInterval = throttleInterval;
    }

    public async Task SearchAsync(string query)
    {
        await _debouncer.DebounceAsync(async () =>
        {
            // Apply throttle through semaphore
            await _throttleSemaphore.WaitAsync();
            try
            {
                // Check if enough time has passed since the last execution
                var elapsed = (DateTime.UtcNow - _lastExecutionTime).TotalMilliseconds;
                if (elapsed < _throttleInterval)
                {
                    // Wait for the remaining time until throttle interval
                    await Task.Delay((int)(_throttleInterval - elapsed));
                }

                // Perform the operation
                Console.WriteLine($"Performing search for query: {query}");
                await Task.Delay(1000); // Simulating operation
                
                // Update last execution time
                _lastExecutionTime = DateTime.UtcNow;
            }
            finally
            {
                _throttleSemaphore.Release();
            }
        });
    }
}
```

### Binding to UI Events (WPF)

```csharp
public class SearchViewModel : INotifyPropertyChanged
{
    private readonly DebounceDispatcher _debouncer;
    private string _searchText;

    public event PropertyChangedEventHandler PropertyChanged;

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SearchText)));
            
            // Debounce search when text changes
            _debouncer.Debounce(PerformSearch);
        }
    }

    public ObservableCollection<string> SearchResults { get; } = new ObservableCollection<string>();

    public SearchViewModel()
    {
        _debouncer = new DebounceDispatcher(300);
    }

    private void PerformSearch()
    {
        // Executes in the UI thread after debounce
        SearchResults.Clear();
        
        if (string.IsNullOrWhiteSpace(_searchText))
            return;

        // In a real application, this would be a database or API query
        for (int i = 0; i < 5; i++)
        {
            SearchResults.Add($"Result {i + 1} for '{_searchText}'");
        }
    }
}
```

## Usage Recommendations

1. **Choose the optimal debounce interval**:
   - For UI events (text input): 200-500 ms
   - For API requests: 500-1000 ms
   - For heavy operations: 1000+ ms

2. **Always handle exceptions**:
   ```csharp
   try
   {
       await debouncer.DebounceAsync(async () => { /* code */ });
   }
   catch (OperationCanceledException)
   {
       // Handle cancellation
   }
   catch (Exception ex)
   {
       // Handle other exceptions
   }
   ```

3. **Don't forget to call Dispose**:
   ```csharp
   public class MyService : IDisposable
   {
       private readonly DebounceDispatcher _debouncer = new DebounceDispatcher(500);
       
       // Using _debouncer...
       
       public void Dispose()
       {
           _debouncer.Dispose();
       }
   }
   ```

4. **Use cancellation to avoid resource leaks**:
   ```csharp
   private CancellationTokenSource _searchCts;
   
   private void OnSearchTextChanged(string text)
   {
       // Cancel previous search
       _searchCts?.Cancel();
       _searchCts = new CancellationTokenSource();
       
       _debouncer.Debounce(() => PerformSearch(text), _searchCts.Token);
   }
   ```

5. **Use the generic version to get results**:
   Instead of:
   ```csharp
   var result = "";
   debouncer.Debounce(() => { result = GetResult(); });
   ```
   
   Better:
   ```csharp
   var resultDebouncer = new DebounceDispatcher<string>(500);
   var result = await resultDebouncer.DebounceAsync(() => GetResultAsync());
   ```

This documentation covers the basic usage of `DebounceDispatcher` and `DebounceDispatcher<T>` and should help you effectively apply these classes in your project.