# ThrottleDispatcher Documentation

## Introduction

`ThrottleDispatcher` is a class that implements the "throttle" pattern for C#, which limits the rate at which a function can be executed. Unlike debouncing (which delays execution until a period of inactivity), throttling ensures an action executes at most once within a specified time interval, regardless of how many times it's called. This is useful for rate-limiting operations like API calls, event handlers, or resource-intensive calculations.

## Contents

- Overview of Classes
- Installation
- Usage Examples
  - Basic Throttle with Action
  - Asynchronous Throttle
  - Using Cancellation
  - Getting Results with ThrottleDispatcher\<T>
- Advanced Scenarios
- Usage Recommendations

## Overview of Classes

The library consists of two main classes:

1. **ThrottleDispatcher\<T>** - The main generic class that allows you to specify the return value type.
2. **ThrottleDispatcher** - A simplified version that inherits from `ThrottleDispatcher<bool>` and provides convenient methods for actions without return values.

## Installation

Add the `ThrottleDispatcher.cs` and `ThrottleDispatcherGeneric.cs` files to your project and make sure you're using the correct namespace:

```csharp
using MasterServerToolkit.DebounceThrottle;
```

## Usage Examples

### Basic Throttle with Action

```csharp
// Create a throttle dispatcher with a 1000ms interval
var throttler = new ThrottleDispatcher(1000);

// Simulate a method called frequently, e.g., on button click
void HandleButtonClick(string buttonId)
{
    // Wrap the handler in Throttle to limit execution frequency
    throttler.Throttle(() => {
        Console.WriteLine($"Button {buttonId} clicked at {DateTime.Now.ToString("HH:mm:ss.fff")}");
        // Here would be code that performs an API call or other expensive operation
    });
}

// Simulation of rapid sequential clicks
for (int i = 0; i < 10; i++)
{
    HandleButtonClick("Submit");
    Thread.Sleep(200); // Simulate clicks every 200ms
}

// Only approximately 2-3 clicks will be processed within 2 seconds
// (First click, then one click after each 1000ms interval)
```

### Asynchronous Throttle

```csharp
// Create a throttle dispatcher with a 2000ms interval
// Set delayAfterExecution to true to ensure minimum 2000ms between completions
var throttler = new ThrottleDispatcher(2000, delayAfterExecution: true);

// Asynchronous method that makes API calls
async Task MakeApiRequestAsync(string requestData)
{
    // Use asynchronous throttle
    await throttler.ThrottleAsync(async () =>
    {
        Console.WriteLine($"Making API request with data: {requestData} at {DateTime.Now.ToString("HH:mm:ss.fff")}");
        
        // Simulating an API request
        await Task.Delay(500); // Simulating network operation
        
        Console.WriteLine($"API request completed at {DateTime.Now.ToString("HH:mm:ss.fff")}");
    });
}

// Usage in an asynchronous method
async Task RunApiExample()
{
    // Send multiple requests in parallel
    var tasks = new List<Task>();
    for (int i = 0; i < 5; i++)
    {
        tasks.Add(MakeApiRequestAsync($"Request {i+1}"));
    }
    
    // Wait for all requests to complete
    await Task.WhenAll(tasks);
    
    // Due to throttling, these will be processed with 2 seconds between the completion of one
    // and the start of the next (because we set delayAfterExecution to true)
}
```

### Using Cancellation

```csharp
// Create a throttle dispatcher with a 1500ms interval
var throttler = new ThrottleDispatcher(1500);

// Cancellation token source
using var cts = new CancellationTokenSource();

// Method with cancellation support
async Task PerformOperationWithCancellation()
{
    try
    {
        await throttler.ThrottleAsync(async () =>
        {
            Console.WriteLine($"Starting operation at {DateTime.Now.ToString("HH:mm:ss.fff")}");
            
            // Simulating a long operation
            for (int i = 0; i < 5; i++)
            {
                // Check for cancellation token in the loop
                if (cts.Token.IsCancellationRequested)
                {
                    Console.WriteLine("Operation was cancelled during execution");
                    return;
                }
                
                await Task.Delay(300); // Simulating an operation step
                Console.WriteLine($"Step {i + 1} of 5");
            }
            
            Console.WriteLine($"Operation completed at {DateTime.Now.ToString("HH:mm:ss.fff")}");
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
    // Start multiple operations
    var tasks = new List<Task>();
    for (int i = 0; i < 3; i++)
    {
        tasks.Add(PerformOperationWithCancellation());
    }
    
    // After 2000ms cancel all pending operations
    await Task.Delay(2000);
    cts.Cancel();
    
    try
    {
        // Wait for all tasks to complete or be cancelled
        await Task.WhenAll(tasks);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Some operations were cancelled");
    }
}
```

### Getting Results with ThrottleDispatcher\<T>

```csharp
// Create a generic throttle dispatcher to get numeric results
// Also enable resetIntervalOnException to reset timing when errors occur
var resultThrottler = new ThrottleDispatcher<int>(3000, resetIntervalOnException: true);

// Function that will return a result
async Task<int> FetchDataWithResultAsync(string query)
{
    return await resultThrottler.ThrottleAsync(async () =>
    {
        Console.WriteLine($"Fetching data for: {query} at {DateTime.Now.ToString("HH:mm:ss.fff")}");
        
        // Simulating an API request
        await Task.Delay(1000);
        
        // Simulate occasional errors
        if (query.Contains("error"))
        {
            throw new Exception("Error in API request");
        }
        
        return query.Length * 10; // Return some calculated result
    });
}

// Usage
async Task ResultExample()
{
    try
    {
        // These will all run at the throttled rate of one per 3 seconds
        int result1 = await FetchDataWithResultAsync("data1");
        Console.WriteLine($"Result 1: {result1}");
        
        int result2 = await FetchDataWithResultAsync("data2");
        Console.WriteLine($"Result 2: {result2}");
        
        // This will throw an exception and reset the interval counter
        int result3 = await FetchDataWithResultAsync("error_data");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Caught exception: {ex.Message}");
        
        // We can immediately try again because resetIntervalOnException is true
        int result4 = await FetchDataWithResultAsync("recovery_data");
        Console.WriteLine($"Result after error: {result4}");
    }
}
```

## Advanced Scenarios

### Implementing a Rate-Limited API Client

```csharp
public class RateLimitedApiClient
{
    private readonly ThrottleDispatcher<string> _apiThrottler;
    private readonly HttpClient _httpClient;
    
    public RateLimitedApiClient(int rateLimit)
    {
        // Create a throttle with delayAfterExecution to respect API rate limits
        _apiThrottler = new ThrottleDispatcher<string>(
            interval: 1000 * 60 / rateLimit, // Convert requests-per-minute to ms
            delayAfterExecution: true,
            resetIntervalOnException: false);
            
        _httpClient = new HttpClient();
    }
    
    public async Task<string> GetAsync(string endpoint)
    {
        return await _apiThrottler.ThrottleAsync(async () =>
        {
            Console.WriteLine($"Making API request to {endpoint}");
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        });
    }
    
    public async Task<string> PostAsync(string endpoint, string content)
    {
        return await _apiThrottler.ThrottleAsync(async () =>
        {
            Console.WriteLine($"Making POST request to {endpoint}");
            var httpContent = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, httpContent);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        });
    }
    
    public void Dispose()
    {
        _apiThrottler.Dispose();
        _httpClient.Dispose();
    }
}
```

### Window Event Throttling in WPF

```csharp
public class ResizeThrottler
{
    private readonly ThrottleDispatcher _throttler;
    private readonly Window _window;
    
    public ResizeThrottler(Window window, int throttleRate = 300)
    {
        _window = window;
        _throttler = new ThrottleDispatcher(throttleRate);
        
        // Subscribe to window size changed events
        _window.SizeChanged += Window_SizeChanged;
    }
    
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Throttle the resize handler to prevent excessive UI updates
        _throttler.Throttle(() =>
        {
            // This will execute at most once per throttleRate ms
            Console.WriteLine($"Window resized to {_window.Width}x{_window.Height}");
            
            // Here you would update layout, recalculate positions, etc.
            UpdateLayout(_window.Width, _window.Height);
        });
    }
    
    private void UpdateLayout(double width, double height)
    {
        // Complex layout calculations that would be expensive to run on every resize event
        Console.WriteLine($"Performing layout update for {width}x{height}");
    }
    
    public void Cleanup()
    {
        _window.SizeChanged -= Window_SizeChanged;
        _throttler.Dispose();
    }
}
```

## Usage Recommendations

1. **Choose the right interval based on your use case**:
   - For UI responsiveness: 100-300 ms
   - For standard API rate limits: Calculate based on limits (e.g., 60 requests/minute = 1000ms)
   - For heavy computational tasks: 500+ ms

2. **Understand delayAfterExecution parameter**:
   - `false` (default): Interval starts when the function is called
   - `true`: Interval starts when the function completes execution
   - Use `true` for strict rate-limiting of resource-intensive operations

3. **Understand resetIntervalOnException parameter**:
   - `false` (default): Keep throttling even if operations fail
   - `true`: Reset interval on exceptions, allowing immediate retry
   - Use `true` when you want to retry failed operations immediately

4. **Handle exceptions properly**:
   ```csharp
   try
   {
       await throttler.ThrottleAsync(async () => { /* code */ });
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

5. **Properly dispose throttlers to prevent memory leaks**:
   ```csharp
   public class MyService : IDisposable
   {
       private readonly ThrottleDispatcher _throttler = new ThrottleDispatcher(500);
       
       // Using _throttler...
       
       public void Dispose()
       {
           _throttler.Dispose();
       }
   }
   ```

6. **Understand the difference between Throttling and Debouncing**:
   - **Throttling**: Limits execution to once per time interval (rate limiting)
   - **Debouncing**: Delays execution until a period of inactivity (coalescing events)
   
   Choose the right technique for your use case:
   - Use throttling for: API rate limits, scroll handlers, resource-intensive operations
   - Use debouncing for: Search inputs, resize events, button clicks that should ignore double-clicks

7. **Consider shared throttlers for system-wide rate limits**:
   ```csharp
   // Singleton pattern for a shared API throttler
   public class ApiThrottler
   {
       private static readonly ThrottleDispatcher<string> _instance = new ThrottleDispatcher<string>(1000, true);
       
       public static ThrottleDispatcher<string> Instance => _instance;
       
       // Private constructor to prevent instantiation
       private ApiThrottler() { }
   }
   ```

This documentation covers the basic usage of `ThrottleDispatcher` and `ThrottleDispatcher<T>` and should help you effectively apply these classes in your project.