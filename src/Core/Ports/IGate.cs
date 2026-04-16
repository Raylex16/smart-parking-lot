namespace SmartParkingLot.Core.Ports;

// SOLID - Dependency Inversion Principle (DIP): Application trabaja con esta
// abstracción para controlar puertas, mientras Hardware provee la implementación
// concreta (Gate). Esto garantiza que Application y Hardware no se conozcan entre sí.
public interface IGate
{
    /// <summary>Abre la puerta (ángulo máximo).</summary>
    void Open();

    /// <summary>Cierra la puerta (ángulo mínimo).</summary>
    void Close();

    /// <summary>Retorna verdadero si la puerta está abierta.</summary>
    bool GetState();
}
