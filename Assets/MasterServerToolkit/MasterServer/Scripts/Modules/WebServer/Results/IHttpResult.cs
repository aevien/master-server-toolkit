using System.Net;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IHttpResult
    {
        Task Execute(HttpListenerContext context);
    }
}