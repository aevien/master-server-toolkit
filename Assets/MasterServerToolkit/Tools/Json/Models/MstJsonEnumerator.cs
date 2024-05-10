using System;
using System.Collections;

namespace MasterServerToolkit.Json
{
    public class MstJsonEnumerator : IEnumerator
    {
        public MstJson target;

        // Enumerators are positioned before the first element until the first MoveNext() call.
        int position = -1;

        public MstJsonEnumerator(MstJson jsonObject)
        {
            if (!jsonObject.IsContainer)
                throw new InvalidOperationException("MstJson must be an array or object to provide an iterator");

            target = jsonObject;
        }

        public bool MoveNext()
        {
            position++;
            return position < target.Count;
        }

        public void Reset()
        {
            position = -1;
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        // ReSharper disable once InconsistentNaming
        public MstJson Current
        {
            get
            {
                return target[position];
            }
        }
    }
}
