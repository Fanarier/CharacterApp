namespace CharacterApp.Pages
{
    /// <summary>Implemented by pages that support content-level search.</summary>
    public interface IPageSearchable
    {
        void FilterItems(string query);
    }
}
