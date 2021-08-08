namespace DTO
{
    public class TokenValue
    {
        public ValueSymbol Value { get; }

        public ValueSymbol ChainValue { get; }

        public ValueSymbol FiatValue { get; }

        public TokenValue()
        {
            Value = new();
            ChainValue = new();
            FiatValue = new();
        }
    }
}
