namespace Stokbox.App.ViewModels
{
    /// <summary>
    /// One entry of the side menu.
    /// </summary>
    public sealed class NavigationItem
    {
        public NavigationItem(string key, string title)
        {
            Key = key;
            Title = title;
        }

        public string Key { get; }

        public string Title { get; }
    }
}
