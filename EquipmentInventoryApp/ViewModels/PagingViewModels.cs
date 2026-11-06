
namespace EquipmentInventoryApp.ViewModels
{
    // One page of a longer list (Milestone 3: history tables are paginated)
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public int TotalCount { get; set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

        // Keeps the page number between 1 and the last page
        public static int ClampPage(int page, int totalCount, int pageSize)
        {
            var lastPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return Math.Min(Math.Max(page, 1), lastPage);
        }
    }

    // Settings for the Previous / Next links under a table
    public class PagerViewModel
    {
        public int Page { get; set; }

        public int TotalPages { get; set; }

        // The query string name for this table's page number, such as "checkoutPage"
        public string PageParameter { get; set; } = "page";

        // The section to scroll back to after changing pages
        public string? Anchor { get; set; }

        // Describes the table for screen readers, such as "Checkouts"
        public string Label { get; set; } = "Results";
    }
}
