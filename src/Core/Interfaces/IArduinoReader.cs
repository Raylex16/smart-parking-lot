namespace SmartParkingLot.Core.Interfaces;

// SOLID - DIP: abstrae el bridge serial para que Application/Cli no dependan del concreto ArduinoSerialBridge.
public interface IArduinoReader : IDisposable
{
    /// <summary>Abre el puerto e inicia el loop de lectura en background.</summary>
    void StartListening();

    /// <summary>Detiene el loop y cierra el puerto.</summary>
    void StopListening();

    /// <summary>True si el loop de lectura está activo.</summary>
    bool IsListening { get; }

    /// <summary>Controla si los mensajes del bridge se imprimen en consola.</summary>
    bool ConsoleLoggingEnabled { get; set; }
}
