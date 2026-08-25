using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class LiteDatabaseAccessorFactory : DatabaseAccessorFactory
    {
        [Header("Settings"), SerializeField]
        [Tooltip("LiteDB file name without a directory. An empty value is replaced in the Editor with a name derived from this factory type.")]
        protected string databaseName = "";

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                string[] values = GetType().Name.FromCamelcase().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                databaseName = values.Length > 0 ? values[0].ToLower() : "database_" + DateTime.UtcNow.ToFileTimeUtc();
            }
        }

        public override void CreateAccessors() { }
    }
}
