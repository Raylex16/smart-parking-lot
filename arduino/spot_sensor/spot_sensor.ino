// Master del Smart Parking Lot.
// Maneja sensores IR (spots y puertas), LEDs, LCD I2C y servo de la puerta de entrada.
// El servo de la puerta de salida (GATE2) lo opera un Uno esclavo via I2C.
//
// I2C bus (pins 20 SDA / 21 SCL en el Mega):
//   0x27 -> LCD 16x2
//   0x08 -> Uno esclavo (controla servo GATE2)

#include <Servo.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

const int SENSOR_COUNT = 3;
const int GATE_COUNT   = 2;

const int IR_PINS[SENSOR_COUNT]  = { 7, 6, 5 };
const int LED_PINS[SENSOR_COUNT] = { 13, 12, 11 };

const int GATE_IR_PINS[GATE_COUNT] = { 4, 3 };

// Para cada gate: si LOCAL_PIN >= 0, el servo esta conectado al Mega en ese pin.
// Si SLAVE_ADDR > 0, el comando se reenvia por I2C a ese esclavo.
// Solo uno de los dos debe estar definido por gate.
const int     GATE_LOCAL_PIN[GATE_COUNT]  = { -1,   -1   };
const uint8_t GATE_SLAVE_ADDR[GATE_COUNT] = { 0x08, 0x08 };
const int     GATE_CLOSED_ANGLE = 0;

const uint8_t LCD_I2C_ADDR    = 0x27;
const uint8_t LCD_COLS         = 16;
const uint8_t LCD_ROWS         = 2;
const unsigned long MSG_DURATION_MS = 3000;

int lastState[SENSOR_COUNT]   = { -1, -1, -1 };
int lastGateState[GATE_COUNT] = { -1, -1 };
Servo localGates[GATE_COUNT];
LiquidCrystal_I2C lcd(LCD_I2C_ADDR, LCD_COLS, LCD_ROWS);

int cachedAvailable = 0;
int cachedTotal     = 0;
unsigned long messageEndsAt = 0;

String inBuf = "";

void setup() {
  for (int i = 0; i < SENSOR_COUNT; i++) {
    pinMode(IR_PINS[i], INPUT);
    pinMode(LED_PINS[i], OUTPUT);
    digitalWrite(LED_PINS[i], HIGH);
  }

  for (int i = 0; i < GATE_COUNT; i++) {
    pinMode(GATE_IR_PINS[i], INPUT);
    if (GATE_LOCAL_PIN[i] >= 0) {
      localGates[i].attach(GATE_LOCAL_PIN[i]);
      localGates[i].write(GATE_CLOSED_ANGLE);
    }
  }

  Wire.begin();

  lcd.init();
  lcd.backlight();
  showIdle();

  Serial.begin(9600);
}

void loop() {
  for (int i = 0; i < SENSOR_COUNT; i++) {
    int raw = digitalRead(IR_PINS[i]);
    int occupied = (raw == LOW) ? 1 : 0;
    if (occupied != lastState[i]) {
      lastState[i] = occupied;
      Serial.print("EVT:SENSOR:IR");
      Serial.print(i + 1);
      Serial.print(":");
      Serial.println(occupied);
    }
  }

  for (int i = 0; i < GATE_COUNT; i++) {
    int raw = digitalRead(GATE_IR_PINS[i]);
    int present = (raw == LOW) ? 1 : 0;
    if (present != lastGateState[i]) {
      lastGateState[i] = present;
      Serial.print("EVT:SENSOR:GATE-IR");
      Serial.print(i + 1);
      Serial.print(":");
      Serial.println(present);
    }
  }

  if (messageEndsAt > 0 && millis() >= messageEndsAt) {
    messageEndsAt = 0;
    showIdle();
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

  for (int i = 0; i < SENSOR_COUNT; i++) {
    String ledId = "LED" + String(i + 1);
    if (actId == ledId && action == "SET") {
      digitalWrite(LED_PINS[i], payload == "1" ? HIGH : LOW);
      Serial.print("ACK:"); Serial.println(actId);
      return;
    }
  }

  for (int i = 0; i < GATE_COUNT; i++) {
    String gateId = "GATE" + String(i + 1);
    if (actId == gateId && action == "ANGLE") {
      int angle = clampAngle(payload.toInt());

      if (GATE_LOCAL_PIN[i] >= 0) {
        localGates[i].write(angle);
        Serial.print("ACK:"); Serial.println(actId);
        return;
      }

      if (GATE_SLAVE_ADDR[i] > 0) {
        bool ok = forwardAngleToSlave(GATE_SLAVE_ADDR[i], i, angle);
        if (ok) { Serial.print("ACK:"); Serial.println(actId); }
        else    { Serial.print("NACK:"); Serial.print(actId); Serial.println(":i2c-fail"); }
        return;
      }

      Serial.print("NACK:"); Serial.print(actId); Serial.println(":no-target");
      return;
    }
  }

  if (actId == "LCD") {
    if (action == "STATUS") {
      int sep = payload.indexOf(':');
      if (sep < 0) { Serial.println("NACK:LCD:status-format"); return; }
      cachedAvailable = payload.substring(0, sep).toInt();
      cachedTotal     = payload.substring(sep + 1).toInt();
      if (messageEndsAt == 0) showIdle();
      Serial.println("ACK:LCD");
      return;
    }
    if (action == "MSG") {
      showMessage(payload);
      messageEndsAt = millis() + MSG_DURATION_MS;
      Serial.println("ACK:LCD");
      return;
    }
  }

  Serial.print("NACK:"); Serial.print(actId); Serial.println(":unsupported");
}

int clampAngle(int angle) {
  if (angle < 0)   return 0;
  if (angle > 180) return 180;
  return angle;
}

bool forwardAngleToSlave(uint8_t addr, int gateIndex, int angle) {
  Wire.beginTransmission(addr);
  Wire.print("GATE");
  Wire.print(gateIndex + 1);
  Wire.print(":ANGLE:");
  Wire.print(angle);
  Wire.write('\n');
  uint8_t status = Wire.endTransmission();
  return status == 0;
}

void showIdle() {
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Smart Parking");
  lcd.setCursor(0, 1);
  lcd.print("Libres: ");
  lcd.print(cachedAvailable);
  lcd.print("/");
  lcd.print(cachedTotal);
}

void showMessage(const String& text) {
  String top = text;
  if (top.length() > LCD_COLS) top = top.substring(0, LCD_COLS);
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print(top);
  lcd.setCursor(0, 1);
  lcd.print("Libres: ");
  lcd.print(cachedAvailable);
  lcd.print("/");
  lcd.print(cachedTotal);
}
