namespace CharacterApp
{
    /// <summary>Контракт для страниц, умеющих сохранять/загружать данные.</summary>
    public interface ISaveLoad
    {
        void QuickSave();
        void SaveAs();
        void LoadJSON();
    }
}
