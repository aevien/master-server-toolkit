using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class Mailer : MonoBehaviour
    {
        public abstract Task<bool> SendMailAsync(string to, string subject, string body);
    }
}