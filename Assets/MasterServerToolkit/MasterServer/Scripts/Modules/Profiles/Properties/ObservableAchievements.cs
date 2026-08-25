using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableAchievements : ObservableBaseList<AchievementProgressInfo>
    {
        public ObservableAchievements(ushort key) : base(key) { }

        public override void Deserialize(string value)
        {
            FromJson(value);
        }

        public override void FromJson(MstJson json)
        {
            _value.Clear();

            foreach (var item in json)
            {
                var achievement = new AchievementProgressInfo();
                achievement.FromJson(item);
                _value.Add(achievement);
            }

            MarkAsDirty();
        }

        public override void FromJson(string json)
        {
            FromJson(new MstJson(json));
        }

        public override string Serialize()
        {
            return ToJson().ToString();
        }

        public override MstJson ToJson()
        {
            var json = MstJson.CreateArray();

            foreach (var item in _value)
            {
                json.Add(item.ToJson());
            }

            return json;
        }

        protected override AchievementProgressInfo ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadPacket<AchievementProgressInfo>();
        }

        protected override void WriteValue(AchievementProgressInfo value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Has(string key)
        {
            return _value.Find(v => v.key == key) != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public AchievementProgressInfo Get(string key)
        {
            return _value.FirstOrDefault(v => v.key == key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool IsUnlocked(string key)
        {
            var achievemet = _value.FirstOrDefault(v => v.key == key);
            return achievemet != null && achievemet.IsUnlocked;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        public void Reset(string key)
        {
            int index = Value.FindIndex(v => v.key == key);

            if (index < 0)
            {
                return;
            }

            var item = this[index];

            if (item.IsUnlocked)
            {
                return;
            }

            var updatedItem = new AchievementProgressInfo(item)
            {
                progress = 0
            };

            this[index] = updatedItem;
        }

        /// <summary>
        /// Tries to unlock achievement with new progress
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public bool TryToUnlock(string key, int progress)
        {
            int index = Value.FindIndex(v => v.key == key);

            if (index < 0)
            {
                return false;
            }

            var item = this[index];

            if (!item.IsUnlocked)
            {
                var updatedItem = new AchievementProgressInfo(item)
                {
                    progress = item.progress + progress
                };

                if (updatedItem.progress >= updatedItem.required)
                    updatedItem.unlockedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                this[index] = updatedItem;
                return updatedItem.IsUnlocked;
            }

            return false;
        }

        /// <summary>
        /// Applies an absolute progress value if it advances the achievement.
        /// </summary>
        /// <param name="key">Achievement key.</param>
        /// <param name="progress">Absolute progress reported by an authoritative source.</param>
        /// <returns><c>true</c> only when this update unlocks the achievement.</returns>
        public bool TrySetProgress(string key, int progress)
        {
            int index = Value.FindIndex(v => v.key == key);

            if (index < 0)
                return false;

            var item = this[index];

            if (item.IsUnlocked || progress <= item.progress)
                return false;

            var updatedItem = new AchievementProgressInfo(item)
            {
                progress = progress
            };

            if (updatedItem.progress >= updatedItem.required)
                updatedItem.unlockedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            this[index] = updatedItem;
            return updatedItem.IsUnlocked;
        }

        /// <summary>
        /// Marks the reward for an unlocked achievement as successfully applied.
        /// </summary>
        /// <param name="key">Achievement key.</param>
        /// <returns><c>true</c> when the pending reward was marked as applied.</returns>
        public bool MarkRewardApplied(string key)
        {
            int index = Value.FindIndex(v => v.key == key);

            if (index < 0)
                return false;

            AchievementProgressInfo item = this[index];

            if (!item.IsUnlocked || item.IsRewardApplied)
                return false;

            var updatedItem = new AchievementProgressInfo(item)
            {
                rewardApplied = true
            };

            this[index] = updatedItem;
            return true;
        }

        /// <summary>
        /// Restores an authoritative unlocked state after a stale profile update.
        /// </summary>
        /// <param name="unlockedState">Previously confirmed achievement state.</param>
        /// <returns><c>true</c> when the unlocked state was restored.</returns>
        public bool RestoreUnlockedState(AchievementProgressInfo unlockedState)
        {
            if (unlockedState == null || !unlockedState.IsUnlocked)
                return false;

            int index = Value.FindIndex(v => v.key == unlockedState.key);

            if (index < 0)
                return false;

            this[index] = new AchievementProgressInfo(unlockedState);
            return true;
        }
    }
}
