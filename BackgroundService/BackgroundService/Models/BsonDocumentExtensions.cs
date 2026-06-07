using MongoDB.Bson;
using System;

namespace BackgroundService.Models
{
    // Розширення для безпечного отримання метрик з BsonDocument, враховуючи можливу відсутність полів або null-значення
    public static class BsonDocumentExtensions
    {
        // Бзепечно дістати double
        public static double? GetDoubleMetric(this BsonDocument doc, string field)
        {
            if (doc.TryGetValue(field, out var v) && !v.IsBsonNull)
            {
                return v.AsDouble;
            }
            else return null;
        }

        // Безпечно дістати int

        public static int? GetIntMetric(this BsonDocument doc, string field)
        {
            if (doc.TryGetValue(field, out var v) && !v.IsBsonNull)
            {
                return v.AsInt32;
            }
            else return null;
        }

        // Безпечно дістати ковзну метрику з округленням
        public static double? GetRollingMetric(this BsonDocument doc, string field, int decimals = 2)
        {
            if (doc.TryGetValue(field, out var v) && !v.IsBsonNull)
            {
                return Math.Round(v.AsDouble, decimals);
            }
            else return null;
        }
    }
}
