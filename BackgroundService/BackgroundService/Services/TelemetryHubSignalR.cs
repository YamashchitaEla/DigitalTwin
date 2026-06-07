using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BackgroundService.Services
{
    //SignalR Хаб — це центральна точка, яка є "мостом" між сервером і клієнтами. Він дозволяє серверу надсилати повідомлення всім підключеним клієнтам або окремим групам клієнтів.
    public class TelemetryHubSignalR : Hub
    {
        // В якості унікального імені/типу
    }
}
