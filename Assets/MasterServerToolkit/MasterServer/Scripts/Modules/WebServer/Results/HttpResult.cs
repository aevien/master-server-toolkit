using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents an HTTP response that can be returned from the web server.
    /// This class handles formatting, compression, CORS configuration, and caching
    /// of HTTP responses in a flexible and chainable way.
    /// </summary>
    public class HttpResult : IHttpResult
    {
        /// <summary>
        /// The string content to be sent in the response body.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// The MIME type of the content (e.g., "text/plain", "application/json").
        /// This will be set in the Content-Type header of the response.
        /// </summary>
        public string ContentType { get; set; } = "text/plain";

        /// <summary>
        /// The HTTP status code to return (default is 200 OK).
        /// </summary>
        public int StatusCode { get; set; } = (int)HttpStatusCode.OK;

        /// <summary>
        /// Indicates whether response compression is enabled.
        /// When true, the response will be compressed using gzip or deflate
        /// if the client supports it.
        /// </summary>
        public bool CompressionEnabled { get; private set; } = false;

        /// <summary>
        /// Collection of HTTP headers to be included in the response.
        /// </summary>
        public WebHeaderCollection Headers { get; set; } = new WebHeaderCollection();

        /// <summary>
        /// Initializes a new instance of the HttpResult class with text content.
        /// </summary>
        /// <param name="value">The string content to return in the response</param>
        /// <param name="contentType">The MIME type of the content (defaults to "text/plain")</param>
        public HttpResult(string value = "", string contentType = "text/plain")
        {
            Value = value;
            ContentType = contentType;
        }

        /// <summary>
        /// Initializes a new instance of the HttpResult class with JSON content.
        /// </summary>
        /// <param name="value">The JSON object to serialize and return</param>
        /// <param name="contentType">The MIME type (defaults to "application/json")</param>
        public HttpResult(MstJson value, string contentType = "application/json") : this(value.ToString(), contentType) { }

        /// <summary>
        /// Initializes a new instance of the HttpResult class with an object
        /// that will be converted to string using ToString().
        /// </summary>
        /// <param name="value">The object to convert to string and return</param>
        /// <param name="contentType">The MIME type (defaults to "text/plain")</param>
        public HttpResult(object value, string contentType = "text/plain") : this(value.ToString(), contentType) { }

        /// <summary>
        /// Configures Cross-Origin Resource Sharing (CORS) headers for the response.
        /// This method allows you to control which origins, methods, and headers are 
        /// permitted for cross-origin requests.
        /// </summary>
        /// <param name="allowedOrigins">Origins allowed to access the resource (* for all)</param>
        /// <param name="allowedMethods">HTTP methods allowed (e.g., "GET, POST, PUT")</param>
        /// <param name="allowedHeaders">Headers allowed in the request</param>
        /// <param name="allowCredentials">Whether credentials (cookies, auth) are allowed</param>
        /// <returns>The current HttpResult instance for method chaining</returns>
        public HttpResult ConfigureCors(string allowedOrigins = "*",
                               string allowedMethods = null,
                               string allowedHeaders = null,
                               bool allowCredentials = false)
        {
            Headers["Access-Control-Allow-Origin"] = allowedOrigins;

            if (!string.IsNullOrEmpty(allowedMethods))
                Headers["Access-Control-Allow-Methods"] = allowedMethods;

            if (!string.IsNullOrEmpty(allowedHeaders))
                Headers["Access-Control-Allow-Headers"] = allowedHeaders;

            if (allowCredentials)
                Headers["Access-Control-Allow-Credentials"] = "true";

            return this;
        }

        /// <summary>
        /// Enables or disables response compression.
        /// When enabled, the response will be compressed using gzip or deflate
        /// if the client indicates support via the Accept-Encoding header.
        /// </summary>
        /// <param name="enable">Whether compression should be enabled</param>
        /// <returns>The current HttpResult instance for method chaining</returns>
        public HttpResult EnableCompression(bool enable = true)
        {
            CompressionEnabled = enable;
            return this;
        }

        /// <summary>
        /// Configures browser and proxy caching behavior for the response.
        /// This sets appropriate Cache-Control headers to control how long
        /// the response can be cached.
        /// </summary>
        /// <param name="maxAgeSeconds">Maximum time in seconds the response can be cached (0 for no caching)</param>
        /// <param name="isPublic">Whether response can be stored in shared/public caches</param>
        /// <returns>The current HttpResult instance for method chaining</returns>
        public HttpResult ConfigureCache(int maxAgeSeconds = 0, bool isPublic = true)
        {
            if (maxAgeSeconds > 0)
            {
                string cacheControl = isPublic ? "public" : "private";
                cacheControl += $", max-age={maxAgeSeconds}";
                Headers["Cache-Control"] = cacheControl;
            }
            else
            {
                Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
                Headers["Pragma"] = "no-cache";
            }

            return this;
        }

        /// <summary>
        /// Executes the HTTP response, writing the configured content, headers,
        /// and status code to the provided HttpListenerContext.
        /// This method handles compression if enabled and supported by the client.
        /// </summary>
        /// <param name="context">The HTTP context containing the request and response</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public virtual async Task Execute(HttpListenerContext context)
        {
            var response = context.Response;
            try
            {
                // Convert the string content to UTF-8 byte array
                byte[] buffer = Encoding.UTF8.GetBytes(Value);

                // Apply compression if enabled and supported by the client
                if (CompressionEnabled)
                {
                    // Check which compression algorithms the client supports
                    string acceptEncoding = context.Request.Headers["Accept-Encoding"] ?? "";

                    // Prefer gzip compression if supported
                    if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Headers["Content-Encoding"] = "gzip";
                        buffer = CompressWithGzip(buffer);
                    }
                    // Fall back to deflate compression if gzip isn't supported
                    else if (acceptEncoding.Contains("deflate", StringComparison.OrdinalIgnoreCase))
                    {
                        response.Headers["Content-Encoding"] = "deflate";
                        buffer = CompressWithDeflate(buffer);
                    }
                    // If no compression is supported, send uncompressed data
                }

                // Set response properties using the (potentially) compressed data
                response.ContentLength64 = buffer.LongLength;
                response.ContentType = ContentType;
                response.StatusCode = StatusCode;

                // Copy all configured headers to the response
                foreach (string key in Headers.Keys)
                {
                    response.Headers[key] = Headers[key];
                }

                // Write the data to the response output stream
                using (Stream output = response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch (Exception ex)
            {
                // Log any errors and abort the response if possible
                Logs.Error(ex);
                response?.Abort();
            }
        }

        /// <summary>
        /// Compresses data using the GZip algorithm.
        /// This provides better compression ratio but slightly more CPU usage compared to Deflate.
        /// </summary>
        /// <param name="data">The byte array to compress</param>
        /// <returns>The compressed byte array</returns>
        private byte[] CompressWithGzip(byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new System.IO.Compression.GZipStream(
                    memoryStream, System.IO.Compression.CompressionMode.Compress, true))
                {
                    // Write the entire data array to the compression stream
                    gzipStream.Write(data, 0, data.Length);
                }

                // Return the compressed data as a byte array
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Compresses data using the Deflate algorithm.
        /// This is a fallback compression method with slightly less compression 
        /// but better compatibility with older clients.
        /// </summary>
        /// <param name="data">The byte array to compress</param>
        /// <returns>The compressed byte array</returns>
        private byte[] CompressWithDeflate(byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var deflateStream = new System.IO.Compression.DeflateStream(
                    memoryStream, System.IO.Compression.CompressionMode.Compress, true))
                {
                    // Write the entire data array to the compression stream
                    deflateStream.Write(data, 0, data.Length);
                }

                // Return the compressed data as a byte array
                return memoryStream.ToArray();
            }
        }
    }
}