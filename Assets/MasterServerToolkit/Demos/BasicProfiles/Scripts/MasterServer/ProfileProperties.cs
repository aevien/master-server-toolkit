using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public static class ProfileProperties
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="profile"></param>
        public static void Fill(ObservableProfile profile)
        {
            profile.Add(new ObservableString(ProfilePropertyKeys.displayName));
            profile.Add(new ObservableString(ProfilePropertyKeys.avatarUrl));
            profile.Add(new ObservableInt(ProfilePropertyKeys.bronze));
            profile.Add(new ObservableInt(ProfilePropertyKeys.silver));
            profile.Add(new ObservableInt(ProfilePropertyKeys.gold));
            profile.Add(new ObservableDictStringInt(ProfilePropertyKeys.items));
        }
    }
}
