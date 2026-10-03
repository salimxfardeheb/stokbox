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

        /// <summary>
        /// Returns asked to the repository: sale id and merged lines.
        /// </summary>
        public List<KeyValuePair<long, IReadOnlyList<ReturnRequestLine>>> ReturnsAsked { get; } =
            new List<KeyValuePair<long, IReadOnlyList<ReturnRequestLine>>>();

        public List<long> Cancelled { get; } = new List<long>();

        public DateTime LastSearchFromUtc { get; private set; }

        public DateTime LastSearchToUtc { get; private set; }

        public string LastSearchNumber { get; private set; }

        public IReadOnlyList<SaleSummary> Search(DateTime fromUtc, DateTime toUtc, string numberText)
        {
            LastSearchFromUtc = fromUtc;
            LastSearchToUtc = toUtc;
            LastSearchNumber = numberText;
            return new SaleSummary[0];
        }

        public Sale GetById(long saleId)
        {
            return null;
        }

        public IReadOnlyList<SaleReturn> GetReturns(long saleId)
        {
            return new SaleReturn[0];
        }

        public void Cancel(long saleId, DateTime cancelledAtUtc)
        {
            Cancelled.Add(saleId);
        }

        public SaleReturn Return(long saleId, IReadOnlyList<ReturnRequestLine> lines, DateTime returnedAtUtc)
        {
            ReturnsAsked.Add(new KeyValuePair<long, IReadOnlyList<ReturnRequestLine>>(saleId, lines));
            return new SaleReturn { Id = ReturnsAsked.Count, SaleId = saleId, CreatedAtUtc = returnedAtUtc, Lines = new SaleReturnLine[0] };
        }

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
