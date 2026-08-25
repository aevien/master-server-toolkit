using MasterServerToolkit.MasterServer;
using UnityEngine;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using SqlSugar;
#endif

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class SqlSugarDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        #region INSPECTOR

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        [Header("Settings"), SerializeField]
        [Tooltip("Fallback provider connection string. -mstDatabaseConnectionString overrides this value at startup; its syntax must match Data Provider.")]
        protected string connectionString = "Server=localhost;Database=master_server_toolkit;Uid=root;Pwd=qazwsxedc123!@#;Port=3306;";
        [SerializeField]
        [Tooltip("Closes database connections automatically after each operation when supported by the selected provider. -mstDatabaseAutoCloseConnection overrides this value.")]
        protected bool autoCloseConnection = true;
        [SerializeField]
        [Tooltip("Language used by SqlSugar for provider messages and generated diagnostics. -mstDatabaseLanguageType overrides this value.")]
        protected LanguageType language = LanguageType.English;
        [SerializeField]
        [Tooltip("SQL database provider used to interpret the connection string and generate queries. -mstDatabaseProvider overrides this value; the provider must match the deployed database.")]
        protected DbType dataProvider = DbType.MySql;
#endif

        #endregion

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        protected ConnectionConfig configuration;
#endif

        protected override void Awake()
        {
            base.Awake();

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
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
#endif
        }

        public override void CreateAccessors() { }
    }
}
