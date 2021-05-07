namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public class DtoBase
    {
        public object this[string propertyName]
        {
            get { return GetType().GetProperty(propertyName).GetValue(this, null); }
            set { GetType().GetProperty(propertyName).SetValue(this, value, null); }
        }
    }
}
