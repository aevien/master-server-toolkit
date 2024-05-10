using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IAccountsDatabaseAccessor : IDatabaseAccessor
    {
        /// <summary>
        /// Should create an empty object with account data.
        /// </summary>
        /// <returns></returns>
        IAccountInfoData CreateAccountInstance();
        /// <summary>
        /// Gets user account from database by id
        /// </summary>
        /// <param name="accountId"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByIdAsync(string accountId);
        /// <summary>
        /// Gets user account from database
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByUsernameAsync(string username);
        /// <summary>
        /// Gets user account from database by email
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByEmailAsync(string email);
        /// <summary>
        /// Gets user account from database by phone number
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue);
        /// <summary>
        /// Gets user account from database by token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByTokenAsync(string token);
        /// <summary>
        /// Gets user account from database by device id. This method can be used for guest accounts
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId);
        /// <summary>
        /// Saves code that user gets when reset pasword request
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        Task SavePasswordResetCodeAsync(string email, string code);
        /// <summary>
        /// Checks password reset code
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        Task<bool> CheckPasswordResetCodeAsync(string email, string code);
        /// <summary>
        /// Email confirmation code user gets after successful registration
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        Task SaveEmailConfirmationCodeAsync(string email, string code);
        /// <summary>
        /// Checks email confirmation code for user after successful registration
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        /// <returns></returns>
        Task<bool> CheckEmailConfirmationCodeAsync(string email, string code);
        /// <summary>
        /// Update all account information in database
        /// </summary>
        /// <param name="account"></param>
        Task UpdateAccountAsync(IAccountInfoData account);
        /// <summary>
        /// Create new account in database
        /// </summary>
        /// <param name="account"></param>
        Task<string> InsertAccountAsync(IAccountInfoData account);
        /// <summary>
        /// Insert account token to database
        /// </summary>
        /// <param name="account"></param>
        /// <param name="token"></param>
        Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token);
    }
}