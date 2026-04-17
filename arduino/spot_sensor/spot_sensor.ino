// Smart Parking Lot — Sensor IR de Spot
// Protocolo serial: SENSOR_ID:VALOR (ej. "IR1:1")
// VALOR: 1 = ocupado (objeto detectado), 0 = libre

const int IR1_PIN = 7;
const int LED_PIN = 13;

void setup() {
  pinMode(IR1_PIN, INPUT);
  pinMode(LED_PIN, OUTPUT);
  Serial.begin(9600);
}

void loop() {
  int value1 = digitalRead(IR1_PIN);

  // Sensor IR activo en LOW: LOW = objeto detectado = ocupado (1)
  bool occupied1 = (value1 == LOW);

  Serial.print("IR1:");
  Serial.println(occupied1 ? "1" : "0");

  // LED indicador: encendido si cualquier sensor detecta
  digitalWrite(LED_PIN, occupied1 ? HIGH : LOW);

  delay(2000);
}
