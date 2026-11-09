using System;
using System.Collections.Generic;
using System.Text;

namespace CoffeeNChill.Functions.DTOs
{
    public class UpdateOrderStatusRequest
    {
        public string OrderId { get; set; } = string.Empty;

        public DateTimeOffset OrderTimestamp { get; set; }

        public string Status { get; set; } = string.Empty;
    }
}

