using MasterServerToolkit.Bridges;
using MasterServerToolkit.Demos.BasicProfile;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;

namespace MasterServerToolkit.Bridges
{
	public class MainMenuView : UIView
    {
        /// <summary>
        /// 
        /// </summary>
        public void SignOut()
        {
            Logger.Debug("Sign out");
            Mst.Client.Auth.SignOut();

            ViewsManager.HideAllViews();
            ViewsManager.Show<SignInView>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ShowProfileView()
        {
            ViewsManager.Show<ProfileView>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void ShowChatsView()
        {
            ViewsManager.Show<ChatsView>();
        }

        /// <summary>
        /// Quits the application
        /// </summary>
        public void Quit()
        {
            Mst.Runtime.Quit();
        }
    }
}