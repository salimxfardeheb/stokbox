namespace Stokbox.Core.Services
{
    /// <summary>
    /// What is entered at the first launch: the shop and the administrator password.
    /// </summary>
    public sealed class FirstRunInput
    {
        public string ShopName { get; set; }

        public string ShopAddress { get; set; }

        public string ShopPhone { get; set; }

        public string Password { get; set; }

        public string PasswordConfirmation { get; set; }
    }
}
