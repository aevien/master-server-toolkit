using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class DatabaseEntriesInfo<T>
    {
        public IEnumerable<T> entries = Enumerable.Empty<T>();
        public int total;
        public int filtered;
    }
}