namespace Bulky.Models.ViewModels
{
    public class ShoppingCartVM
    {
        public IEnumerable<ShoppingCard> ShoppingCardList { get; set; }
        public OrderHeader orderHeader { get; set; }

    }
}
