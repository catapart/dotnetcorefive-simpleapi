namespace SimpleAPI_NetCore50.Websockets
{
    public interface ISessionAttribute
    {
        string Name { get; set; }
        System.Type DataType { get; }
        object Data { get; }
    }

    public interface ISessionAttribute<ValueT> : ISessionAttribute
    {
        new ValueT Data { get; }
    }

    public class SessionAttribute<ValueT> : ISessionAttribute<ValueT>
    {
        public string Name { get; set; }
        public System.Type DataType
        {
            get
            {
                return typeof(ValueT);
            }
        }

        object ISessionAttribute.Data
        {
            get
            {
                return Data;
            }
        }

        public ValueT Data { get; private set; }


        public SessionAttribute(string name, ValueT value)
        {
            Name = name;
            Data = value;
        }

        public void SetData(ValueT value)
        {
            Data = value;
        }
    }

    public static class SessionAttribute
    {
        public static ISessionAttribute Create(string name, object value)
        {
            System.Type attributeType = typeof(SessionAttribute<>).MakeGenericType(value.GetType());
            return System.Activator.CreateInstance(attributeType, name, value) as ISessionAttribute;
        }
    }
}
