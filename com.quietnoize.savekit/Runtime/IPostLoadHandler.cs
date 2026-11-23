namespace QuietNoize.SaveKit
{
    /// <summary>
    /// Defines a handler that is invoked after saved data has been loaded.
    /// </summary>
    public interface IPostLoadHandler
    {
        /// <summary>
        /// Called after the saved data has been loaded.
        /// </summary>
        void OnPostLoad();
    }
}