// Smart Parking Lot — Sketch bidireccional V2 (3 sensores IR)
// Inbound:  EVT:SENSOR:IR<1|2|3>:<0|1>
// Outbound: CMD:ACT:LED<1|2|3>:SET:<0|1>
// Ack:      ACK:<actuatorId> / NACK:<actuatorId>:<reason>

const int SENSOR_COUNT = 3;

const int IR_PINS[SENSOR_COUNT]  = { 7, 6, 5 };   // IR1=spot1, IR2=spot2, IR3=spot3
const int LED_PINS[SENSOR_COUNT] = { 13, 12, 11 }; // LED1, LED2, LED3

int lastState[SENSOR_COUNT] = { -1, -1, -1 };
String inBuf = "";

void setup() {
  for (int i = 0; i < SENSOR_COUNT; i++) {
    pinMode(IR_PINS[i], INPUT);
    pinMode(LED_PINS[i], OUTPUT);
  }
  Serial.begin(9600);
}

void loop() {
  for (int i = 0; i < SENSOR_COUNT; i++) {
    int raw = digitalRead(IR_PINS[i]);
    // Sensor IR activo en LOW: LOW = ocupado (1)
    int occupied = (raw == LOW) ? 1 : 0;
    if (occupied != lastState[i]) {
      lastState[i] = occupied;
      Serial.print("EVT:SENSOR:IR");
      Serial.print(i + 1);
      Serial.print(":");
      Serial.println(occupied);
    }
  }

  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n') { handleCommand(inBuf); inBuf = ""; }
    else if (c != '\r') inBuf += c;
  }
  delay(20);
}

void handleCommand(const String& line) {
  if (!line.startsWith("CMD:ACT:")) return;

  int idEnd = line.indexOf(':', 8);
  if (idEnd < 0) { Serial.println("NACK:?:malformed"); return; }
  String actId = line.substring(8, idEnd);

  int actionEnd = line.indexOf(':', idEnd + 1);
  if (actionEnd < 0) { Serial.print("NACK:"); Serial.print(actId); Serial.println(":no-action"); return; }
  String action = line.substring(idEnd + 1, actionEnd);
  String payload = line.substring(actionEnd + 1);

  // Resuelve LED1/LED2/LED3 -> índice de array
  for (int i = 0; i < SENSOR_COUNT; i++) {
    String ledId = "LED" + String(i + 1);
    if (actId == ledId && action == "SET") {
      digitalWrite(LED_PINS[i], payload == "1" ? HIGH : LOW);
      Serial.print("ACK:"); Serial.println(actId);
      return;
    }
  }

  Serial.print("NACK:"); Serial.print(actId); Serial.println(":unsupported");
}
