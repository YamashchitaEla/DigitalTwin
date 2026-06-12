using BackgroundService.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Linq; // Додано для трансформації результатів агрегації
using System.Threading;
using System.Threading.Tasks;

namespace BackgroundService.Services
{
    public class TelemetrySimulatorService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly string _broker = "localhost";
        private readonly IMongoCollection<BsonDocument> _telemetryCollection;
        private readonly IMongoCollection<DeviceAlert> _alertsCollection;
        private readonly IMongoCollection<BsonDocument> _summaryCollection;
        private readonly ILogger<TelemetrySimulatorService> _logger;
        private readonly IHubContext<TelemetryHubSignalR> _hubContext;
        private readonly Random _random = new();

        public TelemetrySimulatorService(ILogger<TelemetrySimulatorService> logger, IHubContext<TelemetryHubSignalR> hubContext)
        {
            _logger = logger;
            _hubContext = hubContext;

            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("DigitalTwinDb");

            _telemetryCollection = database.GetCollection<BsonDocument>("telemetry");
            _alertsCollection = database.GetCollection<DeviceAlert>("alerts");
            _summaryCollection = database.GetCollection<BsonDocument>("devices_summary");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Повний симулятор з підтримкою алертів, мотогодин та ковзних вікон запущено.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.UtcNow;
                    var hour = now.Hour;

                    // --- ДВИГУН (motor_01) ---
                    var motorTemp = Math.Round(85 + _random.NextDouble() * 30, 2);
                    var motorVib = Math.Round(1.5 + _random.NextDouble() * 0.3, 2);
                    var motorRpm = 1470 + _random.Next(0, 10);
                    var motorCurrent = Math.Round(22.4 + (motorVib * 0.4), 2);

                    var motorDoc = new BsonDocument
                    {
                        { "timestamp", now },
                        { "metadata", new BsonDocument { { "deviceId", "motor_01" }, { "type", "motor" }, { "location", "Sector_A" } } },
                        { "metrics", new BsonDocument
                            {
                                { "temperature", motorTemp },
                                { "vibration", motorVib },
                                { "rpm", motorRpm },
                                { "current", motorCurrent }
                            }
                        }
                    };

                    // МОТОГОДИНИ двигуна
                    if (motorRpm > 0)
                    {
                        var filter = Builders<BsonDocument>.Filter.Eq("deviceId", "motor_01");
                        var update = Builders<BsonDocument>.Update.Inc("totalOperatingHours", 5.0 / 3600.0).Set("deviceId", "motor_01").Set("lastUpdated", now);
                        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
                        var updatedSummary = await _summaryCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                        double totalHours = Math.Round(updatedSummary["totalOperatingHours"].AsDouble, 4);

                        await _hubContext.Clients.All.SendAsync("ReceiveOperatingHours", new { deviceId = "motor_01", totalOperatingHours = totalHours, lastUpdated = now }, stoppingToken);
                    }

                    // --- НАСОС (pump_01) ---
                    var pumpActive = _random.NextDouble() > 0.2;
                    var pumpFlow = pumpActive ? Math.Round(45.5 + _random.NextDouble() * 2, 1) : 0;
                    var inletPressure = pumpActive ? Math.Round(1.2 + _random.NextDouble() * 0.2, 2) : 0;
                    var outletPressure = pumpActive ? Math.Round((motorTemp > 85.0 ? 5.0 : 5.4) + _random.NextDouble() * 0.1, 2) : 0;
                    var cavitationIndex = pumpActive ? Math.Round(0.15 + _random.NextDouble() * 0.05, 3) : 0;

                    var pumpDoc = new BsonDocument
                    {
                        { "timestamp", now },
                        { "metadata", new BsonDocument { { "deviceId", "pump_01" }, { "type", "pump" }, { "location", "Sector_A" } } },
                        { "metrics", new BsonDocument
                            {
                                { "flow_rate", pumpFlow },
                                { "inlet_pressure", inletPressure },
                                { "outlet_pressure", outletPressure },
                                { "cavitation_index", cavitationIndex }
                            }
                        }
                    };

                    // МОТОГОДИНИ насосу
                    if (pumpFlow > 0)
                    {
                        var filter = Builders<BsonDocument>.Filter.Eq("deviceId", "pump_01");
                        var update = Builders<BsonDocument>.Update.Inc("totalOperatingHours", 5.0 / 3600.0).Set("deviceId", "pump_01").Set("lastUpdated", now);
                        var options = new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
                        var updatedSummary = await _summaryCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                        double totalHours = Math.Round(updatedSummary["totalOperatingHours"].AsDouble, 4);

                        await _hubContext.Clients.All.SendAsync("ReceiveOperatingHours", new { deviceId = "pump_01", totalOperatingHours = totalHours, lastUpdated = now }, stoppingToken);
                    }

                    // --- ІНВЕРТОР СЕС (inv_solar_02) ---
                    var isSunny = hour > 6 && hour < 19;
                    double solarFactor = !isSunny ? 0 : (hour < 12 ? (hour - 7) / 5.0 : (19 - hour) / 7.0);

                    var powerAc = Math.Round(solarFactor * 20, 2);
                    var temperature = Math.Round(25 + (solarFactor * 20) + _random.NextDouble() * 5, 2);
                    var voltageDc = isSunny ? Math.Round(600 + _random.NextDouble() * 20, 1) : 0;
                    var currentDc = Math.Round(solarFactor * 35, 2);

                    var inverterDoc = new BsonDocument
                    {
                        { "timestamp", now },
                        { "metadata", new BsonDocument { { "deviceId", "inv_solar_02" }, { "type", "inverter" }, { "location", "Solar_Field_1" } } },
                        { "metrics", new BsonDocument
                            {
                                { "voltage_dc", voltageDc },
                                { "current_dc", currentDc },
                                { "power_ac", powerAc },
                                { "temperature", temperature }
                            }
                        }
                    };

                    // --- КОМІРКА BESS (bess_cell_03) ---
                    double soc = 50;
                    double bessCurrent = 0;

                    if (hour >= 9 && hour <= 15) { soc = 35 + (hour - 9) * 9; bessCurrent = -25; }
                    else if ((hour >= 6 && hour < 9) || (hour >= 16 && hour <= 22))
                    {
                        soc = hour >= 16 ? 85 - (hour - 16) * 11 : 35 - (hour - 6) * 5;
                        bessCurrent = 40;
                    }
                    else
                    {
                        int nightHour = hour >= 23 ? hour - 23 : hour + 1;
                        double baseSoc = 20.0 - (nightHour * 0.83);
                        soc = Math.Round(baseSoc + (_random.NextDouble() * 0.2 - 0.1), 1);
                        bessCurrent = 0.5;
                    }

                    soc = Math.Round(soc, 1);
                    var bessVolts = Math.Round(384 + (bessCurrent * 0.1), 1);
                    var bessTemp = Math.Round(24 + (Math.Abs(bessCurrent) * 0.25) + _random.NextDouble() * 1.5, 2);

                    var bessDoc = new BsonDocument
                    {
                        { "timestamp", now },
                        { "metadata", new BsonDocument { { "deviceId", "bess_cell_03" }, { "type", "bess" }, { "location", "Storage_Room_B" } } },
                        { "metrics", new BsonDocument
                            {
                                { "soc", soc },
                                { "voltage", bessVolts },
                                { "current", bessCurrent },
                                { "temperature", bessTemp }
                            }
                        }
                    };

                    // --- СИЛОВИЙ ТРАНСФОРМАТОР (transformer_04) ---
                    var transOilTemp = Math.Round(45 + (hour % 5) + _random.NextDouble() * 2, 2);
                    var transformerVibration = Math.Round(0.5 + _random.NextDouble() * 0.5, 2);
                    var loadPercentage = _random.Next(60, 95);

                    var transformerDoc = new BsonDocument
                    {
                        { "timestamp", now },
                        { "metadata", new BsonDocument { { "deviceId", "transformer_04" }, { "type", "transformer" }, { "location", "Main_Substation" } } },
                        { "metrics", new BsonDocument
                            {
                                { "oil_temperature", transOilTemp },
                                { "vibration", transformerVibration },
                                { "load_percentage", loadPercentage }
                            }
                        }
                    };

                    // --- ВІДПРАВКА СИРИХ ДАНИХ В БАЗУ ---
                    await _telemetryCollection.InsertManyAsync(new[] {
                        motorDoc, pumpDoc, inverterDoc, bessDoc, transformerDoc
                    }, cancellationToken: stoppingToken);

                    _logger.LogInformation($"[IoT Simulator] Пакет телеметрії відправлено в {now:HH:mm:ss}");

                    // ----------------------------------------------------
                    var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[]
                    {
                        // Обрізати сирі дані
                        BsonDocument.Parse("{ $match: { $expr: { $gte: [ '$timestamp', { $subtract: [ '$$NOW', 10800000 ] } ] } } }"),

                        // Даунсемплінг в 15-хвилинки
                        BsonDocument.Parse(@"
                        {
                            $group: {
                                _id: {
                                    deviceId: '$metadata.deviceId',
                                    timeBucket: { $dateTrunc: { date: '$timestamp', unit: 'minute', binSize: 1 } }
                                },
                                deviceType: { $first: '$metadata.type' },
                                location: { $first: '$metadata.location' },
                                avg_temperature: { $avg: '$metrics.temperature' },
                                max_vibration: { $max: '$metrics.vibration' },
                                avg_rpm: { $avg: '$metrics.rpm' },
                                avg_current: { $avg: '$metrics.current' },
                                avg_flow_rate: { $avg: '$metrics.flow_rate' },
                                avg_inlet_pressure: { $avg: '$metrics.inlet_pressure' },
                                avg_outlet_pressure: { $avg: '$metrics.outlet_pressure' },
                                max_cavitation_index: { $max: '$metrics.cavitation_index' },
                                avg_voltage_dc: { $avg: '$metrics.voltage_dc' },
                                avg_current_dc: { $avg: '$metrics.current_dc' },
                                avg_power_ac: { $avg: '$metrics.power_ac' },
                                avg_soc: { $avg: '$metrics.soc' },
                                avg_bess_voltage: { $avg: '$metrics.voltage' },
                                avg_bess_current: { $avg: '$metrics.current' },
                                avg_oil_temperature: { $avg: '$metrics.oil_temperature' },
                                avg_load_percentage: { $avg: '$metrics.load_percentage' }
                            }
                        }"),

                        // Сортування
                        BsonDocument.Parse("{ $sort: { '_id.deviceId': 1, '_id.timeBucket': 1 } }"),

                        // Ковзне вікно
                        BsonDocument.Parse(@"
                        {
                            $setWindowFields: {
                                partitionBy: '$_id.deviceId',
                                sortBy: { '_id.timeBucket': 1 },
                                output: {
                                    rollingAvgTemp: { $avg: '$avg_temperature', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgOilTemp: { $avg: '$avg_oil_temperature', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingMaxVibration: { $max: '$max_vibration', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingMaxCavitation: { $max: '$max_cavitation_index', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgRpm: { $avg: '$avg_rpm', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgFlowRate: { $avg: '$avg_flow_rate', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgInletPressure: { $avg: '$avg_inlet_pressure', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgOutletPressure: { $avg: '$avg_outlet_pressure', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgCurrent: { $avg: '$avg_current', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgVoltageDc: { $avg: '$avg_voltage_dc', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgCurrentDc: { $avg: '$avg_current_dc', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgPowerAc: { $avg: '$avg_power_ac', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgSoc: { $avg: '$avg_soc', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgBessVoltage: { $avg: '$avg_bess_voltage', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgBessCurrent: { $avg: '$avg_bess_current', window: { range: [-5, 'current'], unit: 'minute' } },
                                    rollingAvgLoadPercentage: { $avg: '$avg_load_percentage', window: { range: [-5, 'current'], unit: 'minute' } }
                                }
                            }
                        }"),

                        // Проєкція в чисту структуру
                        BsonDocument.Parse(@"
                        {
                            $project: {
                                _id: 0,
                                deviceId: '$_id.deviceId',
                                timestamp: '$_id.timeBucket',
                                deviceType: 1,
                                location: 1,
                                metrics: {
                                    temperature: { $round: ['$avg_temperature', 2] },
                                    vibration: { $round: ['$max_vibration', 2] },
                                    rpm: { $round: ['$avg_rpm', 0] },
                                    current: { $round: ['$avg_current', 2] },
                                    flow_rate: { $round: ['$avg_flow_rate', 1] },
                                    inlet_pressure: { $round: ['$avg_inlet_pressure', 2] },
                                    outlet_pressure: { $round: ['$avg_outlet_pressure', 2] },
                                    cavitation_index: { $round: ['$max_cavitation_index', 3] },
                                    voltage_dc: { $round: ['$avg_voltage_dc', 1] },
                                    current_dc: { $round: ['$avg_current_dc', 2] },
                                    power_ac: { $round: ['$avg_power_ac', 2] },
                                    soc: { $round: ['$avg_soc', 1] },
                                    bess_voltage: { $round: ['$avg_bess_voltage', 1] },
                                    bess_current: { $round: ['$avg_bess_current', 2] },
                                    oil_temperature: { $round: ['$avg_oil_temperature', 2] },
                                    load_percentage: { $round: ['$avg_load_percentage', 0] }
                                },
                                rollingMetrics: {
                                    rollingAvgTemp: { $round: ['$rollingAvgTemp', 2] },
                                    rollingAvgOilTemp: { $round: ['$rollingAvgOilTemp', 2] },
                                    rollingMaxVibration: { $round: ['$rollingMaxVibration', 2] },
                                    rollingMaxCavitation: { $round: ['$rollingMaxCavitation', 3] },
                                    rollingAvgRpm: { $round: ['$rollingAvgRpm', 0] },
                                    rollingAvgFlowRate: { $round: ['$rollingAvgFlowRate', 1] },
                                    rollingAvgInletPressure: { $round: ['$rollingAvgInletPressure', 2] },
                                    rollingAvgOutletPressure: { $round: ['$rollingAvgOutletPressure', 2] },
                                    rollingAvgCurrent: { $round: ['$rollingAvgCurrent', 2] },
                                    rollingAvgVoltageDc: { $round: ['$rollingAvgVoltageDc', 1] },
                                    rollingAvgCurrentDc: { $round: ['$rollingAvgCurrentDc', 2] },
                                    rollingAvgPowerAc: { $round: ['$rollingAvgPowerAc', 2] },
                                    rollingAvgSoc: { $round: ['$rollingAvgSoc', 1] },
                                    rollingAvgBessVoltage: { $round: ['$rollingAvgBessVoltage', 1] },
                                    rollingAvgBessCurrent: { $round: ['$rollingAvgBessCurrent', 2] },
                                    rollingAvgLoadPercentage: { $round: ['$rollingAvgLoadPercentage', 0] }
                                }
                            }
                        }"),

                        // Беремо останній документ для кожного пристрою (найсвіжіший хвилинний пакет з ковзними метриками)
                        BsonDocument.Parse("{ $group: { _id: '$deviceId', latest: { $last: '$$ROOT' } } }")
                    });

                    // АКТИВНІ АЛЕРТИ З БАЗИ
                    var activeAlerts = await _alertsCollection.Find(a => a.IsActive).ToListAsync();

                    foreach (var alert in activeAlerts)
                    {
                        await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert);
                    }

                    // А Г Р Е Г А Ц І Я
                    var aggregationCursor = await _telemetryCollection.AggregateAsync(pipeline, cancellationToken: stoppingToken);
                    var aggregatedDocs = await aggregationCursor.ToListAsync(stoppingToken);

                    // Аналізуємо ковзне вікно
                    foreach (var res in aggregatedDocs)
                    {
                        var latestDoc = res["latest"].AsBsonDocument;
                        var deviceId = latestDoc["deviceId"].AsString;
                        var type = latestDoc["deviceType"].AsString;
                        var rollingMetrics = latestDoc["rollingMetrics"].AsBsonDocument;
                        switch (type)
                        {
                            case "motor":
                                {
                                    double rollingAvgTemp = rollingMetrics["rollingAvgTemp"].ToDouble();

                                    if (rollingAvgTemp > 85.0)
                                    {
                                        var existingAlert = await _alertsCollection.Find(a => a.DeviceId == "motor_01" && a.IsActive == true).FirstOrDefaultAsync(stoppingToken);
                                        if (existingAlert == null)
                                        {
                                            var alert = new DeviceAlert
                                            {
                                                DeviceId = "motor_01",
                                                Message = $"Перегрів двигуна! Поточна температура: {rollingAvgTemp:F2}°C (Критично: >85°C). Перевірте стан двигуна.",
                                                Severity = "Critical",
                                                Timestamp = now,
                                                IsActive = true
                                            };
                                            await _alertsCollection.InsertOneAsync(alert, cancellationToken: stoppingToken);
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert, stoppingToken);
                                            _logger.LogWarning($"[ALERT] motor_01 — перегрів двигуна: {rollingAvgTemp}°C");
                                        }
                                    }
                                    else
                                    {
                                        var filter = Builders<DeviceAlert>.Filter.And(Builders<DeviceAlert>.Filter.Eq(a => a.DeviceId, deviceId), Builders<DeviceAlert>.Filter.Eq(a => a.IsActive, true));
                                        var update = Builders<DeviceAlert>.Update.Set(a => a.IsActive, false);
                                        var options = new FindOneAndUpdateOptions<DeviceAlert> { ReturnDocument = ReturnDocument.After };
                                        var closedAlert = await _alertsCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                                        if (closedAlert != null)
                                        {
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", closedAlert, stoppingToken);
                                            _logger.LogInformation($"[RESOLVED] {deviceId} — температура стабілізувалася.");
                                        }
                                    }

                                    break;
                                }

                            case "pump":
                                {
                                    double rollingAvgOutletPressure = rollingMetrics["rollingAvgOutletPressure"].AsDouble;

                                    if (rollingAvgOutletPressure < 5.15)
                                    {
                                        var existingAlert = await _alertsCollection.Find(a => a.DeviceId == "pump_01" && a.IsActive == true).FirstOrDefaultAsync(stoppingToken);
                                        if (existingAlert == null)
                                        {
                                            var alert = new DeviceAlert
                                            {
                                                DeviceId = "pump_01",
                                                Message = $"Падіння тиску на виході насоса! Поточний тиск: {rollingAvgOutletPressure:F2} бар (Норма: 5.4). Перевірте привод двигуна.",
                                                Severity = "Warning",
                                                Timestamp = now,
                                                IsActive = true
                                            };
                                            await _alertsCollection.InsertOneAsync(alert, cancellationToken: stoppingToken);
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert, stoppingToken);
                                            _logger.LogWarning($"[ALERT] pump_01 — зафіксовано низький тиск виходу: {rollingAvgOutletPressure} bar");
                                        }
                                    }
                                    else
                                    {
                                        var filter = Builders<DeviceAlert>.Filter.Eq(a => a.DeviceId, "pump_01") & Builders<DeviceAlert>.Filter.Eq(a => a.IsActive, true);
                                        var update = Builders<DeviceAlert>.Update.Set(a => a.IsActive, false);
                                        var options = new FindOneAndUpdateOptions<DeviceAlert> { ReturnDocument = ReturnDocument.After };
                                        var closedAlert = await _alertsCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                                        if (closedAlert != null)
                                        {
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", closedAlert, stoppingToken);
                                            _logger.LogInformation($"[RESOLVED] pump_01 — нормалізація. Алерт закрито.");
                                        }
                                    }

                                    break;
                                }

                            case "inverter":
                                {
                                    double rollingAvgTemp = rollingMetrics["rollingAvgTemp"].AsDouble;

                                    if (rollingAvgTemp > 45.0)
                                    {
                                        var existingAlert = await _alertsCollection.Find(a => a.DeviceId == "inv_solar_02" && a.IsActive == true).FirstOrDefaultAsync(stoppingToken);
                                        if (existingAlert == null)
                                        {
                                            var alert = new DeviceAlert
                                            {
                                                DeviceId = "inv_solar_02",
                                                Message = $"Увага! Перегрів інвертора. Температура: {rollingAvgTemp:F2}°C",
                                                Severity = "Critical",
                                                Timestamp = now,
                                                IsActive = true
                                            };
                                            await _alertsCollection.InsertOneAsync(alert, cancellationToken: stoppingToken);
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert, stoppingToken);
                                            _logger.LogWarning($"[ALERT] inv_solar_02 перегрівся: {rollingAvgTemp}°C.");
                                        }
                                    }
                                    else
                                    {
                                        var filter = Builders<DeviceAlert>.Filter.Eq(a => a.DeviceId, "inv_solar_02") & Builders<DeviceAlert>.Filter.Eq(a => a.IsActive, true);
                                        var update = Builders<DeviceAlert>.Update.Set(a => a.IsActive, false);
                                        var options = new FindOneAndUpdateOptions<DeviceAlert> { ReturnDocument = ReturnDocument.After };
                                        var closedAlert = await _alertsCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                                        if (closedAlert != null)
                                        {
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", closedAlert, stoppingToken);
                                            _logger.LogInformation($"[RESOLVED] inv_solar_02 — нормалізація. Алерт закрито.");
                                        }
                                    }

                                    break;
                                }

                            case "bess":
                                {
                                    double rollingAvgSoc = rollingMetrics["rollingAvgSoc"].AsDouble;

                                    if (rollingAvgSoc <= 15.0)
                                    {
                                        var existingAlert = await _alertsCollection.Find(a => a.DeviceId == "bess_cell_03" && a.IsActive == true).FirstOrDefaultAsync(stoppingToken);
                                        if (existingAlert == null)
                                        {
                                            var alert = new DeviceAlert
                                            {
                                                DeviceId = "bess_cell_03",
                                                Message = $"Критичний рівень розряду батареї BESS! Поточний заряд: {rollingAvgSoc:F2}%. Необхідне термінове живлення від мережі.",
                                                Severity = "Critical",
                                                Timestamp = now,
                                                IsActive = true
                                            };
                                            await _alertsCollection.InsertOneAsync(alert, cancellationToken: stoppingToken);
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert, stoppingToken);
                                            _logger.LogError($"[CRITICAL ALERT] bess_cell_03 майже розряджена! SoC: {rollingAvgSoc}%");
                                        }
                                    }
                                    else
                                    {
                                        var filter = Builders<DeviceAlert>.Filter.Eq(a => a.DeviceId, "bess_cell_03") & Builders<DeviceAlert>.Filter.Eq(a => a.IsActive, true);
                                        var update = Builders<DeviceAlert>.Update.Set(a => a.IsActive, false);
                                        var options = new FindOneAndUpdateOptions<DeviceAlert> { ReturnDocument = ReturnDocument.After };
                                        var closedAlert = await _alertsCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                                        if (closedAlert != null)
                                        {
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", closedAlert, stoppingToken);
                                            _logger.LogInformation($"[RESOLVED] bess_cell_03 — нормалізація. Алерт закрито.");
                                        }
                                    }

                                    break;
                                }

                            case "transformer":
                                {
                                    double rollingAvgLoadPercentage = rollingMetrics["rollingAvgLoadPercentage"].AsDouble;

                                    if (rollingAvgLoadPercentage > 90)
                                    {
                                        var existingAlert = await _alertsCollection.Find(a => a.DeviceId == "transformer_04" && a.IsActive == true).FirstOrDefaultAsync(stoppingToken);
                                        if (existingAlert == null)
                                        {
                                            var alert = new DeviceAlert
                                            {
                                                DeviceId = "transformer_04",
                                                Message = $"Критичне навантаження трансформатора: {rollingAvgLoadPercentage:F2}%!",
                                                Severity = "Warning",
                                                Timestamp = now,
                                                IsActive = true
                                            };
                                            await _alertsCollection.InsertOneAsync(alert, cancellationToken: stoppingToken);
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", alert, stoppingToken);
                                            _logger.LogWarning($"[ALERT] transformer_04 перевантажений! Записано в базу.");
                                        }
                                    }
                                    else
                                    {
                                        var filter = Builders<DeviceAlert>.Filter.Eq(a => a.DeviceId, "transformer_04") & Builders<DeviceAlert>.Filter.Eq(a => a.IsActive, true);
                                        var update = Builders<DeviceAlert>.Update.Set(a => a.IsActive, false);
                                        var options = new FindOneAndUpdateOptions<DeviceAlert> { ReturnDocument = ReturnDocument.After };
                                        var closedAlert = await _alertsCollection.FindOneAndUpdateAsync(filter, update, options, stoppingToken);
                                        if (closedAlert != null)
                                        {
                                            await _hubContext.Clients.All.SendAsync("ReceiveAlert", closedAlert, stoppingToken);
                                            _logger.LogInformation($"[RESOLVED] transformer_04 — нормалізація. Алерт закрито.");
                                        }
                                    }

                                    break;
                                }

                            default:
                                _logger.LogWarning($"Невідомий тип пристрою: {type}");
                                break;
                        }
                    }

                    var devicesPayload = aggregatedDocs.Select(res =>
                    {
                        // Весь документ лежить всередині "latest"
                        var latestDoc = res["latest"].AsBsonDocument;
                        var metrics = latestDoc["metrics"].AsBsonDocument;
                        var rollingMetrics = latestDoc["rollingMetrics"].AsBsonDocument;

                        return new
                        {
                            DeviceId = latestDoc["deviceId"].AsString,
                            Type = latestDoc["deviceType"].AsString,
                            Location = latestDoc["location"].AsString,
                            Timestamp = latestDoc["timestamp"].AsBsonDateTime.ToUniversalTime(),

                            // Сирі 15-хвилинні значення 
                            Temperature = metrics.GetDoubleMetric("temperature"),
                            OilTemperature = metrics.GetDoubleMetric("oil_temperature"),
                            Vibration = metrics.GetDoubleMetric("vibration"),
                            Rpm = metrics.GetIntMetric("rpm"),
                            Current = metrics.GetDoubleMetric("current"),
                            FlowRate = metrics.GetDoubleMetric("flow_rate"),
                            InletPressure = metrics.GetDoubleMetric("inlet_pressure"),
                            OutletPressure = metrics.GetDoubleMetric("outlet_pressure"),
                            CavitationIndex = metrics.GetDoubleMetric("cavitation_index"),
                            VoltageDc = metrics.GetDoubleMetric("voltage_dc"),
                            CurrentDc = metrics.GetDoubleMetric("current_dc"),
                            PowerAc = metrics.GetDoubleMetric("power_ac"),
                            Soc = metrics.GetDoubleMetric("soc"),
                            BessVoltage = metrics.GetDoubleMetric("bess_voltage"),
                            BessCurrent = metrics.GetDoubleMetric("bess_current"),
                            LoadPercentage = metrics.GetIntMetric("load_percentage"),

                            // Обчислені MongoDB аналітичні ковзні метрики 
                            RollingAvgTemp = rollingMetrics.GetDoubleMetric("rollingAvgTemp"),
                            RollingAvgOilTemp = rollingMetrics.GetDoubleMetric("rollingAvgOilTemp"),
                            RollingMaxVibration = rollingMetrics.GetDoubleMetric("rollingMaxVibration"),
                            RollingMaxCavitation = rollingMetrics.GetDoubleMetric("rollingMaxCavitation"),
                            RollingAvgRpm = rollingMetrics.GetDoubleMetric("rollingAvgRpm"),
                            RollingAvgFlowRate = rollingMetrics.GetDoubleMetric("rollingAvgFlowRate"),
                            RollingAvgInletPressure = rollingMetrics.GetDoubleMetric("rollingAvgInletPressure"),
                            RollingAvgOutletPressure = rollingMetrics.GetDoubleMetric("rollingAvgOutletPressure"),
                            RollingAvgCurrent = rollingMetrics.GetDoubleMetric("rollingAvgCurrent"),
                            RollingAvgVoltageDc = rollingMetrics.GetDoubleMetric("rollingAvgVoltageDc"),
                            RollingAvgCurrentDc = rollingMetrics.GetDoubleMetric("rollingAvgCurrentDc"),
                            RollingAvgPowerAc = rollingMetrics.GetDoubleMetric("rollingAvgPowerAc"),
                            RollingAvgSoc = rollingMetrics.GetDoubleMetric("rollingAvgSoc"),
                            RollingAvgBessVoltage = rollingMetrics.GetDoubleMetric("rollingAvgBessVoltage"),
                            RollingAvgBessCurrent = rollingMetrics.GetDoubleMetric("rollingAvgBessCurrent"),
                            RollingAvgLoadPercentage = rollingMetrics.GetDoubleMetric("rollingAvgLoadPercentage")
                        };
                    }).ToArray();

                    var telemetryPayload = new
                    {
                        Timestamp = now,
                        Devices = devicesPayload
                    };

                    // На фронтенд через SignalR
                    await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", telemetryPayload, cancellationToken: stoppingToken);
                    await Task.Delay(1000, stoppingToken);

                }
                catch (Exception ex)
                {
                    _logger.LogError($"Помилка симулятора: {ex.Message}");
                }

                // Пауза 1 секунду
                await Task.Delay(1000, cancellationToken: stoppingToken);
            }
        }
    }
}