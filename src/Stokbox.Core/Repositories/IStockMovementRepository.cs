using System;

namespace Stokbox.Core.Repositories
{
    public interface IStockMovementRepository
    {
        /// <summary>
        /// Records an ENTREE movement of the given positive quantity (RG-01).
        /// </summary>
        long AddEntry(long productId, int quantity, DateTime createdAtUtc);
    }
}
