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
        /// <param name="id"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByIdAsync(string id);
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
        Task<IAccountInfoData> GetAccountByPhoneNumberAsync(string phoneNumber);
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
        /// Gets user account from database by property value. This method can be used for guest accounts
        /// </summary>
        /// <param name="propertyKey"></param>
        /// <param name="propertyValue"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByPropertyAsync(string propertyKey, string propertyValue);
        /// <summary>
        /// Saves code that user gets when reset pasword request
        /// </summary>
        /// <param name="account"></param>
        /// <param name="code"></param>
        Task SavePasswordResetCodeAsync(IAccountInfoData account, string code);
        /// <summary>
        /// Get data for password reset
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        Task<string> GetPasswordResetDataAsync(string email);
        /// <summary>
        /// Email confirmation code user gets after successful registration
        /// </summary>
        /// <param name="email"></param>
        /// <param name="code"></param>
        Task SaveEmailConfirmationCodeAsync(string email, string code);
        /// <summary>
        /// Get email confirmation code for user after successful registration
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        Task<string> GetEmailConfirmationCodeAsync(string email);
        /// <summary>
        /// Get phone number confirmation code for user
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns></returns>
        Task<string> GetPhoneNumberConfirmationCodeAsync(string phoneNumber);
        /// <summary>
        /// Check phone number confirmation code for user
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns></returns>
        Task<bool> CheckPhoneNumberConfirmationCodeAsync(string confirmationCode);
        /// <summary>
        /// Update all account information in database
        /// </summary>
        /// <param name="account"></param>
        Task<bool> UpdateAccountAsync(IAccountInfoData account);
        /// <summary>
        /// Create new account in database
        /// </summary>
        /// <param name="account"></param>
        Task<string> InsertNewAccountAsync(IAccountInfoData account);
        /// <summary>
        /// Insert account token to database
        /// </summary>
        /// <param name="account"></param>
        /// <param name="token"></param>
        Task<bool> InsertTokenAsync(IAccountInfoData account, string token);
    }
}