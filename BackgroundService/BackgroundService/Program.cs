using BackgroundService.Models; // Підключаємо папку з нашими моделями
using BackgroundService.Services; // Підключаємо папку з нашими сервісами   
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);

// Налаштування бази даних
var mongoClient = new MongoClient("mongodb://localhost:27017");
var database = mongoClient.GetDatabase("DigitalTwinDb");
builder.Services.AddSingleton(database);

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRCorsPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Реєстрація SignalR для готовності використання в додатку
builder.Services.AddSignalR();

// Реєстрація симулятора телеметрії
builder.Services.AddHostedService<TelemetrySimulatorService>();

var app = builder.Build();

app.UseRouting();

app.UseCors("SignalRCorsPolicy");

// Реєстрація маршруту для SignalR хаба
app.MapHub<TelemetryHubSignalR>("/rtime/telemetry");

// Отримати останню точку телеметрії
app.MapGet("/api/devices/{deviceId}/latest", async (string deviceId, IMongoDatabase db) =>
{
    var collection = db.GetCollection<BsonDocument>("telemetry");

    // Формуємо пайплайн агрегації спеціально для ОДНОГО пристрою
    var pipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[]
    {
        BsonDocument.Parse($"{{ $match: {{ 'metadata.deviceId': '{deviceId}', timestamp: {{ $gte: {{ $subtract: [ '$$NOW', 21600000 ] }} }} }} }}"),

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

        BsonDocument.Parse("{ $sort: { '_id.timeBucket': 1 } }"),

        BsonDocument.Parse(@"
        {
            $setWindowFields: {
                partitionBy: '$_id.deviceId',
                sortBy: { '_id.timeBucket': 1 },
                output: {
                    rollingAvgTemp: { $avg: '$avg_temperature', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgOilTemp: { $avg: '$avg_oil_temperature', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingMaxVibration: { $max: '$max_vibration', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingMaxCavitation: { $max: '$max_cavitation_index', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgRpm: { $avg: '$avg_rpm', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgFlowRate: { $avg: '$avg_flow_rate', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgInletPressure: { $avg: '$avg_inlet_pressure', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgOutletPressure: { $avg: '$avg_outlet_pressure', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgCurrent: { $avg: '$avg_current', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgVoltageDc: { $avg: '$avg_voltage_dc', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgCurrentDc: { $avg: '$avg_current_dc', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgPowerAc: { $avg: '$avg_power_ac', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgSoc: { $avg: '$avg_soc', window: { range: [-30, 'current'], unit: 'minute' } },
                    rollingAvgLoadPercentage: { $avg: '$avg_load_percentage', window: { range: [-30, 'current'], unit: 'minute' } }
                }
            }
        }"),

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
                    rollingAvgLoadPercentage: { $round: ['$rollingAvgLoadPercentage', 0] }
                }
            }
        }"),

        BsonDocument.Parse("{ $group: { _id: '$deviceId', latest: { $last: '$$ROOT' } } }")
    });

    var cursor = await collection.AggregateAsync(pipeline);
    var rootResult = await cursor.FirstOrDefaultAsync();

    if (rootResult == null || !rootResult.Contains("latest"))
    {
        return Results.NotFound(new { message = $"Дані для пристрою {deviceId} відсутні за обраний період." });
    }

    // Оскільки ми згрупували через $last: '$$ROOT', реальні дані лежать всередині поля "latest"
    var latest = rootResult["latest"].AsBsonDocument;
    var metrics = latest.Contains("metrics") ? latest["metrics"].AsBsonDocument : new BsonDocument();
    var rollingMetrics = latest.Contains("rollingMetrics") ? latest["rollingMetrics"].AsBsonDocument : new BsonDocument();

    // Допоміжні локальні функції для безпечного читання числових значень
    double GetDoubleSafe(BsonDocument doc, string key) =>
        doc.Contains(key) && !doc[key].IsBsonNull ? doc[key].ToDouble() : 0.0;

    int GetIntSafe(BsonDocument doc, string key) =>
        doc.Contains(key) && !doc[key].IsBsonNull ? doc[key].ToInt32() : 0;

    var payload = new
    {
        DeviceId = latest.Contains("deviceId") ? latest["deviceId"].AsString : deviceId,
        DeviceType = latest.Contains("deviceType") ? latest["deviceType"].AsString : "Unknown",
        Location = latest.Contains("location") ? latest["location"].AsString : "Unknown",
        Timestamp = latest.Contains("timestamp") ? latest["timestamp"].AsBsonDateTime.ToUniversalTime() : DateTime.UtcNow,

        Metrics = new
        {
            Temperature = GetDoubleSafe(metrics, "temperature"),
            OilTemperature = GetDoubleSafe(metrics, "oil_temperature"),
            Vibration = GetDoubleSafe(metrics, "vibration"),
            Rpm = GetIntSafe(metrics, "rpm"),
            Current = GetDoubleSafe(metrics, "current"),
            FlowRate = GetDoubleSafe(metrics, "flow_rate"),
            InletPressure = GetDoubleSafe(metrics, "inlet_pressure"),
            OutletPressure = GetDoubleSafe(metrics, "outlet_pressure"),
            CavitationIndex = GetDoubleSafe(metrics, "cavitation_index"),
            VoltageDc = GetDoubleSafe(metrics, "voltage_dc"),
            CurrentDc = GetDoubleSafe(metrics, "current_dc"),
            PowerAc = GetDoubleSafe(metrics, "power_ac"),
            Soc = GetDoubleSafe(metrics, "soc"),
            LoadPercentage = GetIntSafe(metrics, "load_percentage")
        },
        RollingMetrics = new
        {
            RollingAvgTemp = GetDoubleSafe(rollingMetrics, "rollingAvgTemp"),
            RollingAvgOilTemp = GetDoubleSafe(rollingMetrics, "rollingAvgOilTemp"),
            RollingMaxVibration = GetDoubleSafe(rollingMetrics, "rollingMaxVibration"),
            RollingMaxCavitation = GetDoubleSafe(rollingMetrics, "rollingMaxCavitation"),
            RollingAvgRpm = GetDoubleSafe(rollingMetrics, "rollingAvgRpm"),
            RollingAvgFlowRate = GetDoubleSafe(rollingMetrics, "rollingAvgFlowRate"),
            RollingAvgInletPressure = GetDoubleSafe(rollingMetrics, "rollingAvgInletPressure"),
            RollingAvgOutletPressure = GetDoubleSafe(rollingMetrics, "rollingAvgOutletPressure"),
            RollingAvgCurrent = GetDoubleSafe(rollingMetrics, "rollingAvgCurrent"),
            RollingAvgVoltageDc = GetDoubleSafe(rollingMetrics, "rollingAvgVoltageDc"),
            RollingAvgCurrentDc = GetDoubleSafe(rollingMetrics, "rollingAvgCurrentDc"),
            RollingAvgPowerAc = GetDoubleSafe(rollingMetrics, "rollingAvgPowerAc"),
            RollingAvgSoc = GetDoubleSafe(rollingMetrics, "rollingAvgSoc"),
            RollingAvgLoadPercentage = GetDoubleSafe(rollingMetrics, "rollingAvgLoadPercentage") // $round повертає double, тому читаємо як double
        }
    };

    return Results.Ok(payload);
});

// Отримати критичні алерти
app.MapGet("/api/alerts", async (IMongoDatabase db) =>
{
    var collection = db.GetCollection<DeviceAlert>("alerts");
    var activeAlerts = await collection.Find(x => x.IsActive).ToListAsync();
    return Results.Ok(activeAlerts);
});

app.Run();