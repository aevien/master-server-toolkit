using MasterServerToolkit.MasterServer;
using System.Threading.Tasks;

namespace MasterServerToolkit.GameService
{
    public interface IPlayerValidator
    {
        Task<bool> Validate(MstProperties data);
    }
}