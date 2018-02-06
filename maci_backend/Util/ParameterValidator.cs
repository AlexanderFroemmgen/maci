using System;
using System.Globalization;
using Backend.Data.Persistence.Model;

namespace Backend.Util
{
    public static class ParameterValidator
    {
        public static bool IsValid(ParameterType type, string value)
        {
            if (type == ParameterType.Int)
            {
                int _;
                return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);
            }

            if (type == ParameterType.Float)
            {
                float _;
                return float.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _);
            }

            if (type == ParameterType.String)
            {
                return true;
            }

            throw new ArgumentException("Invalid parameter type.");
        }
    }
}