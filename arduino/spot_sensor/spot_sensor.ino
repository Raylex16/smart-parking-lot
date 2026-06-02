// Master del Smart Parking Lot.
// Maneja sensores IR (spots y puertas), LEDs, LCD I2C y servo de la puerta de entrada.
// El servo de la puerta de salida (GATE2) lo opera un Uno esclavo via I2C.
// Soporte de cámara OV7670 por puerta para reconocimiento de placas (QQVGA grayscale, base64).
//
// I2C bus Wire  (pins 20 SDA / 21 SCL): LCD 16x2 (0x27), Uno esclavo (0x08)
// I2C bus Wire1 (pins 70 SDA1/ 71 SCL1): OV7670 SCCB (0x21)

#include <Servo.h>
#include <Wire.h>
#include <LiquidCrystal_I2C.h>

// ── Sensores y LEDs de spots ───────────────────────────────────────────────
const int SENSOR_COUNT = 3;
const int GATE_COUNT   = 2;

const int IR_PINS[SENSOR_COUNT]  = { 7, 6, 5 };
const int LED_PINS[SENSOR_COUNT] = { 13, 12, 11 };
const int GATE_IR_PINS[GATE_COUNT] = { 4, 3 };

const int     GATE_LOCAL_PIN[GATE_COUNT]  = { -1,   -1   };
const uint8_t GATE_SLAVE_ADDR[GATE_COUNT] = { 0x08, 0x08 };
const int     GATE_CLOSED_ANGLE = 0;

// ── LCD ────────────────────────────────────────────────────────────────────
const uint8_t LCD_I2C_ADDR    = 0x27;
const uint8_t LCD_COLS         = 16;
const uint8_t LCD_ROWS         = 2;
const unsigned long MSG_DURATION_MS = 3000;

// ── OV7670 ────────────────────────────────────────────────────────────────
// Pines de señal (ver docs/hardware-wiring.md para diagrama completo)
#define OV_VSYNC  2   // PE4 — frame sync
#define OV_HREF   3   // PE5 — line active
#define OV_PCLK   4   // PG5 — pixel clock
#define OV_XCLK   11  // PB5 / OC1A — master clock 8 MHz
#define OV_RESET  32  // PC5 — activo bajo
#define OV_PWDN   33  // PC4 — activo alto = apagado

// Dirección 7-bit SCCB (0x42>>1)
#define OV7670_ADDR 0x21

#define QQVGA_W     160
#define QQVGA_H     120
#define QQVGA_BYTES (QQVGA_W * QQVGA_H)   // 19 200 bytes por frame

// Registros mínimos para QQVGA YUV422 (YUYV). Ajustar según muestra física.
// Sentinel de fin: {0xFF, 0xFF}
static const uint8_t OV7670_QQVGA_YUV[][2] = {
    {0x12, 0x80},  // COM7: soft reset (auto-clears, esperar 100 ms)
    {0x11, 0x01},  // CLKRC: sin dividir el clock externo
    {0x12, 0x00},  // COM7: YUV output
    {0x0C, 0x04},  // COM3: scale enable
    {0x3E, 0x19},  // COM14: manual scaling, PCLK /4
    {0x70, 0x3A},  // SCALING_XSC
    {0x71, 0x35},  // SCALING_YSC
    {0x72, 0x11},  // SCALING_DCWCTR: H/2 V/2 → QQVGA
    {0x73, 0xF1},  // SCALING_PCLK_DIV: /2
    {0xA2, 0x02},  // SCALING_PCLK_DELAY
    {0x15, 0x00},  // COM10: polaridades normales
    {0x17, 0x16},  // HSTART
    {0x18, 0x04},  // HSTOP
    {0x19, 0x02},  // VSTART
    {0x1A, 0x7A},  // VSTOP
    {0x32, 0x80},  // HREF edge offset
    {0x03, 0x0A},  // VREF
    {0x3A, 0x04},  // TSLB: YUYV byte order
    {0x40, 0xD0},  // COM15: full output range
    {0xFF, 0xFF},  // fin
};

// ── Estado global ─────────────────────────────────────────────────────────
int lastState[SENSOR_COUNT]   = { -1, -1, -1 };
int lastGateState[GATE_COUNT] = { -1, -1 };
Servo localGates[GATE_COUNT];
LiquidCrystal_I2C lcd(LCD_I2C_ADDR, LCD_COLS, LCD_ROWS);

int cachedAvailable = 0;
int cachedTotal     = 0;
unsigned long messageEndsAt = 0;

String inBuf = "";

// ── setup ─────────────────────────────────────────────────────────────────
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

  // OV7670 control pins
  pinMode(OV_RESET, OUTPUT);
  pinMode(OV_PWDN,  OUTPUT);
  digitalWrite(OV_PWDN,  LOW);   // sensor encendido
  digitalWrite(OV_RESET, LOW);
  delay(10);
  digitalWrite(OV_RESET, HIGH);  // liberar reset

  // Pines de datos PA0–PA7 como entrada (DDRA = 0)
  DDRA = 0x00;

  setupXCLK();  // Timer1 → 8 MHz en OC1A (pin 11)

  Wire.begin();
  Wire1.begin();  // para SCCB del OV7670

  lcd.init();
  lcd.backlight();
  showIdle();

  delay(100);
  initOV7670();

  Serial.begin(115200);
}

// ── loop ──────────────────────────────────────────────────────────────────
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

// ── handleCommand ─────────────────────────────────────────────────────────
void handleCommand(const String& line) {
  // Captura de cámara: CMD:CAM:CAPTURE:{gateId}
  if (line.startsWith("CMD:CAM:CAPTURE:")) {
    String gateId = line.substring(16);
    captureAndSendFrame(gateId);
    return;
  }

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

// ── Funciones auxiliares existentes ───────────────────────────────────────
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

// ── OV7670 — XCLK via Timer1 CTC a 8 MHz ─────────────────────────────────
void setupXCLK() {
  // Timer1 CTC: toggle OC1A en cada compare match
  // f = F_CPU / (2 * N * (OCR1A + 1)) = 16MHz / (2*1*(0+1)) = 8 MHz
  TCCR1A = _BV(COM1A0);              // Toggle OC1A on compare match
  TCCR1B = _BV(WGM12) | _BV(CS10); // CTC mode, prescaler = 1
  OCR1A  = 0;
  pinMode(OV_XCLK, OUTPUT);
}

// ── OV7670 — SCCB (Wire1) ─────────────────────────────────────────────────
void writeOV7670Reg(uint8_t reg, uint8_t val) {
  Wire1.beginTransmission(OV7670_ADDR);
  Wire1.write(reg);
  Wire1.write(val);
  Wire1.endTransmission();
  delayMicroseconds(100);
}

void initOV7670() {
  for (int i = 0; ; i++) {
    uint8_t reg = OV7670_QQVGA_YUV[i][0];
    uint8_t val = OV7670_QQVGA_YUV[i][1];
    if (reg == 0xFF && val == 0xFF) break;
    writeOV7670Reg(reg, val);
    // Pausa extra tras reset software
    if (reg == 0x12 && val == 0x80) delay(100);
  }
}

// ── OV7670 — Captura y envío base64 ──────────────────────────────────────
//
// El frame OV7670 sale en formato YUV422 (YUYV):
//   byte 0 = Y0, byte 1 = U, byte 2 = Y1, byte 3 = V, ...
// Solo se transmite el canal Y (grayscale): bytes pares → 19 200 bytes.
//
// Se usa un buffer pequeño de 48 bytes (múltiplo de 3 → 64 chars base64)
// para no superar los 8 KB de SRAM del Mega.
//
// Lectura con acceso directo a puertos para respetar el timing del PCLK.
//   VSYNC → PE4 (pin 2)   HREF → PE5 (pin 3)   PCLK → PG5 (pin 4)
//   Data  → PINA (pins 22–29 = PA0–PA7)

static const char B64_TABLE[] =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

void encodeBase64Chunk(const uint8_t* data, int len, char* out) {
  int j = 0;
  for (int i = 0; i < len; i += 3) {
    uint8_t a = data[i];
    uint8_t b = (i + 1 < len) ? data[i + 1] : 0;
    uint8_t c = (i + 2 < len) ? data[i + 2] : 0;
    out[j++] = B64_TABLE[a >> 2];
    out[j++] = B64_TABLE[((a & 0x03) << 4) | (b >> 4)];
    out[j++] = (i + 1 < len) ? B64_TABLE[((b & 0x0F) << 2) | (c >> 6)] : '=';
    out[j++] = (i + 2 < len) ? B64_TABLE[c & 0x3F] : '=';
  }
  out[j] = '\0';
}

void captureAndSendFrame(const String& gateId) {
  const int BUF = 48;   // múltiplo de 3 → 64 chars base64 por línea
  uint8_t   buf[BUF];
  char      b64[69];    // 64 chars + null + margen
  int       bufIdx = 0;

  Serial.print("CAM:BEGIN:");
  Serial.print(QQVGA_BYTES);
  Serial.print(":");
  Serial.println(gateId);

  // Esperar inicio de frame: VSYNC HIGH → LOW → HIGH
  while (  PINE & 0x10);  // esperar VSYNC LOW
  while (!(PINE & 0x10)); // esperar VSYNC HIGH (frame activo)

  for (int row = 0; row < QQVGA_H; row++) {
    while (!(PINE & 0x20));  // esperar HREF HIGH (línea activa)

    for (int col = 0; col < QQVGA_W * 2; col++) {
      // Esperar flanco de subida de PCLK
      while (  PING & 0x20);   // PCLK LOW
      while (!(PING & 0x20));  // PCLK HIGH → dato válido en PINA

      if ((col & 1) == 0) {
        // Byte par = Y (luminancia)
        buf[bufIdx++] = PINA;
        if (bufIdx == BUF) {
          encodeBase64Chunk(buf, BUF, b64);
          Serial.print("CAM:DATA:");
          Serial.println(b64);
          bufIdx = 0;
        }
      }
    }

    while (PINE & 0x20);  // esperar HREF LOW (fin de línea)
  }

  // Flush bytes restantes
  if (bufIdx > 0) {
    encodeBase64Chunk(buf, bufIdx, b64);
    Serial.print("CAM:DATA:");
    Serial.println(b64);
  }

  Serial.println("CAM:END");
}
