using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.GameService
{
    public abstract class BasePlayerModule : BaseServiceModule, IPlayerModule
    {
        protected SuccessCallback authenticateCallback;

        public string Id { get; set; }
        public string Name { get; set; }
        public string Avatar { get; set; }
        public bool IsGuest { get; set; } = true;

        /// <inheritdoc />
        public virtual bool IsAuthenticationSupported { get; protected set; }

        public PlayerAccountState State { get; protected set; } = PlayerAccountState.Active;
        public PlayerAgeGroup AgeGroup { get; protected set; } = PlayerAgeGroup.Unknown;
        public MstJson Extra { get; set; }

        public event PlayerInfoHandler OnAuthenticateEvent;
        public event PlayerInfoHandler OnInfoChangedEvent;

        public virtual void Authenticate(SuccessCallback callback)
        {
            authenticateCallback = callback;
        }

        protected void NotifyOnAuthenticated(bool isSuccess, string error)
        {
            authenticateCallback?.Invoke(isSuccess, error);
            OnAuthenticateEvent?.Invoke(this);
            authenticateCallback = null;
        }

        protected void NotifyOnInfoChanged()
        {
            OnInfoChangedEvent?.Invoke(this);
        }
    }
}
