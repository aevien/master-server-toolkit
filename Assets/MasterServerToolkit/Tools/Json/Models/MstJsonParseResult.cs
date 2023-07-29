namespace MasterServerToolkit.Json
{
    public struct MstJsonParseResult
    {
        public readonly MstJson result;
        public readonly int offset;
        public readonly bool pause;

        public MstJsonParseResult(MstJson result, int offset, bool pause)
        {
            this.result = result;
            this.offset = offset;
            this.pause = pause;
        }
    }
}