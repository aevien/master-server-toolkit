using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Base interface for updatable objects
    /// </summary>
    public interface IUpdatable
    {
        /// <summary>
        /// Method called when the object needs to be updated
        /// </summary>
        void DoUpdate();
    }

    /// <summary>
    /// Extended interface with support for intervals and priorities
    /// </summary>
    public interface IIntervalUpdatable : IUpdatable
    {
        /// <summary>
        /// Update interval in seconds (0 = every frame)
        /// </summary>
        float UpdateInterval { get; }

        /// <summary>
        /// Execution priority (lower number = higher priority)
        /// </summary>
        int Priority { get; }
    }

    /// <summary>
    /// Centralized update manager with support for intervals and priorities.
    /// This manager efficiently handles multiple updatable objects by grouping them
    /// based on their update frequency and executing them in priority order.
    /// </summary>
    public class MstUpdateRunner : SingletonBehaviour<MstUpdateRunner>
    {
        /// <summary>
        /// Internal class for storing information about an updatable object
        /// </summary>
        private class UpdateItem
        {
            public IUpdatable updatable;
            public float interval;
            public float nextUpdateTime;
            public int priority;

            public UpdateItem(IUpdatable updatable, float interval, int priority)
            {
                this.updatable = updatable;
                this.interval = interval;
                this.priority = priority;
                this.nextUpdateTime = interval > 0 ? Time.time + interval : 0;
            }
        }

        // HashSet for tracking all registered objects to ensure uniqueness
        private readonly HashSet<IUpdatable> registeredObjects = new HashSet<IUpdatable>();

        // List for objects that update every frame
        private readonly List<UpdateItem> everyFrameItems = new List<UpdateItem>();

        // List for objects with interval-based updates
        private readonly List<UpdateItem> intervalItems = new List<UpdateItem>();

        // Temporary lists for safe addition/removal during iteration
        private readonly List<UpdateItem> itemsToAdd = new List<UpdateItem>();
        private readonly List<IUpdatable> itemsToRemove = new List<IUpdatable>();

        // Flag indicating we are in the process of updating
        private bool isUpdating = false;

        // Flag to track when sorting is needed
        private bool needsSorting = false;

        /// <summary>
        /// Total count of updatable objects managed by this runner
        /// </summary>
        public int Count => registeredObjects.Count;

        /// <summary>
        /// Count of objects that update every frame
        /// </summary>
        public int EveryFrameCount => everyFrameItems.Count;

        /// <summary>
        /// Count of objects with interval-based updates
        /// </summary>
        public int IntervalCount => intervalItems.Count;

        /// <summary>
        /// Unity Update method - processes all registered updatable objects
        /// </summary>
        private void Update()
        {
            // Set flag that we've started updating
            isUpdating = true;

            try
            {
                // Update objects that run every frame
                UpdateEveryFrameItems();

                // Update objects with intervals
                UpdateIntervalItems();
            }
            finally
            {
                // Always reset the flag, even if an error occurred
                isUpdating = false;

                // Process pending operations (additions/removals)
                ProcessPendingOperations();
            }
        }

        /// <summary>
        /// Updates objects that should be updated every frame
        /// </summary>
        private void UpdateEveryFrameItems()
        {
            // Sort by priority if needed
            if (needsSorting && everyFrameItems.Count > 1)
            {
                everyFrameItems.Sort((a, b) => a.priority.CompareTo(b.priority));
            }

            // Iterate backwards for safe removal during iteration
            for (int i = everyFrameItems.Count - 1; i >= 0; i--)
            {
                var item = everyFrameItems[i];

                // Check if object was marked for removal
                if (item.updatable == null || itemsToRemove.Contains(item.updatable))
                {
                    everyFrameItems.RemoveAt(i);
                    continue;
                }

                try
                {
                    item.updatable.DoUpdate();
                }
                catch (Exception e)
                {
                    Logs.Error($"Error updating {item.updatable.GetType().Name}: {e}");
                    everyFrameItems.RemoveAt(i);
                    // Also remove from registered objects when an error occurs
                    registeredObjects.Remove(item.updatable);
                }
            }
        }

        /// <summary>
        /// Updates objects with specified intervals
        /// </summary>
        private void UpdateIntervalItems()
        {
            float currentTime = Time.time;

            // Sort by priority if needed
            if (needsSorting && intervalItems.Count > 1)
            {
                intervalItems.Sort((a, b) => a.priority.CompareTo(b.priority));
                needsSorting = false;
            }

            // Iterate backwards for safe removal
            for (int i = intervalItems.Count - 1; i >= 0; i--)
            {
                var item = intervalItems[i];

                // Check if object was marked for removal
                if (item.updatable == null || itemsToRemove.Contains(item.updatable))
                {
                    intervalItems.RemoveAt(i);
                    continue;
                }

                // Check if it's time to update this object
                if (currentTime >= item.nextUpdateTime)
                {
                    try
                    {
                        item.updatable.DoUpdate();
                        // Schedule next update
                        item.nextUpdateTime = currentTime + item.interval;
                    }
                    catch (Exception e)
                    {
                        Logs.Error($"Error updating {item.updatable.GetType().Name}: {e}");
                        intervalItems.RemoveAt(i);
                        // Also remove from registered objects when an error occurs
                        registeredObjects.Remove(item.updatable);
                    }
                }
            }
        }

        /// <summary>
        /// Processes pending add and remove operations
        /// </summary>
        private void ProcessPendingOperations()
        {
            // First remove marked objects
            if (itemsToRemove.Count > 0)
            {
                foreach (var updatable in itemsToRemove)
                {
                    RemoveFromLists(updatable);
                    registeredObjects.Remove(updatable);
                }
                itemsToRemove.Clear();
            }

            // Then add new objects
            if (itemsToAdd.Count > 0)
            {
                foreach (var item in itemsToAdd)
                {
                    // Double-check that the object wasn't already added
                    if (!registeredObjects.Contains(item.updatable))
                    {
                        registeredObjects.Add(item.updatable);

                        if (item.interval <= 0)
                        {
                            everyFrameItems.Add(item);
                        }
                        else
                        {
                            intervalItems.Add(item);
                        }
                        needsSorting = true;
                    }
                }
                itemsToAdd.Clear();
            }
        }

        /// <summary>
        /// Removes an object from all internal lists
        /// </summary>
        /// <param name="updatable">The object to remove</param>
        private void RemoveFromLists(IUpdatable updatable)
        {
            // Remove from every frame objects list
            everyFrameItems.RemoveAll(item => item.updatable == updatable);

            // Remove from interval objects list
            intervalItems.RemoveAll(item => item.updatable == updatable);
        }

        /// <summary>
        /// Adds an updatable object to the manager
        /// </summary>
        /// <param name="updatable">The object to add</param>
        public static void Add(IUpdatable updatable)
        {
            if (updatable == null)
            {
                Logs.Warn("Attempted to add null object to MstUpdateRunner");
                return;
            }

            if (TryGetOrCreate(out var instance))
            {
                instance.AddInternal(updatable);
            }
        }

        /// <summary>
        /// Internal method for adding an object
        /// </summary>
        /// <param name="updatable">The object to add</param>
        private void AddInternal(IUpdatable updatable)
        {
            // Check if the object is already registered
            if (registeredObjects.Contains(updatable))
            {
                Logs.Warn($"Object {updatable.GetType().Name} is already registered in MstUpdateRunner");
                return;
            }

            // Check if the object is in pending operations
            if (isUpdating)
            {
                // Check if already in pending additions
                foreach (var item in itemsToAdd)
                {
                    if (item.updatable == updatable)
                    {
                        Logs.Warn($"Object {updatable.GetType().Name} is already in pending additions");
                        return;
                    }
                }

                // Check if in pending removals - if so, remove it from there
                if (itemsToRemove.Contains(updatable))
                {
                    itemsToRemove.Remove(updatable);
                }
            }

            // Determine object parameters
            float interval = 0f;
            int priority = 100;

            // If object supports intervals, get them
            if (updatable is IIntervalUpdatable intervalUpdatable)
            {
                interval = intervalUpdatable.UpdateInterval;
                priority = intervalUpdatable.Priority;
            }

            var newItem = new UpdateItem(updatable, interval, priority);

            // If we're in the process of updating, delay the addition
            if (isUpdating)
            {
                itemsToAdd.Add(newItem);
            }
            else
            {
                // Add to HashSet immediately
                registeredObjects.Add(updatable);

                // Add to the appropriate list
                if (interval <= 0)
                {
                    everyFrameItems.Add(newItem);
                }
                else
                {
                    intervalItems.Add(newItem);
                }

                needsSorting = true;
            }
        }

        /// <summary>
        /// Removes an updatable object from the manager
        /// </summary>
        /// <param name="updatable">The object to remove</param>
        public static void Remove(IUpdatable updatable)
        {
            if (updatable == null)
            {
                return;
            }

            if (TryGetOrCreate(out var instance))
            {
                instance.RemoveInternal(updatable);
            }
        }

        /// <summary>
        /// Internal method for removing an object
        /// </summary>
        /// <param name="updatable">The object to remove</param>
        private void RemoveInternal(IUpdatable updatable)
        {
            // Check if the object is actually registered
            if (!registeredObjects.Contains(updatable))
            {
                return;  // Object is not registered, nothing to remove
            }

            // If we're in the process of updating, delay the removal
            if (isUpdating)
            {
                // Make sure it's not already in the removal list
                if (!itemsToRemove.Contains(updatable))
                {
                    itemsToRemove.Add(updatable);
                }

                // Remove from pending additions if present
                itemsToAdd.RemoveAll(item => item.updatable == updatable);
            }
            else
            {
                RemoveFromLists(updatable);
                registeredObjects.Remove(updatable);
            }
        }

        /// <summary>
        /// Checks if an object is contained in the manager
        /// </summary>
        /// <param name="updatable">The object to check</param>
        /// <returns>True if the object is managed by this runner</returns>
        public static bool Contains(IUpdatable updatable)
        {
            if (updatable == null || !TryGetOrCreate(out var instance))
            {
                return false;
            }

            return instance.ContainsInternal(updatable);
        }

        /// <summary>
        /// Internal method for checking if an object is contained
        /// </summary>
        /// <param name="updatable">The object to check</param>
        /// <returns>True if the object is contained in any list</returns>
        private bool ContainsInternal(IUpdatable updatable)
        {
            // Now uses O(1) HashSet lookup instead of O(n) list iteration
            return registeredObjects.Contains(updatable);
        }

        /// <summary>
        /// Updates the parameters (interval and priority) of an already registered updatable object.
        /// This method is useful when an object's update frequency or priority needs to be changed
        /// during runtime without removing and re-adding the object.
        /// </summary>
        /// <param name="updatable">The updatable object whose parameters need to be updated</param>
        /// <remarks>
        /// This method handles the following scenarios:
        /// - Moving objects between everyFrame and interval lists based on new interval values
        /// - Updating priority which may require re-sorting
        /// - Maintaining the object's registration in the HashSet while updating its parameters
        /// If the object is not registered, the method returns without doing anything.
        /// </remarks>
        private void UpdateParametersInternal(IUpdatable updatable)
        {
            // First check if the object is actually registered in our system
            // Using HashSet provides O(1) lookup performance
            if (!registeredObjects.Contains(updatable))
            {
                // Object is not registered, nothing to update
                // This is not an error condition - just silently return
                return;
            }

            // Remove the object from current lists but keep it in registeredObjects
            // This preserves the registration while allowing us to re-add with new parameters
            RemoveFromLists(updatable);

            // Get the updated parameters from the object
            // Default values match those used in AddInternal
            float interval = 0f;      // Default: update every frame
            int priority = 100;       // Default: medium priority

            // Check if the object implements the extended interface with interval support
            if (updatable is IIntervalUpdatable intervalUpdatable)
            {
                // Retrieve the current interval and priority from the object
                // These values may have changed since the object was first added
                interval = intervalUpdatable.UpdateInterval;
                priority = intervalUpdatable.Priority;
            }

            // Create a new UpdateItem with the fresh parameters
            // This ensures we're using the most current values
            var newItem = new UpdateItem(updatable, interval, priority);

            // Determine which list the object should be added to based on its interval
            if (interval <= 0)
            {
                // Interval of 0 or less means update every frame
                everyFrameItems.Add(newItem);
            }
            else
            {
                // Positive interval means periodic updates
                intervalItems.Add(newItem);
            }

            // Mark that lists need re-sorting since we added a new item
            // The actual sorting will happen during the next Update cycle
            needsSorting = true;
        }

        /// <summary>
        /// Public method to update parameters of a registered updatable object.
        /// This is the entry point for external code to request parameter updates.
        /// </summary>
        /// <param name="updatable">The updatable object whose parameters need to be updated</param>
        /// <example>
        /// <code>
        /// // Example usage:
        /// var enemy = new Enemy();
        /// MstUpdateRunner.Add(enemy);
        /// 
        /// // Later, when difficulty increases:
        /// enemy.UpdateInterval = 0.5f;  // Update twice as fast
        /// MstUpdateRunner.UpdateParameters(enemy);
        /// </code>
        /// </example>
        public static void UpdateParameters(IUpdatable updatable)
        {
            // Validate input
            if (updatable == null)
            {
                Logs.Warn("Attempted to update parameters for null object in MstUpdateRunner");
                return;
            }

            // Get or create the singleton instance and delegate to internal method
            if (TryGetOrCreate(out var instance))
            {
                instance.UpdateParametersInternal(updatable);
            }
        }

        /// <summary>
        /// Gets performance statistics for debugging and monitoring
        /// </summary>
        /// <returns>A formatted string containing current statistics</returns>
        public static string GetPerformanceStats()
        {
            if (!TryGetOrCreate(out var instance))
            {
                return "MstUpdateRunner not initialized";
            }

            return $"Total objects: {instance.Count}\n" +
                   $"Every frame: {instance.EveryFrameCount}\n" +
                   $"With intervals: {instance.IntervalCount}\n" +
                   $"Pending additions: {instance.itemsToAdd.Count}\n" +
                   $"Pending removals: {instance.itemsToRemove.Count}";
        }
    }
}