using System;
using System.Collections.Generic;
using System.Text;

namespace CoffeeNChill.Functions.DTOs
{
    // Per the Part 1 brief, UpdateMenuItem updates "price or availability": both
    // fields are optional so a caller can send either one, or both, without being
    // forced to resend Name/Description. Name/Description stay here as optional
    // overrides for completeness, but are never required.
    public class UpdateMenuItemRequest
    {
        // Optional: updated menu item name
        public string? Name { get; set; }

        // Optional: updated description
        public string? Description { get; set; }

        // Optional: updated price
        public double? Price { get; set; }

        // Optional: updated availability
        public bool? IsAvailable { get; set; }
    }
}
