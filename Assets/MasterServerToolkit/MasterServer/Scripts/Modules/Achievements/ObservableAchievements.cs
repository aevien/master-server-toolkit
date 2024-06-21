using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableAchievements : ObservableBaseList<AchievemetProgressData>
    {
        public ObservableAchievements(ushort key) : base(key) { }
        public ObservableAchievements(ushort key, List<AchievemetProgressData> defaultValues) : base(key, defaultValues) { }

        public AchievemetProgressData GetProgressById(string id)
        {
            return _value.Find(a => a.id == id);
        }

        public void UpdateProgress(string id, int addValue, int maxValue)
        {
            int index = _value.FindIndex(a => a.id == id);

            if (index >= 0)
            {
                var item = this[index];
                item.currentValue += addValue;
                item.maxValue = maxValue;

                if (item.currentValue >= item.maxValue)
                {
                    item.endTime = DateTimeOffset.Now.ToUnixTimeSeconds();
                }

                this[index] = item;
            }
            else
            {
                Add(new AchievemetProgressData()
                {
                    id = id,
                    currentValue = addValue,
                    maxValue = maxValue
                });
            }
        }

        public bool ContainsProgress(string id)
        {
            return _value.FindIndex(a => a.id == id) >= 0;
        }

        public bool IsProgressMet(string id)
        {
            int index = _value.FindIndex(a => a.id == id);

            if (index >= 0)
            {
                var item = this[index];
                return item.currentValue >= item.maxValue;
            }
            else
            {
                return false;
            }
        }

        public override void Deserialize(string value)
        {
            FromJson(value);
        }

        public override void FromJson(MstJson json)
        {
            _value.Clear();

            foreach (var item in json)
            {
                var newItem = new AchievemetProgressData();
                newItem.FromJson(item);
                Add(newItem);
            }
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

            foreach (var item in this)
            {
                json.Add(item.ToJson());
            }

            return json;
        }

        protected override AchievemetProgressData ReadValue(EndianBinaryReader reader)
        {
            return reader.ReadPacket<AchievemetProgressData>();
        }

        protected override void WriteValue(AchievemetProgressData value, EndianBinaryWriter writer)
        {
            writer.Write(value);
        }
    }
}