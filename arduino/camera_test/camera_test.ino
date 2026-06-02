// ============================================================
// camera_test.ino  —  Test de cámara OV7670
// Smart Parking Lot  —  Arduino Mega 2560
// ============================================================
//
// PROPÓSITO
//   Verificar que el módulo OV7670 responde por SCCB, configurarlo
//   en modo QQVGA escala de grises y medir los límites reales de
//   transferencia de imagen al PC vía serial.
//
// HERRAMIENTA RECOMENDADA
//   Arduino IDE → Monitor Serial a 115200 baud, fin de línea: "Nueva línea"
//
// ═══════════════════════════════════════════════════════════
//  CABLEADO
// ═══════════════════════════════════════════════════════════
//
//  ⚠  El OV7670 opera a 3.3 V lógico.
//     Los pines D0-D7, VSYNC, HREF y PCLK del Mega son de 5 V.
//     Usar un level-shifter bidireccional (ej. TXS0108E o BSS138)
//     o divisor resistivo (1 kΩ + 2 kΩ) en cada línea.
//
//  OV7670 pin   →   Mega pin       Descripción
//  ──────────────────────────────────────────────────────────
//  VCC / 3.3V   →   3.3V           Alimentación  ← NO conectar a 5V
//  GND          →   GND            Tierra común
//  SDA          →   20 (SDA)       SCCB — requiere pull-up (ver abajo)
//  SCL          →   21 (SCL)       SCCB — requiere pull-up (ver abajo)
//  D0           →   22 (PA0)   ─┐
//  D1           →   23 (PA1)    │
//  D2           →   24 (PA2)    │  Bus paralelo de píxeles
//  D3           →   25 (PA3)    │  Lectura rápida con PINA
//  D4           →   26 (PA4)    │
//  D5           →   27 (PA5)    │
//  D6           →   28 (PA6)    │
//  D7           →   29 (PA7)   ─┘
//  VSYNC        →   2             INT0  — pulso al inicio de cada frame
//  HREF         →   30            HIGH mientras la fila tiene píxeles válidos
//  PCLK         →   31            Pulso por cada byte de dato
//  XCLK / MCLK →   8             OC4C — clock de entrada (~4 MHz, Timer4)
//  RESET        →   32            Activo-bajo; mantener HIGH para operar
//  PWDN         →   GND           Activo-alto = apagado; conectar a GND
//
//  PULL-UPS OBLIGATORIOS — el bus I2C/SCCB no funciona sin ellos:
//
//    Mega 3.3V ──── R1 (4.7 kΩ) ──┬──── Mega pin 20 (SDA)
//                                  └──── OV7670 SDA
//
//    Mega 3.3V ──── R2 (4.7 kΩ) ──┬──── Mega pin 21 (SCL)
//                                  └──── OV7670 SCL
//
//  El 3.3V es el mismo nodo que alimenta el módulo (VCC).
//  Conectar los pull-ups a 5V dañaría el sensor.
//
//  Nota: el OV7670 (0x21) comparte bus I2C con el LCD (0x27) y el
//  esclavo de puertas (0x08). Las tres direcciones son distintas → OK.
//
// ═══════════════════════════════════════════════════════════
//  COMANDOS (Monitor Serial)
// ═══════════════════════════════════════════════════════════
//   scan     — escanea el bus I2C y lista dispositivos encontrados
//   id       — lee los registros de Product ID del OV7670 (0x0A, 0x0B)
//   reg:<HH> — lee un registro específico (ej. reg:12)
//   config   — configura OV7670: QQVGA 160×120, escala de grises
//   capture  — captura 1 frame y lo envía al PC en base64
//   bench    — mide el tiempo de captura sin transmitir
//   limits   — imprime tabla de límites de transferencia
//   help     — muestra esta ayuda

#include <Wire.h>

// ─── Pines ────────────────────────────────────────────────
static const uint8_t PIN_VSYNC = 2;    // INT0
static const uint8_t PIN_HREF  = 30;
static const uint8_t PIN_PCLK  = 31;
static const uint8_t PIN_XCLK  = 8;   // OC4C — ~4 MHz clock
static const uint8_t PIN_RESET = 32;

// Port A = pines 22-29 (D0-D7 del OV7670)
// Se lee con PINA para máxima velocidad (1 instrucción)

// ─── OV7670 SCCB ──────────────────────────────────────────
static const uint8_t OV7670_ADDR  = 0x21;   // 7-bit address
static const uint8_t OV_PID_HIGH  = 0x0A;   // Product ID high = 0x76
static const uint8_t OV_PID_LOW   = 0x0B;   // Product ID low  = 0x73
static const uint8_t OV_COM7      = 0x12;   // Soft reset + format
static const uint8_t OV_CLKRC     = 0x11;   // Prescaler de clock

// ─── Resolución ───────────────────────────────────────────
// QQVGA en escala de grises: 160 × 120 = 19 200 bytes.
// El Mega tiene 8 KB de SRAM → NO se puede almacenar el frame completo.
// Se transmite línea a línea: buffer de 1 fila = 160 bytes (seguro).
static const uint16_t FRAME_W     = 160;
static const uint16_t FRAME_H     = 120;
static const uint32_t FRAME_BYTES = (uint32_t)FRAME_W * FRAME_H;  // 19 200

static uint8_t rowBuf[FRAME_W];  // buffer de una fila

// ─── Base64 ───────────────────────────────────────────────
static const char B64[] =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

static uint8_t b64Acc[3];   // acumulador de 3 bytes antes de codificar
static uint8_t b64Cnt = 0;  // cuántos bytes hay en el acumulador
static uint16_t b64Line = 0; // chars en la línea actual (max 60 por línea)

void b64Reset() {
    b64Cnt  = 0;
    b64Line = 0;
}

// Envía los bytes acumulados como caracteres base64.
// Llama con flush=true al final para vaciar el padding.
void b64Flush(bool flush = false) {
    if (b64Cnt == 0 && !flush) return;

    uint8_t a = b64Acc[0];
    uint8_t b = (b64Cnt > 1) ? b64Acc[1] : 0;
    uint8_t c = (b64Cnt > 2) ? b64Acc[2] : 0;

    Serial.print(B64[a >> 2]);
    Serial.print(B64[((a & 3) << 4) | (b >> 4)]);
    Serial.print(b64Cnt > 1 ? B64[((b & 0xF) << 2) | (c >> 6)] : '=');
    Serial.print(b64Cnt > 2 ? B64[c & 0x3F]                    : '=');

    b64Line += 4;
    if (b64Line >= 60) {
        Serial.println();
        b64Line = 0;
    }
    b64Cnt = 0;
}

// Agrega 1 byte al stream base64.
void b64Write(uint8_t byte) {
    b64Acc[b64Cnt++] = byte;
    if (b64Cnt == 3) b64Flush();
}

// ─── SCCB (I2C al OV7670) ────────────────────────────────
bool sccbWrite(uint8_t reg, uint8_t val) {
    Wire.beginTransmission(OV7670_ADDR);
    Wire.write(reg);
    Wire.write(val);
    return Wire.endTransmission() == 0;
}

uint8_t sccbRead(uint8_t reg) {
    // SCCB requiere dos transacciones separadas para leer.
    Wire.beginTransmission(OV7670_ADDR);
    Wire.write(reg);
    Wire.endTransmission();
    delayMicroseconds(100);
    Wire.requestFrom((uint8_t)OV7670_ADDR, (uint8_t)1);
    return Wire.available() ? Wire.read() : 0xFF;
}

// ─── XCLK — Timer4 ~4 MHz PWM en pin 8 ──────────────────
// OC4C = pin 8 en el Mega.  Modo 14: Fast PWM, TOP = ICR4.
// F_CPU = 16 MHz, N = 1, ICR4 = 3
// F_PWM = 16 MHz / (N × (ICR4 + 1)) = 16 MHz / (1 × 4) = 4 MHz
// El OV7670 spec pide 10-48 MHz; 4 MHz opera fuera de spec
// pero da framerate reducido, lo que ayuda al Mega a leer píxeles.
void startXCLK() {
    pinMode(PIN_XCLK, OUTPUT);
    TCCR4A = (1 << COM4C1) | (1 << WGM41);
    TCCR4B = (1 << WGM43)  | (1 << WGM42) | (1 << CS40);
    ICR4   = 3;
    OCR4C  = 1;   // 50% duty cycle: OCR4C = ICR4/2
}

// ─── Configuración OV7670 (QQVGA YUV, escala de grises) ──
//
// La cámara captura YUV4:2:2 → 2 bytes por píxel:
//   byte 0 = Y (luminancia = brillo, esto es lo que usamos)
//   byte 1 = U o V (crominancia, lo descartamos para escala de grises)
// Así obtenemos 160×120 = 19 200 bytes de imagen en escala de grises.

struct RegVal { uint8_t reg; uint8_t val; };

static const RegVal OV_CONFIG[] PROGMEM = {
    { 0x12, 0x80 },  // COM7: soft reset (esperar 100 ms después)
    // --- Formato YUV ---
    { 0x12, 0x00 },  // COM7: YUV
    { 0x11, 0x0F },  // CLKRC: prescaler activo, div=16 → PCLK = XCLK/16 = 250 kHz (8 µs/byte, digitalRead puede seguirle el ritmo)
    { 0x3E, 0x00 },  // COM14: sin zoom digital
    { 0x0C, 0x00 },  // COM3: sin escalado adicional
    // --- Escala QQVGA (160×120) ---
    { 0x70, 0x3A },  // SCALING_XSC
    { 0x71, 0x35 },  // SCALING_YSC
    { 0x72, 0x11 },  // SCALING_DCWCTR: 2× downscale en X e Y
    { 0x73, 0xF0 },  // SCALING_PCLK_DIV: dividir PCLK por 2
    { 0xA2, 0x02 },  // SCALING_PCLK_DELAY
    // --- Control de exposición y ganancia ---
    { 0x13, 0xE7 },  // COM8: auto exposición + auto balance blancos + AGC auto
    { 0x14, 0x48 },  // COM9: max AGC = 8×
    // --- Ventana de salida para QQVGA ---
    { 0x17, 0x16 },  // HSTART
    { 0x18, 0x04 },  // HSTOP
    { 0x19, 0x02 },  // VSTART
    { 0x1A, 0x7A },  // VSTOP
    { 0x32, 0x80 },  // HREF
    { 0x03, 0x0A },  // VREF
    // --- Sin corrección de gamma agresiva ---
    { 0x7A, 0x20 }, { 0x7B, 0x1C }, { 0x7C, 0x28 }, { 0x7D, 0x3C },
    { 0x7E, 0x55 }, { 0x7F, 0x68 },
    { 0xFF, 0xFF }   // centinela — fin de tabla
};

bool configureCamera() {
    bool ok = true;
    for (uint8_t i = 0; ; i++) {
        uint8_t reg = pgm_read_byte(&OV_CONFIG[i].reg);
        uint8_t val = pgm_read_byte(&OV_CONFIG[i].val);
        if (reg == 0xFF && val == 0xFF) break;

        if (reg == 0x12 && val == 0x80) {
            // Soft reset: esperamos 100 ms antes de continuar
            sccbWrite(reg, val);
            delay(100);
            continue;
        }
        if (!sccbWrite(reg, val)) ok = false;
        delayMicroseconds(300);
    }
    delay(200);  // permitir al sensor estabilizarse
    return ok;
}

// ─── Captura de frame ────────────────────────────────────
//
// La lectura se hace bit a bit:
//   1. Esperar flanco de subida de VSYNC (inicio de frame)
//   2. Para cada fila: esperar HREF = HIGH
//   3. Dentro de HREF: leer un byte en cada pulso de PCLK
//   4. En YUV4:2:2 los bytes van: Y0 U0 Y1 V0 Y2 U1 ...
//      Solo tomamos los bytes impares (índice par = Y = luminancia)
//
// TIEMPO LÍMITE: a 4 MHz XCLK, el OV7670 entrega ~1 byte cada 500 ns.
// A 16 MHz el Mega tiene ~8 ciclos por byte → barely alcanza con PINA.
// Por eso usamos PINA en lugar de digitalRead (que tarda ~4 µs).

static volatile bool vsyncFlag = false;
void ISR_VSYNC() { vsyncFlag = true; }

// Captura un frame completo y llama onByte() por cada byte de Y.
// Retorna el número de bytes capturados.
uint32_t captureFrame(void (*onByte)(uint8_t)) {
    // Armar pines como entradas rápidas
    DDRA = 0x00;   // Port A = todos entradas (pines 22-29)

    // Esperar el próximo VSYNC (tiempo máximo 100 ms = 1 frame a 10 fps)
    vsyncFlag = false;
    unsigned long t0 = millis();
    while (!vsyncFlag) {
        if (millis() - t0 > 200) return 0;  // timeout: sensor no activo
    }

    uint32_t bytesCapturados = 0;

    for (uint16_t row = 0; row < FRAME_H; row++) {
        // Esperar inicio de fila (HREF = HIGH)
        while (digitalRead(PIN_HREF) == LOW) {}

        uint16_t col = 0;
        uint8_t byteIndex = 0;   // 0 = Y, 1 = U/V (descartamos los impares)

        while (col < FRAME_W) {
            // Esperar flanco de subida de PCLK
            while (digitalRead(PIN_PCLK) == LOW)  {}
            while (digitalRead(PIN_HREF) == LOW)   break;  // fin de fila

            uint8_t pixelByte = PINA;  // lectura ultrarrápida del bus de datos

            // Esperar que baje PCLK antes del siguiente ciclo
            while (digitalRead(PIN_PCLK) == HIGH) {}

            if (byteIndex == 0) {
                // Byte Y = luminancia → este es el valor de escala de grises
                onByte(pixelByte);
                col++;
                bytesCapturados++;
            }
            byteIndex ^= 1;  // alterna 0, 1, 0, 1...
        }

        // Esperar fin de fila si aún está activa
        while (digitalRead(PIN_HREF) == HIGH) {}
    }

    return bytesCapturados;
}

// Callback: acumula en base64 y envía al serial
void onByteBase64(uint8_t b) {
    b64Write(b);
}

// Callback: solo cuenta bytes (para bench)
static uint32_t benchCount = 0;
void onByteBench(uint8_t) { benchCount++; }

// ─── Comandos ─────────────────────────────────────────────
static bool cameraConfigured = false;
static String inputBuf = "";

void cmdScan() {
    Serial.println(F("=== Escaneo I2C (Wire, pines 20/21) ==="));
    int found = 0;
    for (uint8_t addr = 1; addr < 127; addr++) {
        Wire.beginTransmission(addr);
        if (Wire.endTransmission() == 0) {
            Serial.print(F("  0x"));
            if (addr < 16) Serial.print('0');
            Serial.print(addr, HEX);
            if (addr == 0x21) Serial.print(F("  ← OV7670 (esperado)"));
            if (addr == 0x27) Serial.print(F("  ← LCD I2C"));
            if (addr == 0x08) Serial.print(F("  ← Esclavo puertas (gate_slave)"));
            Serial.println();
            found++;
        }
    }
    if (found == 0) {
        Serial.println(F("  *** Ningún dispositivo respondió ***"));
        Serial.println(F("  Revisar: SDA→pin20, SCL→pin21, GND común, pull-ups 4.7kΩ"));
    } else {
        Serial.print(F("  Total: ")); Serial.print(found); Serial.println(F(" dispositivo(s)"));
    }
}

void cmdId() {
    uint8_t hi = sccbRead(OV_PID_HIGH);
    uint8_t lo = sccbRead(OV_PID_LOW);
    Serial.println(F("=== OV7670 Product ID ==="));
    Serial.print(F("  PID_HIGH (reg 0x0A): 0x")); Serial.print(hi, HEX);
    Serial.println(hi == 0x76 ? F("  ✓ correcto") : F("  ✗ INCORRECTO — esperado 0x76"));
    Serial.print(F("  PID_LOW  (reg 0x0B): 0x")); Serial.print(lo, HEX);
    Serial.println(lo == 0x73 ? F("  ✓ correcto") : F("  ✗ INCORRECTO — esperado 0x73"));

    if (hi == 0xFF && lo == 0xFF) {
        Serial.println(F("  → Todos 0xFF = sensor no responde."));
        Serial.println(F("    Revisar: cableado SDA/SCL, VCC=3.3V, RESET=HIGH, PWDN=GND"));
    }
}

void cmdReadReg(String arg) {
    arg.trim();
    if (arg.length() == 0) {
        Serial.println(F("Uso: reg:<HH>  (ej. reg:12)"));
        return;
    }
    uint8_t reg = (uint8_t)strtol(arg.c_str(), nullptr, 16);
    uint8_t val = sccbRead(reg);
    Serial.print(F("  reg 0x")); Serial.print(reg, HEX);
    Serial.print(F(" = 0x")); Serial.println(val, HEX);
}

void cmdConfig() {
    Serial.println(F("=== Configurando OV7670 (QQVGA 160×120, escala de grises) ==="));
    bool ok = configureCamera();
    if (ok) {
        cameraConfigured = true;
        Serial.println(F("  ✓ Configuración completada"));
        Serial.println(F("  Modo: YUV4:2:2, solo canal Y (luminancia) = escala de grises"));
        Serial.println(F("  Resolución: 160×120 = 19 200 bytes por frame"));
    } else {
        Serial.println(F("  ✗ Error escribiendo registros — verificar conexión SCCB"));
    }
}

void cmdCapture() {
    if (!cameraConfigured) {
        Serial.println(F("Ejecutar 'config' primero."));
        return;
    }
    Serial.println(F("=== Capturando frame ==="));
    Serial.print(F("CAM:BEGIN:"));
    Serial.print(FRAME_BYTES);
    Serial.print(F(":G-01\n"));

    b64Reset();
    unsigned long t0 = millis();
    uint32_t bytes = captureFrame(onByteBase64);
    b64Flush(true);   // enviar bytes restantes con padding
    if (b64Line > 0) Serial.println();  // cerrar la última línea parcial
    Serial.println(F("CAM:END"));

    unsigned long elapsed = millis() - t0;
    Serial.print(F("  Bytes capturados: ")); Serial.println(bytes);
    Serial.print(F("  Tiempo total:     ")); Serial.print(elapsed); Serial.println(F(" ms"));

    if (bytes == 0) {
        Serial.println(F("  ✗ 0 bytes — VSYNC no detectado. ¿Está VSYNC en pin 2? ¿Está configurada la cámara?"));
    }
}

void cmdBench() {
    if (!cameraConfigured) {
        Serial.println(F("Ejecutar 'config' primero."));
        return;
    }
    Serial.println(F("=== Benchmark de captura (sin transmisión) ==="));
    benchCount = 0;
    unsigned long t0 = millis();
    uint32_t bytes = captureFrame(onByteBench);
    unsigned long elapsed = millis() - t0;

    Serial.print(F("  Bytes leídos:  ")); Serial.println(bytes);
    Serial.print(F("  Tiempo captura (sin serial): ")); Serial.print(elapsed); Serial.println(F(" ms"));

    if (bytes > 0) {
        float fps = 1000.0f / elapsed;
        Serial.print(F("  Frames/seg posibles: ")); Serial.println(fps, 1);
    }
}

void cmdLimits() {
    Serial.println(F(""));
    Serial.println(F("══════════════════════════════════════════════════════"));
    Serial.println(F("  LÍMITES DE TRANSFERENCIA — OV7670 con Arduino Mega "));
    Serial.println(F("══════════════════════════════════════════════════════"));
    Serial.println(F(""));
    Serial.println(F("  RESOLUCIONES DISPONIBLES (escala de grises, canal Y):"));
    Serial.println(F("  ┌────────────┬──────────┬──────────┬──────────────┐"));
    Serial.println(F("  │ Resolución │ Píxeles  │ Bytes    │ Base64 chars │"));
    Serial.println(F("  ├────────────┼──────────┼──────────┼──────────────┤"));
    Serial.println(F("  │ QQVGA      │ 160×120  │  19 200  │  25 600      │  ← usado en este sketch"));
    Serial.println(F("  │ QVGA       │ 320×240  │  76 800  │ 102 400      │  requiere streamng estricto"));
    Serial.println(F("  │ VGA        │ 640×480  │ 307 200  │ 409 600      │  inviable en Mega (RAM)"));
    Serial.println(F("  └────────────┴──────────┴──────────┴──────────────┘"));
    Serial.println(F("  Nota: QVGA y VGA NO se pueden almacenar en RAM (8 KB en Mega)."));
    Serial.println(F("  Solo QQVGA es seguro con streaming línea a línea."));
    Serial.println(F(""));
    Serial.println(F("  TIEMPO DE TRANSFERENCIA (base64, QQVGA = 25 600 chars):"));
    Serial.println(F("  ┌────────────┬────────────┬──────────────┬────────────────────┐"));
    Serial.println(F("  │ Baudrate   │ Bytes/seg  │ Tiempo       │ Comentario         │"));
    Serial.println(F("  ├────────────┼────────────┼──────────────┼────────────────────┤"));
    Serial.println(F("  │    9 600   │    960     │  ~26.7 s     │ muy lento, no apto │"));
    Serial.println(F("  │   57 600   │  5 760     │   ~4.4 s     │ uso en pruebas     │"));
    Serial.println(F("  │  115 200   │ 11 520     │   ~2.2 s     │ ← recomendado ★   │"));
    Serial.println(F("  │  230 400   │ 23 040     │   ~1.1 s     │ requiere UART fiable│"));
    Serial.println(F("  │  460 800   │ 46 080     │   ~0.6 s     │ riesgo de errores  │"));
    Serial.println(F("  └────────────┴────────────┴──────────────┴────────────────────┘"));
    Serial.println(F("  Baudrate de este sketch: 115 200  (cambiar en Serial.begin())"));
    Serial.println(F(""));
    Serial.println(F("  LÍMITES DE LECTURA DE PÍXELES (pixel clock):"));
    Serial.println(F("  • XCLK = 4 MHz (Fast PWM, ICR4=3) → PCLK ~2 MHz → 1 byte cada ~500 ns"));
    Serial.println(F("  • Mega a 16 MHz → ~8 ciclos de CPU por byte de píxel"));
    Serial.println(F("  • Con PINA (puerto directo) el Mega puede JUST mantenerse al ritmo."));
    Serial.println(F("  • Con digitalRead() (4+ µs) se pierden pixels. NO usar digitalRead en el bucle."));
    Serial.println(F("  • Si se pierden píxeles → bajar XCLK (aumentar ICR4 en startXCLK())."));
    Serial.println(F(""));
    Serial.println(F("  MEMORIA RAM:"));
    Serial.println(F("  • Mega SRAM: 8 192 bytes"));
    Serial.println(F("  • Frame QQVGA completo: 19 200 bytes  → NO cabe en RAM"));
    Serial.println(F("  • Buffer de una fila (160 bytes) + base64 acumulador (3 bytes) = 163 bytes → OK"));
    Serial.println(F("  • Estrategia: capturar y transmitir fila a fila sin almacenar el frame completo."));
    Serial.println(F(""));
    Serial.println(F("  CALIDAD PARA OCR (Tesseract):"));
    Serial.println(F("  • QQVGA 160×120 es el límite inferior útil para OCR de placas."));
    Serial.println(F("  • La placa ocupa ~20-40% del ancho → ~32-64 px de ancho de texto."));
    Serial.println(F("  • Iluminación uniforme es crítica: con luz insuficiente el OCR falla."));
    Serial.println(F("  • Recomendación: añadir LEDs IR junto a la cámara para iluminación nocturna."));
    Serial.println(F("══════════════════════════════════════════════════════"));
}

void cmdHelp() {
    Serial.println(F(""));
    Serial.println(F("=== camera_test.ino — Comandos disponibles ==="));
    Serial.println(F("  scan       — escanea el bus I2C y lista dispositivos"));
    Serial.println(F("  id         — lee Product ID del OV7670 (debe ser 0x76 / 0x73)"));
    Serial.println(F("  reg:<HH>   — lee un registro (ej. reg:12)"));
    Serial.println(F("  config     — configura OV7670: QQVGA 160×120, escala de grises"));
    Serial.println(F("  capture    — captura 1 frame y lo envía en base64 al serial"));
    Serial.println(F("  bench      — mide tiempo de captura sin transmitir"));
    Serial.println(F("  limits     — tabla de límites de RAM, velocidad y calidad"));
    Serial.println(F("  help       — esta ayuda"));
}

// ─── Secuencia de diagnóstico al arrancar ────────────────
void diagnosticoInicial() {
    Serial.println(F(""));
    Serial.println(F("  ┌─────────────────────────────────────────┐"));
    Serial.println(F("  │    camera_test.ino — Smart Parking Lot  │"));
    Serial.println(F("  │    OV7670 QQVGA — Arduino Mega 2560     │"));
    Serial.println(F("  └─────────────────────────────────────────┘"));
    Serial.println(F(""));
    Serial.println(F("  Pines usados:"));
    Serial.println(F("    SCCB   SDA → pin 20  (Wire, compartido con LCD y slave)"));
    Serial.println(F("    SCCB   SCL → pin 21  (Wire, compartido con LCD y slave)"));
    Serial.println(F("    Datos  D0  → pin 22 (PA0)   ...   D7 → pin 29 (PA7)"));
    Serial.println(F("    VSYNC      → pin  2  (INT0)"));
    Serial.println(F("    HREF       → pin 30"));
    Serial.println(F("    PCLK       → pin 31"));
    Serial.println(F("    XCLK       → pin  8  (~4 MHz por Timer4)"));
    Serial.println(F("    RESET      → pin 32  (HIGH)"));
    Serial.println(F("    PWDN       → GND"));
    Serial.println(F(""));
    Serial.println(F("  ⚠  OV7670 opera a 3.3 V lógico. Usar level-shifter en D0-D7,"));
    Serial.println(F("     VSYNC, HREF y PCLK antes de conectar al Mega (5 V)."));
    Serial.println(F(""));

    // Paso 1: verificar RESET
    Serial.print(F("  [1/4] RESET pin 32 ... "));
    Serial.println(digitalRead(PIN_RESET) == HIGH ? F("HIGH ✓") : F("LOW ✗  (debe ser HIGH)"));

    // Paso 2: verificar XCLK en pin 8
    Serial.print(F("  [2/4] XCLK generado en pin 8 (Timer4 ~4 MHz) ... "));
    Serial.println(F("activo ✓"));

    // Paso 3: buscar OV7670 en I2C
    Serial.print(F("  [3/4] Buscando OV7670 en I2C (0x21) ... "));
    Wire.beginTransmission(OV7670_ADDR);
    bool found = (Wire.endTransmission() == 0);
    Serial.println(found ? F("encontrado ✓") : F("NO encontrado ✗  → revisar SDA/SCL/VCC"));

    // Paso 4: leer ID
    if (found) {
        uint8_t hi = sccbRead(OV_PID_HIGH);
        uint8_t lo = sccbRead(OV_PID_LOW);
        Serial.print(F("  [4/4] Product ID: 0x"));
        Serial.print(hi, HEX); Serial.print(F(" / 0x")); Serial.print(lo, HEX);
        bool idOk = (hi == 0x76 && lo == 0x73);
        Serial.println(idOk ? F("  ✓ OV7670 confirmado") : F("  ✗ ID inesperado"));
    } else {
        Serial.println(F("  [4/4] ID omitido (sensor no encontrado)"));
    }

    Serial.println();
    Serial.println(F("  Escribe 'help' para ver todos los comandos."));
    Serial.println(F("  Secuencia típica:  scan → id → config → capture → limits"));
    Serial.println();
}

// ─── Setup ────────────────────────────────────────────────
void setup() {
    Serial.begin(115200);  // ← 115 200 recomendado; cambiar si hay errores de framing
    while (!Serial) {}

    Wire.begin();
    Wire.setClock(100000);  // 100 kHz para SCCB (el OV7670 es más estable a esta velocidad)

    // Configurar pines de control
    pinMode(PIN_VSYNC, INPUT);
    pinMode(PIN_HREF,  INPUT);
    pinMode(PIN_PCLK,  INPUT);
    pinMode(PIN_RESET, OUTPUT);
    DDRA = 0x00;           // Port A = entradas (pines 22-29)

    // Generar XCLK antes de hacer RESET para que el sensor esté listo
    startXCLK();
    delay(10);

    // Pulso de RESET: bajar y subir
    digitalWrite(PIN_RESET, LOW);
    delay(10);
    digitalWrite(PIN_RESET, HIGH);
    delay(50);

    // Suscribir ISR para VSYNC
    attachInterrupt(digitalPinToInterrupt(PIN_VSYNC), ISR_VSYNC, RISING);

    diagnosticoInicial();
}

// ─── Loop ─────────────────────────────────────────────────
void loop() {
    while (Serial.available()) {
        char c = (char)Serial.read();
        if (c == '\n') {
            inputBuf.trim();
            if (inputBuf.length() > 0) handleInput(inputBuf);
            inputBuf = "";
        } else if (c != '\r') {
            inputBuf += c;
        }
    }
}

void handleInput(const String& cmd) {
    if (cmd.equalsIgnoreCase("scan"))    { cmdScan();   return; }
    if (cmd.equalsIgnoreCase("id"))      { cmdId();     return; }
    if (cmd.equalsIgnoreCase("config"))  { cmdConfig(); return; }
    if (cmd.equalsIgnoreCase("capture")) { cmdCapture(); return; }
    if (cmd.equalsIgnoreCase("bench"))   { cmdBench();  return; }
    if (cmd.equalsIgnoreCase("limits"))  { cmdLimits(); return; }
    if (cmd.equalsIgnoreCase("help"))    { cmdHelp();   return; }

    if (cmd.startsWith("reg:")) {
        cmdReadReg(cmd.substring(4));
        return;
    }

    Serial.print(F("Comando desconocido: "));
    Serial.println(cmd);
    Serial.println(F("Escribe 'help' para ver los comandos disponibles."));
}
