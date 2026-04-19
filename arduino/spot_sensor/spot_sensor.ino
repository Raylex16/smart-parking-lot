// Smart Parking Lot — Sketch bidireccional V1
// Inbound: EVT:SENSOR:IR1:<0|1>
// Outbound recibido: CMD:ACT:LED1:SET:<0|1>
// Ack: ACK:<actuatorId> / NACK:<actuatorId>:<reason>

const int IR1_PIN = 7;
const int LED1_PIN = 13;
int lastIR1 = -1;
String inBuf = "";

void setup() {
  pinMode(IR1_PIN, INPUT);
  pinMode(LED1_PIN, OUTPUT);
  Serial.begin(9600);
}

void loop() {
  int raw = digitalRead(IR1_PIN);
  // Sensor IR activo en LOW: LOW = ocupado (1)
  int occupied = (raw == LOW) ? 1 : 0;
  if (occupied != lastIR1) {
    lastIR1 = occupied;
    Serial.print("EVT:SENSOR:IR1:");
    Serial.println(occupied);
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

  if (actId == "LED1" && action == "SET") {
    digitalWrite(LED1_PIN, payload == "1" ? HIGH : LOW);
    Serial.print("ACK:"); Serial.println(actId);
  } else {
    Serial.print("NACK:"); Serial.print(actId); Serial.println(":unsupported");
  }
}
