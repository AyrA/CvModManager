namespace CvModManager.Lib
{
    public class ModInfo
    {
        public string Title { get; }
        public string Description { get; }
        public long? PublishedFileId { get; }
        public string[] Tags { get; }
        public bool Enabled { get; internal set; }
        public string ModFolderName => Path.GetFileName(ModDir);
        internal string ModDir { get; }

        internal ModInfo(string title, string description, long? publishedFileId, string[] tags, string modDir, bool enabled)
        {
            Title = title ?? string.Empty;
            Description = description ?? string.Empty;
            PublishedFileId = publishedFileId;
            Tags = tags ?? [];
            ModDir = modDir;
            Enabled = enabled;
        }

        internal ModInfo(InternalModInfo info, string modDir, bool enabled) : this(info.Title, info.Description, info.PublishedFileId, info.Tags, modDir, enabled)
        {
            //NOOP
        }
    }
}
