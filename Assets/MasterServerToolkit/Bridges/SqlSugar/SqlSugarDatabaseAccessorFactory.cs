using MasterServerToolkit.MasterServer;
using SqlSugar;
using UnityEngine;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class SqlSugarDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        protected string connectionString = "Server=localhost;Database=master_server_toolkit;Uid=root;Pwd=qazwsxedc123!@#;Port=3306;";
        [SerializeField]
        protected bool autoCloseConnection = true;
        [SerializeField]
        protected LanguageType language = LanguageType.English;
        [SerializeField]
        protected DbType dataProvider = DbType.MySql;

        #endregion

        protected ConnectionConfig configuration;

        protected override void Awake()
        {
            base.Awake();

            connectionString = Mst.Args.AsString(Mst.Args.Names.DatabaseConnectionString, connectionString);
            autoCloseConnection = Mst.Args.AsBool(Mst.Args.Names.DatabaseAutoCloseConnection, autoCloseConnection);
            language = Mst.Args.AsEnum(Mst.Args.Names.DatabaseLanguageType, language);
            dataProvider = Mst.Args.AsEnum(Mst.Args.Names.DatabaseProvider, dataProvider);

            configuration = new ConnectionConfig()
            {
                ConnectionString = connectionString,
                DbType = dataProvider,
                IsAutoCloseConnection = autoCloseConnection,
                LanguageType = language,
                AopEvents = new AopEvents()
            };

            //configuration.AopEvents.OnLogExecuting = (sql, p) =>
            //{
            //    logger.Info(sql);
            //};
        }

        public override void CreateAccessors() { }
    }
}