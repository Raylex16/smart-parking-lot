// Escaner I2C. Subir al Mega (master) con el Uno (slave) ya conectado al bus.
// Lista todas las direcciones que responden. Util para diagnosticar problemas
// de bus, pull-ups, GND comun, o pines mal conectados.
//
// Esperado en este proyecto:
//   0x08 -> Uno esclavo (gate_slave)
//   0x27 -> LCD I2C  (si esta conectado)

#include <Wire.h>

void setup() {
  Wire.begin();
  Serial.begin(9600);
  while (!Serial) {}
  Serial.println("\n=== I2C Scanner ===");
}

void loop() {
  Serial.println("Escaneando bus...");
  int found = 0;

  for (uint8_t addr = 1; addr < 127; addr++) {
    Wire.beginTransmission(addr);
    uint8_t status = Wire.endTransmission();

    if (status == 0) {
      Serial.print("  Encontrado dispositivo en 0x");
      if (addr < 16) Serial.print('0');
      Serial.println(addr, HEX);
      found++;
    }
  }

  if (found == 0) {
    Serial.println("  *** Ningun dispositivo respondio ***");
    Serial.println("  Posibles causas:");
    Serial.println("    - SDA/SCL en pines incorrectos (Mega: 20/21, NO A4/A5)");
    Serial.println("    - GND no comun entre placas");
    Serial.println("    - Faltan resistencias pull-up en el bus");
  } else {
    Serial.print("  Total: ");
    Serial.print(found);
    Serial.println(" dispositivo(s)");
  }

  Serial.println();
  delay(3000);
}
