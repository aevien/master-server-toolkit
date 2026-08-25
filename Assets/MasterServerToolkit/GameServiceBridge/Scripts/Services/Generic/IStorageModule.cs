using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Represents a callback that receives player storage data.
    /// </summary>
    /// <param name="data">The player data in JSON format.</param>
    public delegate void StorageDataHandler(MstJson data);

    /// <summary>
    /// Defines the contract for player data storage exposed by the current game service.
    /// </summary>
    public interface IStorageModule : IServiceModule
    {
        /// <summary>
        /// Occurs when player data is loaded.
        /// </summary>
        event StorageDataHandler OnLoadEvent;

        /// <summary>
        /// Occurs when player data is saved.
        /// </summary>
        event SuccessCallback OnSaveEvent;

        /// <summary>
        /// Gets the currently loaded player data.
        /// </summary>
        MstJson Data { get; }

        /// <summary>
        /// Stores a string value in persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="value">The string value to store.</param>
        void SetString(string key, string value);

        /// <summary>
        /// Stores a floating-point value in persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="value">The floating-point value to store.</param>
        void SetFloat(string key, float value);

        /// <summary>
        /// Stores an integer value in persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="value">The integer value to store.</param>
        void SetInt(string key, int value);

        /// <summary>
        /// Stores a Boolean value in persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="value">The Boolean value to store.</param>
        void SetBool(string key, bool value);

        /// <summary>
        /// Saves multiple player data entries in a single operation.
        /// </summary>
        /// <param name="data">The JSON object that contains key-value pairs to store.</param>
        /// <param name="saveAsStats">
        /// <see langword="true"/> to save the data as platform statistics when supported; otherwise, <see langword="false"/>.
        /// </param>
        void SaveData(MstJson data, bool saveAsStats = false);

        /// <summary>
        /// Retrieves a string value from persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="defaultValue">The value to return when the key is not found.</param>
        /// <returns>The stored string value, or <paramref name="defaultValue"/> when the key is not found.</returns>
        string GetString(string key, string defaultValue = "");

        /// <summary>
        /// Retrieves a floating-point value from persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="defaultValue">The value to return when the key is not found.</param>
        /// <returns>The stored floating-point value, or <paramref name="defaultValue"/> when the key is not found.</returns>
        float GetFloat(string key, float defaultValue = 0f);

        /// <summary>
        /// Retrieves an integer value from persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="defaultValue">The value to return when the key is not found.</param>
        /// <returns>The stored integer value, or <paramref name="defaultValue"/> when the key is not found.</returns>
        int GetInt(string key, int defaultValue = 0);

        /// <summary>
        /// Retrieves a Boolean value from persistent storage.
        /// </summary>
        /// <param name="key">The key that identifies the stored value.</param>
        /// <param name="defaultValue">The value to return when the key is not found.</param>
        /// <returns>The stored Boolean value, or <paramref name="defaultValue"/> when the key is not found.</returns>
        bool GetBool(string key, bool defaultValue = false);

        /// <summary>
        /// Loads all stored player data.
        /// </summary>
        /// <param name="callback">The callback that receives the loaded player data.</param>
        void LoadData(StorageDataHandler callback);
    }
}
