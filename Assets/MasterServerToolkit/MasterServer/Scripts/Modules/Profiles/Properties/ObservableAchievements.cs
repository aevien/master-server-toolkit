using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

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
            var json = MstJson.EmptyArray;

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
        public bool IsUnlocked(string key)
        {
            var achievemet = _value.Find(v => v.key == key);
            return achievemet != null ? achievemet.IsUnlocked : false;
        }

        /// <summary>
        /// Tries to unlock achievement with new progress
        /// </summary>
        /// <param name="key"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        public bool TryToUnlock(string key, int progress)
        {
            var index = Value.FindIndex(v => v.key == key);

            if (index >= 0)
            {
                var item = this[index];

                if (!item.IsUnlocked)
                {
                    item.progress += progress;

                    if (item.IsUnlocked)
                    {
                        item.unlockDate = DateTime.UtcNow;
                    }

                    this[index] = item;

                    return item.IsUnlocked;
                }
                else
                {
                    // Return false to prevent new achievement unlock reward
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
