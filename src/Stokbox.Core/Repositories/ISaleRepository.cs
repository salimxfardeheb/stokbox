using System;
using System.Collections.Generic;
using Stokbox.Core.Entities;

namespace Stokbox.Core.Repositories
{
    /// <summary>
    /// Sales are recorded, cancelled or returned; nothing here modifies or deletes one (RG-05).
    /// </summary>
    public interface ISaleRepository
    {
        /// <summary>
        /// Records the sale, its lines and one VENTE movement per line in a single transaction (RG-07),
        /// after checking again, inside that transaction, that every product is active and in stock (RG-10).
        /// The sale takes the next number of its day.
        /// </summary>
        /// <exception cref="Validation.BusinessRuleException">A product is missing, archived or out of stock; nothing is recorded.</exception>
        Sale Create(NewSale sale);

        /// <summary>
        /// Sales whose number contains the text when one is given, otherwise the sales of the period
        /// [fromUtc, toUtc[; the latest first.
        /// </summary>
        IReadOnlyList<SaleSummary> Search(DateTime fromUtc, DateTime toUtc, string numberText);

        /// <summary>
        /// The sale with its lines and the quantity already returned on each; null when it does not exist.
        /// </summary>
        Sale GetById(long saleId);

        /// <summary>
        /// The returns of the sale, the oldest first.
        /// </summary>
        IReadOnlyList<SaleReturn> GetReturns(long saleId);

        /// <summary>
        /// In a single transaction: marks a VALIDEE sale without any return as ANNULEE
        /// and puts every line back in stock with an ANNULATION movement.
        /// </summary>
        /// <exception cref="Validation.BusinessRuleException">The sale is missing, already cancelled or has returns; nothing is recorded.</exception>
        void Cancel(long saleId, DateTime cancelledAtUtc);

        /// <summary>
        /// In a single transaction: records the return, its lines and one RETOUR movement per line.
        /// Each quantity must not exceed what was sold minus what was already returned (RG-06).
        /// </summary>
        /// <exception cref="Validation.BusinessRuleException">The sale is missing or cancelled, or a quantity is refused; nothing is recorded.</exception>
        SaleReturn Return(long saleId, IReadOnlyList<ReturnRequestLine> lines, DateTime returnedAtUtc);
    }
}
