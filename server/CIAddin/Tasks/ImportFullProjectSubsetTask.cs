using Tricentis.TCCore.BusinessObjects;
using Tricentis.TCCore.BusinessObjects.Tasks;
using Tricentis.TCCore.Persistency.Tasks;


namespace CIAddin.Tasks
{
    [TCTask(ApplicableType = typeof(TCProject))]
    [TCTaskParam("FilesToImport", TCTaskParamType.StringValues)]
    //[TCTaskParam("ConfirmUseLargeSubsetImport", TCTaskParamType.MsgBoxResult_OkCancel, "Ok")]
    public class ImportFullProjectSubsetTask : ImportProjectSubsetTask
    {
        public override string Name => "ImportFullProjectSubsetTask";

        public override bool RequiresChangeRights => false;
        protected override bool IsAPIOnlyTask => true;

        protected override bool SupressForGUIAccess => true;

        protected override IImportSubsetFolderStrategy GetImportSubsetFolderStrategy() => (IImportSubsetFolderStrategy)new ImportSubsetWithoutFolderStrategy2();       
    }
}