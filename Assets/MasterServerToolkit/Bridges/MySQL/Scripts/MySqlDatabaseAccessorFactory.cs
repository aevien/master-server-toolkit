using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MySQL
{
    public abstract class MySqlDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        public HelpBox _header = new HelpBox()
        {
            Text = "This script is a factory, which sets up database accessors for the game. " +
            "Use [connectionString] field to set your own connection string for database client"
        };

        #region INSPECTOR

        [Header("Settings"), SerializeField]
        protected string connectionString = "Server=localhost;Database=master_server_toolkit;Uid=root;Pwd=qazwsxedc123!@#;Port=3306;";
        [SerializeField]
        protected bool useGlobalConnectionstring = true;

        #endregion

        protected override void Awake()
        {
            base.Awake();

            connectionString = useGlobalConnectionstring
                ? Mst.Args.AsString(Mst.Args.Names.DbConnectionString, connectionString)
                : connectionString;
        }

        public override void CreateAccessors() { }
    }
}
