using Stokbox.Core.Entities;

namespace Stokbox.Core.Repositories
{
    public interface ISaleRepository
    {
        /// <summary>
        /// Records the sale, its lines and one VENTE movement per line in a single transaction (RG-07),
        /// after checking again, inside that transaction, that every product is active and in stock (RG-10).
        /// The sale takes the next number of its day.
        /// </summary>
        /// <exception cref="Validation.BusinessRuleException">A product is missing, archived or out of stock; nothing is recorded.</exception>
        Sale Create(NewSale sale);
    }
}
