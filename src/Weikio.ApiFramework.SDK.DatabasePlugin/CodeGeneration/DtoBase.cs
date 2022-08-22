namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public class DtoBase
    {
        public object this[string propertyName]
        {
            get { return GetType().GetProperty(propertyName.TrimStart('@')).GetValue(this, null); }
            set { GetType().GetProperty(propertyName.TrimStart('@')).SetValue(this, value, null); }
        }
    }
}
