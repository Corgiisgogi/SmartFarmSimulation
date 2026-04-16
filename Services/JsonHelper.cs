using System.Collections.Generic;
using System.Linq;

namespace SmartFarmUI.Services
{
    public static class JsonHelper
    {
        public static string ToJson(object obj)
        {
            if (obj == null) return "null";

            if (obj is string str) return "\"" + EscapeJson(str) + "\"";
            if (obj is bool b) return b ? "true" : "false";
            if (obj is int || obj is long || obj is double || obj is float || obj is decimal)
                return obj.ToString();

            if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
            {
                var items = enumerable.Cast<object>().Select(ToJson);
                return "[" + string.Join(",", items) + "]";
            }

            var properties = obj.GetType().GetProperties();
            var jsonParts = new List<string>();

            foreach (var prop in properties)
            {
                var value = prop.GetValue(obj);
                var jsonValue = ToJson(value);
                jsonParts.Add($"\"{prop.Name}\":{jsonValue}");
            }

            return "{" + string.Join(",", jsonParts) + "}";
        }

        public static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\n", "\\n")
                      .Replace("\r", "\\r")
                      .Replace("\t", "\\t");
        }
    }
}
