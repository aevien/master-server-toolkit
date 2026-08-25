using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstCreate
    {
        public IServerSocket ServerSocket()
        {
            return Mst.Settings.ServerSocketFactory();
        }

        public IClientSocket ClientSocket()
        {
            return Mst.Settings.ClientSocketFactory();
        }

        /// <summary>
        /// Creates a logger of the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Logger Logger(string name)
        {
            return LogManager.GetLogger(name);
        }

        /// <summary>
        /// Creates a logger of the given name, and sets its defualt log level
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaulLogLevel"></param>
        /// <returns></returns>
        public Logger Logger(string name, LogLevel defaulLogLevel)
        {
            var logger = LogManager.GetLogger(name);
            logger.LogLevel = defaulLogLevel;
            return logger;
        }
    }
}