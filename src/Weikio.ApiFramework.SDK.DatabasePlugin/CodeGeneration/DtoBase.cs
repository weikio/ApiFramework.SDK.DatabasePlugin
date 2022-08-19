namespace Weikio.ApiFramework.SDK.DatabasePlugin.CodeGeneration
{
    public class DtoBase
    {
        public object this[string propertyName]
        {
            get { return GetType().GetProperty(propertyName.Replace("@", string.Empty)).GetValue(this, null); }
            set { GetType().GetProperty(propertyName.Replace("@", string.Empty)).SetValue(this, value, null); }
        }
    }
}
