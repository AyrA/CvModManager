namespace CvModManager.Lib
{
    internal static class Tools
    {
        public const bool IsDebug =
#if DEBUG
            true;
#else
            false;
#endif
    }
}
