using EduMaster.Domain.Common;


namespace EduMaster.Domain.Streams.ValueObjects
{
    public class StreamName
    {
        public string Value { get; } = string.Empty;

        public StreamName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException("لا يمكن ان يكون اسم الشعبة فارغا.");

            if (value.Length > 50)
                throw new DomainException("اسم الشعبة طويل جدا.");


            Value = value.Trim(); 
        }

        public override bool Equals(object? obj)
        {
            if(obj is  StreamName other)
                return Value.Equals(other.Value);

            return false;
        }

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value;

    }
}
