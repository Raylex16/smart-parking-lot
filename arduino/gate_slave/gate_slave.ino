// Esclavo I2C que controla los servos de las puertas (entrada y salida).
// El master (Mega) le envia comandos "GATE<n>:ANGLE:<deg>\n" via I2C.
// Tambien acepta los mismos comandos por Serial USB para test sin master.
//
// Cableado:
//   Pin A4 (SDA) <- SDA del Mega (pin 20)
//   Pin A5 (SCL) <- SCL del Mega (pin 21)
//   GND          <- GND comun con el Mega (obligatorio)
//   Pin 9        -> Senal Servo GATE1 (entrada)
//   Pin 10       -> Senal Servo GATE2 (salida)
//   Vcc/GND servos: fuente externa propia del Uno, GND comun con todo.

#include <Wire.h>
#include <Servo.h>

const uint8_t I2C_ADDR    = 0x08;
const int     GATE_COUNT  = 2;
const int     SERVO_PINS[GATE_COUNT] = { 9, 10 };
const int     INIT_ANGLE  = 0;
const int     RX_BUF_SIZE = 32;

Servo servos[GATE_COUNT];
char rxBuf[RX_BUF_SIZE];
volatile int rxLen = 0;
volatile bool messagePending = false;

String serialBuf = "";

void setup() {
  Serial.begin(9600);

  for (int i = 0; i < GATE_COUNT; i++) {
    servos[i].attach(SERVO_PINS[i]);
    servos[i].write(INIT_ANGLE);
  }

  Wire.begin(I2C_ADDR);
  Wire.onReceive(onI2CReceive);

  Serial.print("Gate Slave listo. I2C addr 0x");
  Serial.println(I2C_ADDR, HEX);
  Serial.println("Servos: GATE1 -> pin 9, GATE2 -> pin 10");
  Serial.println("Comandos por Serial (test): GATE1:ANGLE:90 | GATE2:ANGLE:0");
}

void loop() {
  if (messagePending) {
    noInterrupts();
    char local[RX_BUF_SIZE];
    int n = rxLen;
    memcpy(local, rxBuf, n);
    local[n] = '\0';
    messagePending = false;
    interrupts();

    handleCommand(String(local));
  }

  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\n') {
      handleCommand(serialBuf);
      serialBuf = "";
    } else if (c != '\r') {
      serialBuf += c;
    }
  }
}

void onI2CReceive(int howMany) {
  int n = 0;
  while (Wire.available() && n < RX_BUF_SIZE - 1) {
    char c = (char)Wire.read();
    if (c == '\n' || c == '\r') break;
    rxBuf[n++] = c;
  }
  rxLen = n;
  messagePending = (n > 0);
}

void handleCommand(String cmd) {
  cmd.trim();
  if (cmd.length() == 0) return;

  int firstSep = cmd.indexOf(':');
  if (firstSep < 0) {
    Serial.print("Comando invalido: ");
    Serial.println(cmd);
    return;
  }

  String target = cmd.substring(0, firstSep);
  String rest   = cmd.substring(firstSep + 1);

  int secondSep = rest.indexOf(':');
  if (secondSep < 0) {
    Serial.print("Falta payload: ");
    Serial.println(cmd);
    return;
  }

  String action  = rest.substring(0, secondSep);
  String payload = rest.substring(secondSep + 1);

  int gateIndex = -1;
  for (int i = 0; i < GATE_COUNT; i++) {
    String gateId = "GATE" + String(i + 1);
    if (target.equalsIgnoreCase(gateId)) {
      gateIndex = i;
      break;
    }
  }

  if (gateIndex < 0) {
    Serial.print("Target desconocido: ");
    Serial.println(target);
    return;
  }

  if (action.equalsIgnoreCase("ANGLE")) {
    int angle = payload.toInt();
    if (angle < 0)   angle = 0;
    if (angle > 180) angle = 180;
    servos[gateIndex].write(angle);
    Serial.print("Servo ");
    Serial.print(target);
    Serial.print(" -> ");
    Serial.print(angle);
    Serial.println(" grados");
    return;
  }

  Serial.print("Action no soportada: ");
  Serial.println(action);
}
