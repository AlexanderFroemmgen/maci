using System.Globalization;

namespace Backend.Util
{
    public static class ParseUtils
    {
        public static object ParseToClosestPossibleValueType(string value)
        {
            int maybeIntValue;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out maybeIntValue))
            {
                return maybeIntValue;
            }

            float maybeFloatValue;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out maybeFloatValue))
            {
                return maybeFloatValue;
            }

            return value;
        }
    }
}