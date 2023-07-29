using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MySQL
{
    public abstract class MySqlDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField, Tooltip("Use [connectionString] field to set your own connection string for database client")]
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
