namespace QuietNoize.SaveKit
{
    /// <summary>
    /// Defines a handler that is invoked before saved data is loaded.
    /// </summary>
    public interface IPreLoadHandler
    {
        /// <summary>
        /// Called before the saved data is loaded.
        /// </summary>
        void OnPreLoad();
    }
}