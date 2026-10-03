using System;
using System.Collections.Generic;
using System.Globalization;
using Stokbox.Core.Entities;
using Stokbox.Core.Repositories;

namespace Stokbox.Core.Tests.Fakes
{
    public sealed class InMemorySaleRepository : ISaleRepository
    {
        public List<NewSale> Created { get; } = new List<NewSale>();

        /// <summary>
        /// When set, Create fails with this exception, as the database would on a refused sale.
        /// </summary>
        public Exception Failure { get; set; }

        public Sale Create(NewSale sale)
        {
            if (Failure != null)
            {
                throw Failure;
            }

            Created.Add(sale);

            return new Sale
            {
                Id = Created.Count,
                Number = "V-" + sale.NumberDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "-" + Created.Count.ToString("D4", CultureInfo.InvariantCulture),
                CreatedAtUtc = sale.CreatedAtUtc,
                TotalCents = sale.TotalCents,
                ReceivedCents = sale.ReceivedCents,
                ChangeCents = sale.ChangeCents,
                Status = Sale.StatusValidated,
                Lines = sale.Lines
            };
        }
    }
}
