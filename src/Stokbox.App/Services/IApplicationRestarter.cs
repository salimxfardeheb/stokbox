namespace Stokbox.App.Services
{
    public interface IApplicationRestarter
    {
        /// <summary>
        /// Closes the application and starts it again, as after a restored backup.
        /// </summary>
        void Restart();
    }
}
