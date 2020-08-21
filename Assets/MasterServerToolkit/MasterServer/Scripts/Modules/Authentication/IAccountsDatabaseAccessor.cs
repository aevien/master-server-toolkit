using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IAccountsDatabaseAccessor
    {
        /// <summary>
        /// Should create an empty object with account data.
        /// </summary>
        /// <returns></returns>
        IAccountInfoData CreateAccountInstance();
        /// <summary>
        /// Gets user account from database
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByUsernameAsync(string username);
        /// <summary>
        /// Gets user account from database by token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByTokenAsync(string token);
        /// <summary>
        /// Gets user account from database by email
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        Task<IAccountInfoData> GetAccountByEmailAsync(string email);
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
        Task<IPasswordResetData> GetPasswordResetDataAsync(string email);
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
        /// Update all account information in database
        /// </summary>
        /// <param name="account"></param>
        Task UpdateAccountAsync(IAccountInfoData account);
        /// <summary>
        /// Create new account in database
        /// </summary>
        /// <param name="account"></param>
        Task InsertNewAccountAsync(IAccountInfoData account);
        /// <summary>
        /// Insert account token to database
        /// </summary>
        /// <param name="account"></param>
        /// <param name="token"></param>
        Task InsertTokenAsync(IAccountInfoData account, string token);
    }
}