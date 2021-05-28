
using System.Collections.Generic;
using Tricentis.TCCore.Base.Folders;
using Tricentis.TCCore.BusinessObjects.Folders;
using Tricentis.TCCore.BusinessObjects.Tasks;
using Tricentis.TCCore.Persistency;
using Tricentis.TCCore.Persistency.Localization;
using Tricentis.TCCore.Persistency.MetaInfo;

namespace CIAddin
{
    class ImportSubsetWithoutFolderStrategy2 : IImportSubsetFolderStrategy
    {
        public bool ShouldPauseUndo() => true;

        public string GetNameOfTopComponentFolder(string importSpecifier) => TCLocalization.Instance.GetString("Component");

        public void InsertTopFolder(TCFolder folderToImportTo, TCFolder importedProjectTopFolder)
        {
            folderToImportTo.DisplayedName = importedProjectTopFolder.DisplayedName;
            List<OwnedItem> ownedItemList = new List<OwnedItem>();
            foreach (OwnedItem ownedItem in (GenericAssocN<OwnedItem>)importedProjectTopFolder.Items)
                ownedItemList.Add(ownedItem);
            this.DeleteFolderItems(folderToImportTo);
            folderToImportTo.Items.AddAll((ICollection<OwnedItem>)ownedItemList);
            foreach (string configurationParamName in importedProjectTopFolder.TestConfiguration.ConfigurationParamNames)
                folderToImportTo.TestConfiguration.SetConfigurationParam(configurationParamName, importedProjectTopFolder.TestConfiguration.GetConfigurationParamValue(configurationParamName), true);
        }

        private void DeleteFolderItems(TCFolder folderToImportTo)
        {
            foreach (PersistableObject persistableObject in new List<OwnedItem>((IEnumerable<OwnedItem>)folderToImportTo.Items))
                persistableObject.Delete();
        }
    }
}