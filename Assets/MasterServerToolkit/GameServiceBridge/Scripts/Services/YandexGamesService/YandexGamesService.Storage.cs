namespace MasterServerToolkit.GameService
{
    public partial class YandexGamesService : BaseGameService
    {
        public override void SetString(string key, string value)
        {
            Data.SetField(key, value);
            SavePlayerData(Data);
        }

        public override void SetFloat(string key, float value)
        {
            Data.SetField(key, value);
            SavePlayerData(Data);
        }

        public override void SetInt(string key, int value)
        {
            Data.SetField(key, value);
            SavePlayerData(Data);
        }

        public override string GetString(string key, string defaultValue = "")
        {
            Data.GetField(out string value, key, defaultValue);
            return value;
        }

        public override float GetFloat(string key, float defaultValue = 0f)
        {
            Data.GetField(out float value, key, defaultValue);
            return value;
        }

        public override int GetInt(string key, int defaultValue = 0)
        {
            Data.GetField(out int value, key, defaultValue);
            return value;
        }
    }
}