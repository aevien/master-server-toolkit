using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents generic database for profiles
    /// </summary>
    public interface IProfilesDatabaseAccessor : IDatabaseAccessor
    {
        /// <summary>
        /// Should restore all values of the given profile, 
        /// or not change them, if there's no entry in the database
        /// </summary>
        /// <param name="profile"></param>
        Task RestoreProfileAsync(ObservableServerProfile profile);

        /// <summary>
        /// Should save updated profile into database
        /// </summary>
        /// <param name="profile"></param>
        Task UpdateProfileAsync(ObservableServerProfile profile);
    }
}