using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace BackgroundService.Models
{
    // Основна модель для отримання даних з часової (Time-Series) колекції "telemetry"
    public class DeviceTelemetry
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }

        [BsonElement("metadata")]
        public DeviceMetadata Metadata { get; set; } = null!;

        [BsonElement("metrics")]
        public DeviceMetrics Metrics { get; set; } = null!;
    }

    // Метадані
    public class DeviceMetadata
    {
        [BsonElement("deviceId")]
        public string DeviceId { get; set; } = null!;

        [BsonElement("type")]
        public string Type { get; set; } = null!;

        [BsonElement("location")]
        public string Location { get; set; } = null!;
    }

    // Метрики (можуть бути різними для різних типів пристроїв, тому багато полів є nullable)
    public class DeviceMetrics
    {
        // --- Метрики, які зустрічаються у кількох пристроях ---
        [BsonElement("temperature")]
        public double? Temperature { get; set; }

        [BsonElement("vibration")]
        public double? Vibration { get; set; }

        [BsonElement("current")]
        public double? Current { get; set; }

        [BsonElement("voltage")]
        public double? Voltage { get; set; }

        // --- Метрики двигуна (motor_01) ---
        [BsonElement("rpm")]
        public int? Rpm { get; set; }

        // --- Метрики насоса (pump_01) ---
        [BsonElement("flow_rate")]
        public double? FlowRate { get; set; }

        [BsonElement("inlet_pressure")]
        public double? InletPressure { get; set; }

        [BsonElement("outlet_pressure")]
        public double? OutletPressure { get; set; }

        [BsonElement("cavitation_index")]
        public double? CavitationIndex { get; set; }

        // --- Метрики інвертора СЕС (inv_solar_02) ---
        [BsonElement("voltage_dc")]
        public double? VoltageDc { get; set; }

        [BsonElement("current_dc")]
        public double? CurrentDc { get; set; }

        [BsonElement("power_ac")]
        public double? PowerAc { get; set; }

        // --- Метрики комірки BESS (bess_cell_03) ---
        [BsonElement("soc")]
        public double? Soc { get; set; }

        // --- Метрики трансформатора (transformer_04) ---
        [BsonElement("oil_temperature")]
        public double? OilTemperature { get; set; }

        [BsonElement("load_percentage")]
        public int? LoadPercentage { get; set; }
    }

    // Модель для збереження зафіксованих аварій у звичайну колекцію "alerts"
    public class DeviceAlert
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("deviceId")]
        public string DeviceId { get; set; } = null!;

        [BsonElement("message")]
        public string Message { get; set; } = null!;

        [BsonElement("severity")]
        public string Severity { get; set; } = null!;

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;
    }
}