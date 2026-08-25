using MasterServerToolkit.Logging;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        /// <summary>
        /// Execution priority (lower number = higher priority)
        /// </summary>
        int Priority { get; }
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
            public long registrationOrder;

            public UpdateItem(IUpdatable updatable, float interval, int priority, long registrationOrder)
            {
                this.updatable = updatable;
                this.interval = interval;
                this.priority = priority;
                this.registrationOrder = registrationOrder;
                this.nextUpdateTime = interval > 0 ? Time.time + interval : 0;
            }
        }

        private sealed class UpdatableReferenceComparer : IEqualityComparer<IUpdatable>
        {
            public bool Equals(IUpdatable first, IUpdatable second)
            {
                return ReferenceEquals(first, second);
            }

            public int GetHashCode(IUpdatable updatable)
            {
                return ReferenceEquals(updatable, null)
                    ? 0
                    : RuntimeHelpers.GetHashCode(updatable);
            }
        }

        private static readonly IEqualityComparer<IUpdatable> updatableReferenceComparer =
            new UpdatableReferenceComparer();

        // HashSet for tracking all registered objects to ensure uniqueness
        private readonly HashSet<IUpdatable> registeredObjects =
            new HashSet<IUpdatable>(updatableReferenceComparer);

        // List for objects that update every frame
        private readonly List<UpdateItem> everyFrameItems = new List<UpdateItem>();

        // List for objects with interval-based updates
        private readonly List<UpdateItem> intervalItems = new List<UpdateItem>();

        // Temporary lists for safe addition/removal during iteration
        private readonly List<UpdateItem> itemsToAdd = new List<UpdateItem>();
        private readonly List<IUpdatable> itemsToRemove = new List<IUpdatable>();
        private readonly List<IUpdatable> itemsToUpdate = new List<IUpdatable>();

        // Flag indicating we are in the process of updating
        private bool isUpdating = false;

        // Flag to track when sorting is needed
        private bool needsSorting = false;

        private long nextRegistrationOrder;

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
                SortItemsIfNeeded();

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
            for (int i = 0; i < everyFrameItems.Count;)
            {
                var item = everyFrameItems[i];

                // Check if object was marked for removal
                if (item.updatable == null || ContainsReference(itemsToRemove, item.updatable))
                {
                    everyFrameItems.RemoveAt(i);
                    registeredObjects.Remove(item.updatable);
                    continue;
                }

                try
                {
                    item.updatable.DoUpdate();
                    i++;
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

            for (int i = 0; i < intervalItems.Count;)
            {
                var item = intervalItems[i];

                // Check if object was marked for removal
                if (item.updatable == null || ContainsReference(itemsToRemove, item.updatable))
                {
                    intervalItems.RemoveAt(i);
                    registeredObjects.Remove(item.updatable);
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
                        continue;
                    }
                }

                i++;
            }
        }

        private void SortItemsIfNeeded()
        {
            if (!needsSorting)
                return;

            everyFrameItems.Sort(CompareUpdateItems);
            intervalItems.Sort(CompareUpdateItems);
            needsSorting = false;
        }

        private static int CompareUpdateItems(UpdateItem first, UpdateItem second)
        {
            int priorityComparison = first.priority.CompareTo(second.priority);
            return priorityComparison != 0
                ? priorityComparison
                : first.registrationOrder.CompareTo(second.registrationOrder);
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

            if (itemsToUpdate.Count > 0)
            {
                foreach (var updatable in itemsToUpdate)
                {
                    if (registeredObjects.Contains(updatable))
                        ApplyUpdatedParameters(updatable);
                }

                itemsToUpdate.Clear();
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
            everyFrameItems.RemoveAll(item => ReferenceEquals(item.updatable, updatable));

            // Remove from interval objects list
            intervalItems.RemoveAll(item => ReferenceEquals(item.updatable, updatable));
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
            if (registeredObjects.Contains(updatable))
            {
                // Re-registering during the same update cancels a deferred removal. The existing
                // update item is still present until pending operations are processed.
                if (isUpdating && RemoveReference(itemsToRemove, updatable))
                    return;

                Logs.Warn($"Object {updatable.GetType().Name} is already registered in MstUpdateRunner");
                return;
            }

            // Check if the object is in pending operations
            if (isUpdating)
            {
                // Check if already in pending additions
                foreach (var item in itemsToAdd)
                {
                    if (ReferenceEquals(item.updatable, updatable))
                    {
                        Logs.Warn($"Object {updatable.GetType().Name} is already in pending additions");
                        return;
                    }
                }

            }

            // Determine object parameters
            float interval = 0f;
            int priority = updatable.Priority;

            // If object supports intervals, get them
            if (updatable is IIntervalUpdatable intervalUpdatable)
            {
                interval = intervalUpdatable.UpdateInterval;
            }

            var newItem = new UpdateItem(updatable, interval, priority, nextRegistrationOrder++);

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

            if (TryGetExisting(out var instance))
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
                if (isUpdating)
                    itemsToAdd.RemoveAll(item => ReferenceEquals(item.updatable, updatable));

                return;
            }

            // If we're in the process of updating, delay the removal
            if (isUpdating)
            {
                // Make sure it's not already in the removal list
                if (!ContainsReference(itemsToRemove, updatable))
                {
                    itemsToRemove.Add(updatable);
                }

                // Remove from pending additions if present
                itemsToAdd.RemoveAll(item => ReferenceEquals(item.updatable, updatable));
                RemoveReference(itemsToUpdate, updatable);
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
            if (updatable == null || !TryGetExisting(out var instance))
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
            if (!registeredObjects.Contains(updatable))
                return;

            if (isUpdating)
            {
                if (!ContainsReference(itemsToUpdate, updatable))
                    itemsToUpdate.Add(updatable);

                return;
            }

            ApplyUpdatedParameters(updatable);
        }

        private void ApplyUpdatedParameters(IUpdatable updatable)
        {
            UpdateItem item = FindUpdateItem(updatable);

            if (item == null)
                return;

            RemoveFromLists(updatable);
            item.interval = updatable is IIntervalUpdatable intervalUpdatable
                ? intervalUpdatable.UpdateInterval
                : 0f;
            item.priority = updatable.Priority;
            item.nextUpdateTime = item.interval > 0 ? Time.time + item.interval : 0;

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

        private UpdateItem FindUpdateItem(IUpdatable updatable)
        {
            UpdateItem item = everyFrameItems.Find(candidate => ReferenceEquals(candidate.updatable, updatable));
            return item ?? intervalItems.Find(candidate => ReferenceEquals(candidate.updatable, updatable));
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

            // Parameter updates only apply to an existing runner and registered object.
            if (TryGetExisting(out var instance))
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
            if (!TryGetExisting(out var instance))
            {
                return "MstUpdateRunner not initialized";
            }

            return $"Total objects: {instance.Count}\n" +
                   $"Every frame: {instance.EveryFrameCount}\n" +
                   $"With intervals: {instance.IntervalCount}\n" +
                   $"Pending additions: {instance.itemsToAdd.Count}\n" +
                   $"Pending removals: {instance.itemsToRemove.Count}\n" +
                   $"Pending parameter updates: {instance.itemsToUpdate.Count}";
        }

        private static bool ContainsReference(List<IUpdatable> items, IUpdatable updatable)
        {
            return items.Exists(item => ReferenceEquals(item, updatable));
        }

        private static bool RemoveReference(List<IUpdatable> items, IUpdatable updatable)
        {
            int index = items.FindIndex(item => ReferenceEquals(item, updatable));

            if (index < 0)
                return false;

            items.RemoveAt(index);
            return true;
        }
    }
}
