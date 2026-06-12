import React, { useEffect, useState, useRef } from 'react';
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts';
import GaugeComponent from 'react-gauge-component';
import { Activity, AlertTriangle, CheckCircle, ShieldAlert, Clock, Gauge, Cpu } from 'lucide-react';
import '../styles/_mainPage.css';

//Початковий стан пристроїв
const INITIAL_DEVICES = {
    motor_01: { deviceId: 'motor_01', name: 'Асинхронний Двигун', type: 'motor', location: 'Sector_A', metrics: {}, hours: 0, status: 'Normal' },
    pump_01: { deviceId: 'pump_01', name: 'Магістральний Насос', type: 'pump', location: 'Sector_A', metrics: {}, hours: 0, status: 'Normal' },
    inv_solar_02: { deviceId: 'inv_solar_02', name: 'Інвертор СЕС', type: 'inverter', location: 'Solar_Field_1', metrics: {}, hours: 0, status: 'Normal' },
    bess_cell_03: { deviceId: 'bess_cell_03', name: 'Комірка BESS', type: 'bess', location: 'Storage_Room_B', metrics: {}, hours: 0, status: 'Normal' },
    transformer_04: { deviceId: 'transformer_04', name: 'Трансформатор', type: 'transformer', location: 'Main_Substation', metrics: {}, hours: 0, status: 'Normal' },
};

export default function MainPage({ connection }) {
    // Стан пристроїв
    const [devices, setDevices] = useState(INITIAL_DEVICES);
    // Стан алертів
    const [alerts, setAlerts] = useState([]);
    // Графіки (точки) 
    const [chartData, setChartData] = useState({
        motor_01: [], pump_01: [], inv_solar_02: [], bess_cell_03: [], transformer_04: []
    });
    // Активне вікно
    const [activeTab, setActiveTab] = useState('motor_01');

    // МЕТРИКИ КОЖНОГО ПРИСТРОЮ ЗА ТИПОМ ПРИСТРОЮ
    const extractMetrics = (device) => {
        switch (device.type) {
            case 'motor':
                return {
                    temperature: device.temperature,
                    vibration: device.vibration,
                    rpm: device.rpm,
                    current: device.current,
                };
            case 'pump':
                return {
                    flow_rate: device.flowRate,
                    inlet_pressure: device.inletPressure,
                    outlet_pressure: device.outletPressure,
                    cavitation_index: device.cavitationIndex,
                };
            case 'inverter':
                return {
                    voltage_dc: device.voltageDc,
                    power_ac: device.powerAc,
                    current_dc: device.currentDc,
                    temperature: device.temperature,
                };
            case 'bess':
                return {
                    soc: device.soc,
                    voltage: device.bessVoltage,
                    current: device.bessCurrent,
                    temperature: device.temperature,
                };
            case 'transformer':
                return {
                    oil_temperature: device.oilTemperature,
                    vibration: device.vibration,
                    load_percentage: device.loadPercentage,
                };
            default:
                return {};
        }
    };

    // Історія точок для графіків 
    const chartDataRef = useRef({
        motor_01: [],
        pump_01: [],
        inv_solar_02: [],
        bess_cell_03: [],
        transformer_04: []
    });

    // Функція для визначення кольору статусу пристрою
    const getStatusColor = (status) => {
        switch (status) {
            case 'Critical': return 'bg-red-900/40 border-red-500 text-red-400 animate-pulse';
            case 'Warning': return 'bg-amber-900/40 border-amber-500 text-amber-400';
            default: return 'bg-slate-800/60 border-emerald-500 text-emerald-400';
        }
    };

    // КОНФІГУРАЦІЯ МЕТРИК ДЛЯ СІТКИ (ЗА ТИПОМ ПРИСТРОЮ)
    const DEVICE_METRICS_CONFIG = {
        motor: [
            { key: 'temperature', label: 'Температура', unit: '°C', className: 'font-bold text-slate-100' },
            { key: 'vibration', label: 'Вібрація', unit: 'мм/с', className: 'text-slate-100' },
            { key: 'rpm', label: 'Оберти', unit: 'rpm', className: 'text-slate-100' },
            { key: 'current', label: 'Струм', unit: 'A', className: 'text-slate-100' }
        ],
        pump: [
            { key: 'flow_rate', label: 'Потік', unit: 'м³/г', className: 'font-bold text-slate-100' },
            { key: 'inlet_pressure', label: 'Тиск на вході', unit: 'бар', className: 'text-slate-100' },
            { key: 'outlet_pressure', label: 'Тиск на виході', unit: 'бар', className: 'text-slate-100' },
            { key: 'cavitation_index', label: 'Кавітація', unit: '', className: 'text-slate-100' }
        ],
        inverter: [
            { key: 'voltage_dc', label: 'Напруга постійного струму', unit: 'V', className: 'text-slate-100' },
            { key: 'current_dc', label: 'Потужність змінного струму', unit: 'A', className: 'text-slate-100' },
            { key: 'power_ac', label: 'Потужність змінного струму', unit: 'кВт', className: 'font-bold text-slate-100' },
            { key: 'temperature', label: 'Температура', unit: '°C', className: 'text-slate-100' }
        ],
        bess: [
            { key: 'soc', label: 'Заряд (SoC)', unit: '%', className: 'font-bold text-blue-400' },
            { key: 'voltage', label: 'Напруга', unit: 'V', className: 'text-slate-100' },
            { key: 'current', label: 'Струм', unit: 'A', className: 'text-slate-100' },
            { key: 'temperature', label: 'Температура', unit: '°C', className: 'text-slate-100' }
        ],
        transformer: [
            { key: 'oil_temperature', label: 'Темп Масла', unit: '°C', className: 'text-slate-100' },
            { key: 'vibration', label: 'Вібрація', unit: 'мм/с', className: 'text-slate-100' },
            { key: 'load_percentage', label: 'Навантаження', unit: '%', className: 'font-bold text-orange-400' }
        ]
    };

    // КОНФІГУРАЦІЯ ГЕЙДЖІВ
    const gaudgeConfigs = {
        motor_01: [
            { key: "temperature", name: "Температура", color: "#f64141", min: 0, max: 120 }, // Cyber Red
            { key: "vibration", name: "Вібрація", color: "#fbbf24", min: 0, max: 100 },      // Glowing Amber
            { key: "rpm", name: "Хвилинна частота обертання", color: "#00d2ff", min: 0, max: 3000 }, // Electric Cyan
            { key: "current", name: "Струм", color: "#10b981", min: 0, max: 100 }          // Neon Emerald
        ],

        pump_01: [
            { key: "flow_rate", name: "Потік", color: "#00d2ff", min: 0, max: 60 },
            { key: "inlet_pressure", name: "Тиск на вході", color: "#f64141", min: 0, max: 10 },
            { key: "outlet_pressure", name: "Тиск на виході", color: "#fbbf24", min: 0, max: 10 },
            { key: "cavitation_index", name: "Індекс кавітації", color: "#a855f7", min: 0, max: 1 } // Cyber Purple
        ],

        inv_solar_02: [
            { key: "voltage_dc", name: "Напруга постійного струму", color: "#00d2ff", min: 0, max: 650 },
            { key: "power_ac", name: "Потужність змінного струму", color: "#10b981", min: 0, max: 25 },
            { key: "current_dc", name: "Струм постійного струму", color: "#fbbf24", min: 0, max: 50 },
            { key: "temperature", name: "Температура", color: "#f64141", min: 0, max: 120 }
        ],

        bess_cell_03: [
            { key: "soc", name: "Заряд (SoC)", color: "#00d2ff", min: 0, max: 100 },
            { key: "voltage", name: "Напруга", color: "#a855f7", min: -500, max: 500 },
            { key: "current", name: "Струм", color: "#fbbf24", min: -100, max: 100 },
            { key: "temperature", name: "Температура", color: "#f64141", min: 0, max: 80 }
        ],

        transformer_04: [
            { key: "oil_temperature", name: "Температура масла", color: "#f64141", min: 0, max: 120 },
            { key: "vibration", name: "Вібрація", color: "#fbbf24", min: 0, max: 100 },
            { key: "load_percentage", name: "Навантаження", color: "#00d2ff", min: 0, max: 100 }
        ]
    };

    // КОНФІГУРАЦІЯ ГРАФІКІВ
    const chartConfigs = {
        motor_01: [
            { key: "temperature", name: "Температура", color: "#f64141" },
            { key: "vibration", name: "Вібрація", color: "#fbbf24" },
            { key: "rpm", name: "Хвилинна частота обертання", color: "#00d2ff" },
            { key: "current", name: "Струм", color: "#10b981" }
        ],
        pump_01: [
            { key: "flow_rate", name: "Потік", color: "#00d2ff" },
            { key: "inlet_pressure", name: "Тиск на вході", color: "#f64141" },
            { key: "outlet_pressure", name: "Тиск на виході", color: "#fbbf24" },
            { key: "cavitation_index", name: "Індекс кавітації", color: "#a855f7" }
        ],
        inv_solar_02: [
            { key: "voltage_dc", name: "Напруга постійного струму", color: "#00d2ff" },
            { key: "power_ac", name: "Потужність змінного струму", color: "#10b981" },
            { key: "current_dc", name: "Струм постійного струму", color: "#fbbf24" },
            { key: "temperature", name: "Температура", color: "#f64141" }
        ],
        bess_cell_03: [
            { key: "soc", name: "Заряд", color: "#00d2ff" },
            { key: "voltage", name: "Напруга", color: "#a855f7" },
            { key: "current", name: "Струм", color: "#fbbf24" },
            { key: "temperature", name: "Температура", color: "#f64141" }
        ],
        transformer_04: [
            { key: "oil_temperature", name: "Температура масла", color: "#f64141" },
            { key: "vibration", name: "Вібрація", color: "#fbbf24" },
            { key: "load_percentage", name: "Навантаження", color: "#00d2ff" }
        ]
    };

    useEffect(() => {
        if (!connection) return;

        // ТЕЛЕМЕТРІЯ
        const handleTelemetry = (payload) => {
            console.log(payload);
            const timeLabel = new Date().toLocaleTimeString();
            payload.devices.forEach(device => {
                // Заповнюємо точки замінюючи нові дані
                chartDataRef.current[device.deviceId] = [
                    ...(chartDataRef.current[device.deviceId] || []),
                    {
                        time: timeLabel,
                        ...extractMetrics(device)
                    }
                ].slice(-20);
            });


            setDevices(prev => {
                const next = { ...prev };
                payload.devices.forEach(device => {
                    next[device.deviceId] = {
                        ...next[device.deviceId],
                        metrics: extractMetrics(device),
                        lastSeen: timeLabel
                    };
                });
                return next;
            });


            setChartData({
                ...chartDataRef.current
            });
        };

        // АЛЕРТИ
        const handleAlert = (alert) => {
            console.log(alert);
            setAlerts(prev => {
                if (alert.isActive) {
                    return [
                        // Додаємо новий алерт наперед
                        alert,
                        ...prev.filter(
                            a =>
                                !(a.deviceId === alert.deviceId)
                        )
                    ];
                }
                return prev.filter(
                    // Фільтруємо без неактивного алерту
                    a => a.deviceId !== alert.deviceId
                );
            });

            setDevices(prev => {
                if (!prev[alert.deviceId])
                    return prev;
                return {
                    ...prev,
                    [alert.deviceId]: {
                        ...prev[alert.deviceId],
                        status: alert.isActive
                            ? alert.severity
                            : "Normal"
                    }
                }
            });
        };

        // МОТОГОДИНИ
        const handleHours = (data) => {
            setDevices(prev => {
                if (!prev[data.deviceId])
                    return prev;
                return {
                    ...prev,
                    [data.deviceId]: {
                        ...prev[data.deviceId],
                        hours: data.totalOperatingHours
                    }
                };
            });
        };

        connection.on(
            "ReceiveTelemetry",
            handleTelemetry
        );
        connection.on(
            "ReceiveAlert",
            handleAlert
        );
        connection.on(
            "ReceiveOperatingHours",
            handleHours
        );

        return () => {
            connection.off(
                "ReceiveTelemetry",
                handleTelemetry
            );
            connection.off(
                "ReceiveAlert",
                handleAlert
            );
            connection.off(
                "ReceiveOperatingHours",
                handleHours
            );
        };

        console.log(chartData[activeTab]);
    }, [connection]);


    // ОБСЛОГОВУВАННЯ ---------------------------
    const predictByHours = (hours, limit) => {
        if (hours == null){
            return "--";
        }

        // Час до ТО - різниця між поточними годинами напрацювання та лімітом
        const remaining = limit - hours;
        if (remaining <= 0)
            return "Потрібне ТО";

        if (remaining < 50)
            return `До ТО: ${remaining.toFixed(1)} м-год (критично)`;

        return `До ТО: ${remaining.toFixed(1)} м-год`;
    };

    const predictInverter = (temperature, current) => {
        if (temperature == null || current == null) {
            return "--";
        }

        // Вагові коефіціенти для рухвання зносу (температура впливає на 60%, а струм на 40%)
        // Температура поділена на 120 оскільки беремо частину складового діапазону
        let wear = (temperature / 50) * 0.6 + (current / 35) * 0.4;
        // Щоб вага не перевищувала 100% (1)
        wear = Math.min(wear, 1);
        // Залишок без зносу і є залишковим здоров'ям
        const health = (100 - wear * 100).toFixed(1);
        const months = Math.round(health / 10);
        return `ТО через ~${months} міс.`;
    };

    const predictBess = (soc, temperature) => {
        if (soc == null || temperature == null) {
            return "--";
        }

        // Вагові коефіціенти для рухвання зносу
        let wear = ((100 - soc) / 100) * 0.6 + (temperature / 36) * 0.4;
        // Щоб вага не перевищувала 100% (1)
        wear = Math.min(wear, 1);
        // Залишок без зносу і є залишковим здоров'ям
        const health = (100 - wear * 100).toFixed(1);
        const cyclesLeft = Math.round(health * 50);
        return `Залишок ресурсу: ${cyclesLeft} циклів`;
    };

    const predictTransformer = (oilTemperature, loadPercentage, vibration) => {
        if (oilTemperature == null || loadPercentage == null || vibration == null) {
            return "--";
        }

        // Вагові коефіціенти для рухвання зносу
        let wear = (oilTemperature / 51) * 0.5 + (loadPercentage / 95) * 0.3 + (vibration / 3) * 0.2;
        // Щоб вага не перевищувала 100% (1)
        wear = Math.min(wear, 1);
        // Залишок без зносу і є залишковим здоров'ям
        const health = (100 - wear * 100).toFixed(1);
        const months = Math.round(health / 8);
        return `ТО через ~${months} міс.`;
    };

    const getServicePrediction = (device) => {
        switch (device.type) {
            case "motor":
                return predictByHours(device.hours, 1000);

            case "pump":
                return predictByHours(device.hours, 800);

            case "inverter":
                return predictInverter(
                    device.metrics.temperature,
                    device.metrics.current_dc
                );

            case "transformer":
                return predictTransformer(
                    device.metrics.oil_temperature,
                    device.metrics.load_percentage,
                    device.metrics.vibration
                );

            case "bess":
                return predictBess(
                    device.metrics.soc,
                    device.metrics.temperature
                );

            default:
                return "--";
        }
    };

    return (
        <div className="main-page">

            {/* ВЕРХНЯ ПАНЕЛЬ */}
            <div className="top-panel">
                <h2 className="devices_grid-title">
                    Технологічна сітка агрегатів
                </h2>

                <div className="devices_grid-container">
                    {Object.values(devices).map((dev) => (
                        <div
                            key={dev.deviceId}
                            onClick={() => setActiveTab(dev.deviceId)}
                            className={`devices_grid-card ${activeTab === dev.deviceId ? "active" : ""}`}
                        >
                            <div className="devices_grid-card-name">
                                {dev.name}
                            </div>

                            <div className="devices_grid-card-status">
                                Стан:

                                <p className={`devices_grid-card-status_info status-${dev.status.toLowerCase()}`}>
                                    {dev.status}
                                </p>
                            </div>

                            <div className="devices_grid-card-metric">
                                {DEVICE_METRICS_CONFIG[dev.type]?.map(metric => (
                                    <div key={metric.key}>
                                        {metric.label}:{" "}
                                        <span>
                                            {dev.metrics?.[metric.key] ?? "--"} {metric.unit}
                                        </span>
                                    </div>
                                ))}
                            </div>

                            {(dev.type === "motor" || dev.type === "pump") && (
                                <div className="devices_grid-card-operaring-hours">
                                    <span>
                                        <Clock size={12} />
                                        {" "}
                                        {(dev.hours ?? 0).toFixed(2)} м-год
                                    </span>
                                </div>
                            )}

                            <div className="devices_grid-card-TO">
                                <Clock size={12} />
                                {" "}
                                {getServicePrediction(dev)}
                            </div>
                        </div>
                    ))}
                </div>
            </div>

            {/* НИЖНЯ ПАНЕЛЬ */}
            <div className="dashboard-bottom">
                {/* ГЕЙДЖІ */}
                <div className="gauges-panel">
                    {chartData[activeTab]?.length === 0 ? (
                        <div className="devices_visual_gaudge-info_wait">
                            Очікування даних...
                        </div>
                    ) : (
                        gaudgeConfigs[activeTab]?.map(metric => (
                            <div
                                key={metric.key}
                                className="devices_visual_gaudge-item"
                            >
                                <GaugeComponent
                                    value={devices[activeTab]?.metrics?.[metric.key] ?? 0}
                                    minValue={metric.min}
                                    maxValue={metric.max}
                                    arc={{
                                        width: 0.15,
                                        padding: 0.02,
                                        cornerRounding: 3,
                                        subArcs: [
                                            {
                                                limit: metric.max,
                                                color: metric.color
                                            }
                                        ]
                                    }}
                                    style={{
                                        width: "100%",
                                        height: "90px"
                                    }}
                                    labels={{
                                        valueLabel: {
                                            style: {
                                                fill: "#f8fafc",
                                                fontSize: 25
                                            }
                                        },
                                        tickLabels: {
                                            type: "inner",
                                            defaultTickValueConfig: {
                                                style: {
                                                    fill: "#64748b",
                                                    fontSize: 5
                                                }
                                            }
                                        }
                                    }}
                                />
                                <p
                                    className="devices_visual_gaudge-info"
                                    style={{ color: metric.color }}
                                >
                                    {metric.name}
                                </p>
                            </div>
                        ))
                    )}
                </div>

                {/* ГРАФІК */}
                <div className="chart-panel">
                    <h3 className="devices_visual-title">
                        {devices[activeTab]?.name}
                    </h3>

                    {!chartData[activeTab] || chartData[activeTab].length === 0 ? (
                        <div className="devices_visual_chart-wait">
                            Очікування пакетів даних...
                        </div>
                    ) : (
                        <ResponsiveContainer width="100%" height={250}>
                            <LineChart
                                data={chartData[activeTab]}
                                margin={{
                                    top: 30,
                                    right: 20,
                                    left: 0,
                                    bottom: 0
                                }}
                            >
                                <CartesianGrid
                                    strokeDasharray="4 4"
                                    vertical={false}
                                />

                                <XAxis
                                    dataKey="time"
                                    tickLine={false}
                                    angle={-30}
                                    textAnchor="end"
                                    height={50}
                                />

                                <YAxis
                                    tickLine={false}
                                    axisLine={false}
                                />

                                <Legend
                                    verticalAlign="bottom"
                                    iconType="circle"
                                    iconSize={10}
                                />

                                {chartConfigs[activeTab]?.map(metric => (
                                    <Line
                                        key={metric.key}
                                        dataKey={metric.key}
                                        name={metric.name}
                                        stroke={metric.color}
                                        strokeWidth={2.5}
                                        dot={true}
                                        activeDot={{
                                            r: 5,
                                            stroke: metric.color
                                        }}
                                    />
                                ))}
                            </LineChart>
                        </ResponsiveContainer>
                    )}
                </div>

                {/* АЛЕРТИ */}
                <div className="alerts-panel">
                    <h2 className="alerts-title">
                        <ShieldAlert size={18} />
                        Журнал подій
                    </h2>

                    <div className="alerts-list">
                        {alerts.length === 0 ? (
                            <div className="alerts-empty">
                                Аномалій не зафіксовано
                            </div>
                        ) : (
                            alerts.map((alert, idx) => (
                                <div
                                    key={idx}
                                    className={
                                        alert.severity === "Critical"
                                            ? "alert-critical"
                                            : "alert-warning"
                                    }
                                >
                                    <div className="alert-header">
                                        <span className="alert-severity">
                                            [{alert.severity}]
                                        </span>

                                        <span className="alert-time">
                                            {new Date(alert.timestamp).toLocaleTimeString()}
                                        </span>
                                    </div>

                                    <div className="alert-message">
                                        {alert.message}
                                    </div>

                                    <div className="alert-device">
                                        ID: {alert.deviceId}
                                    </div>
                                </div>
                            ))
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
}